using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Metadata;
using System.Collections.Specialized;

namespace Confuser.Core
{
    public class PhaseEventArgs : EventArgs
    {
        public PhaseEventArgs(int phase) { this.phase = phase; }
        int phase;
        public int Phase { get { return phase; } }
    }
    public class LogEventArgs : EventArgs
    {
        public LogEventArgs(string message) { this.message = message; }
        string message;
        public string Message { get { return message; } }
    }
    public class ProgressEventArgs : EventArgs
    {
        public ProgressEventArgs(double progress) { this.progress = progress; }
        double progress;
        public double Progress { get { return progress; } }
    }
    public class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(Exception ex) { this.ex = ex; }
        Exception ex;
        public Exception Exception { get { return ex; } }
    }

    public class Logger : IProgresser
    {
        public event EventHandler<PhaseEventArgs> BeginPhase;
        public event EventHandler<LogEventArgs> Logging;
        public event EventHandler<ProgressEventArgs> Progressing;
        public event EventHandler<ExceptionEventArgs> Fault;
        public event EventHandler<LogEventArgs> End;

        internal void Log(string message)
        {
            if (Logging != null)
                Logging(this, new LogEventArgs(message));
        }
        internal void StartPhase(int phase)
        {
            if (BeginPhase != null)
                BeginPhase(this, new PhaseEventArgs(phase));
        }
        internal void Progress(double precentage)
        {
            if (Progressing != null)
                Progressing(this, new ProgressEventArgs(precentage));
        }
        internal void Fatal(Exception ex)
        {
            if (Fault != null)
                Fault(this, new ExceptionEventArgs(ex));
            else
                throw ex;
        }
        internal void Finish(string message)
        {
            if (End != null)
                End(this, new LogEventArgs(message));
        }

        void IProgresser.SetProgress(double precentage)
        {
            Progress(precentage);
        }
    }
    public interface IProgresser
    {
        void SetProgress(double precentage);
    }
    public interface IProgressProvider
    {
        void SetProgresser(IProgresser progresser);
    }

    public class ConfuserParameter
    {
        string src = "";
        public string SourceAssembly { get { return src; } set { src = value; } }

        string dstPath = "";
        public string DestinationPath { get { return dstPath; } set { dstPath = value; } }

        string refers = "";
        public string ReferencesPath { get { return refers; } set { refers = value; } }

        IConfusion[] cions = new IConfusion[0];
        public IConfusion[] Confusions { get { return cions; } set { cions = value; } }

        Preset preset = Preset.Normal;
        public Preset DefaultPreset { get { return preset; } set { preset = value; } }

        Packer[] packers = new Packer[0];
        public Packer[] Packers { get { return packers; } set { packers = value; } }

        string sn = "";
        public string StrongNameKeyPath { get { return sn; } set { sn = value; } }

        Logger log = new Logger();
        public Logger Logger { get { return log; } }

        Marker mkr = new Marker();
        public Marker Marker { get { return mkr; } set { mkr = value; } }
    }

    public class Confuser
    {
        internal ConfuserParameter param;
        internal List<AssemblyDefinition> assemblies;
        internal List<IEngine> engines;
        internal void Log(string message) { param.Logger.Log(message); }

        public Thread ConfuseAsync(ConfuserParameter param)
        {
            Thread thread = new Thread(delegate() { Confuse(param); });
            thread.IsBackground = true;
            thread.Name = "Confuuusing";
            thread.Start();
            return thread;
        }

        public void Confuse(ConfuserParameter param)
        {
            try
            {
                this.param = param;
                param.Logger.StartPhase(1);
                param.Logger.Log("Started at " + DateTime.Now.ToShortTimeString() + ".");
                param.Logger.Log("Loading...");

                System.Reflection.StrongNameKeyPair sn;
                Initialize(out sn);

                List<byte[]> pes = new List<byte[]>();
                List<ModuleDefinition> mods = new List<ModuleDefinition>();
                for (int i = 0; i < assemblies.Count; i++)
                {
                    param.Logger.StartPhase(2);
                    param.Logger.Log(string.Format("Obfuscating assembly {0}...", assemblies[i].FullName));

                    IDictionary<IConfusion, NameValueCollection> globalParams = GetGlobalParams(assemblies[i]);
                    List<Phase> phases = new List<Phase>();
                    foreach (IConfusion cion in param.Confusions)
                        foreach (Phase phase in cion.Phases)
                            phases.Add(phase);

                    foreach (ModuleDefinition mod in assemblies[i].Modules)
                    {
                        if (sn != null && mod.Assembly != null)
                        {
                            if (mod.Assembly.MainModule == mod)
                                mod.Assembly.Name.PublicKey = sn.PublicKey;
                            mod.Attributes |= ModuleAttributes.StrongNameSigned;
                        }
                        else
                        {
                            if (mod.Assembly != null && mod.Assembly.MainModule == mod)
                                mod.Assembly.Name.PublicKey = null;
                            mod.Attributes &= ~ModuleAttributes.StrongNameSigned;
                        }
                        param.Logger.Log(string.Format("Obfuscating structure of module {0}...", mod.Name));

                        helpers.Clear();
                        //global cctor which used in many confusion
                        if (mod.GetType("<Module>").GetStaticConstructor() == null)
                        {
                            MethodDefinition cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.HideBySig |
                                MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
                                MethodAttributes.Static, mod.Import(typeof(void)));
                            cctor.Body = new MethodBody(cctor);
                            cctor.Body.GetILProcessor().Emit(OpCodes.Ret);
                            mod.GetType("<Module>").Methods.Add(cctor);
                        }
                        else
                        {
                            MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                            ((IAnnotationProvider)cctor).Annotations.Clear();
                        }
                        helpers.Add(mod.GetType("<Module>").GetStaticConstructor(), HelperAttribute.NoEncrypt);

                        ProcessStructuralPhases(mod, globalParams, phases);

                        param.Logger.StartPhase(3);

                        MemoryStream final = new MemoryStream();
                        ProcessMdPePhases(mod, globalParams, phases, final, new WriterParameters() { StrongNameKeyPair = (mod.Attributes & ModuleAttributes.StrongNameSigned) != 0 ? sn : null });

                        pes.Add(final.ToArray());
                        mods.Add(mod);

                        param.Logger.Log("Module " + mod.Name + " Done.");
                    }
                }

                Finalize(mods.ToArray(), pes.ToArray());

                param.Logger.Finish("Ended at " + DateTime.Now.ToShortTimeString() + ".");
            }
            catch (Exception ex)
            {
                param.Logger.Fatal(ex);
            }
            finally
            {
                GlobalAssemblyResolver.Instance.AssemblyCache.Clear();
                GC.Collect();
            }
        }

        void Initialize(out System.Reflection.StrongNameKeyPair sn)
        {
            sn = null;
            if (string.IsNullOrEmpty(param.StrongNameKeyPath))
                param.Logger.Log("Strong name key not specified.");
            else if (!File.Exists(param.StrongNameKeyPath))
                param.Logger.Log("Strong name key not found. Output assembly will not be signed.");
            else
                sn = new System.Reflection.StrongNameKeyPair(new FileStream(param.StrongNameKeyPath, FileMode.Open));

            Marker mkr = param.Marker;

            GlobalAssemblyResolver.Instance.AssemblyCache.Clear();
            GlobalAssemblyResolver.Instance.ClearSearchDirectories();
            GlobalAssemblyResolver.Instance.AddSearchDirectory(param.ReferencesPath);

            param.Logger.Log(string.Format("Analysing assemblies..."));
            mkr.Initalize(param.Confusions, param.Packers);
            assemblies = new List<AssemblyDefinition>(mkr.GetAssemblies(param.SourceAssembly, param.DefaultPreset, this, (sender, e) => param.Logger.Log(e.Message)));

            helpers = new Dictionary<IMemberDefinition, HelperAttribute>();

            engines = new List<IEngine>();
            foreach (IConfusion cion in param.Confusions)
                foreach (Phase phase in cion.Phases)
                    if (phase.GetEngine() != null)
                        engines.Add(phase.GetEngine());
            for (int i = 0; i < engines.Count; i++)
            {
                engines[i].Analysis(param.Logger, assemblies);
                param.Logger.Progress((double)(i + 1) / engines.Count);
            }
        }
        void ProcessStructuralPhases(ModuleDefinition mod, IDictionary<IConfusion, NameValueCollection> globalParams, IEnumerable<Phase> phases)
        {
            ConfusionParameter cParam = new ConfusionParameter();
            bool end1 = false;
            foreach (StructurePhase i in from i in phases where (i is StructurePhase) orderby (int)i.Priority + i.PhaseID * 10 ascending select i)
            {
                if (!end1 && i.PhaseID > 1)
                {
                    MarkModule(mod);
                    MarkObfuscateHelpers(mod);
                    CecilHelper.RefreshTokens(mod);
                    end1 = true;
                }
                List<IAnnotationProvider> mems = GetTargets(mod, i.Confusion);
                if (mems.Count == 0) continue;

                i.Confuser = this;
                param.Logger.Log("Executing " + i.Confusion.Name + " Phase " + i.PhaseID + "...");

                i.Initialize(mod);
                if (globalParams.ContainsKey(i.Confusion))
                    cParam.GlobalParameters = globalParams[i.Confusion];
                else
                    cParam.GlobalParameters = new NameValueCollection();
                if (i.WholeRun == true)
                {
                    cParam.Parameters = null;
                    cParam.Target = null;
                    i.Process(cParam);
                    param.Logger.Progress(1);
                }
                else
                {
                    if (i is IProgressProvider)
                    {
                        cParam.Parameters = new NameValueCollection();
                        foreach (IAnnotationProvider mem in mems)
                        {
                            NameValueCollection memParam = (from set in mem.Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection> where set.Key.Phases.Contains(i) select set.Value).FirstOrDefault();
                            string hash = mem.GetHashCode().ToString("X8");
                            foreach (string pkey in memParam.AllKeys)
                                cParam.Parameters[hash + "_" + pkey] = memParam[pkey];
                        }
                        cParam.Target = mems;
                        (i as IProgressProvider).SetProgresser(param.Logger);
                        i.Process(cParam);
                    }
                    else
                    {
                        double total = mems.Count;
                        int interval = 1;
                        if (total > 1000)
                            interval = (int)total / 100;
                        int now = 0;
                        foreach (IAnnotationProvider mem in mems)
                        {
                            cParam.Parameters = (from set in mem.Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection> where set.Key.Phases.Contains(i) select set.Value).FirstOrDefault();
                            cParam.Target = mem;
                            i.Process(cParam);
                            if (now % interval == 0 || now == total - 1)
                                param.Logger.Progress((now + 1) / total);
                            now++;
                        }
                    }
                    param.Logger.Progress(1);
                }
                i.DeInitialize();
            }
        }
        void ProcessMdPePhases(ModuleDefinition mod, IDictionary<IConfusion, NameValueCollection> globalParams, IEnumerable<Phase> phases, Stream stream, WriterParameters parameters)
        {
            MetadataProcessor psr = new MetadataProcessor();
            double total1 = (from i in phases where (i is MetadataPhase) select i).Count();
            int now1 = 1;
            psr.BeforeBuildModule += new MetadataProcessor.MetadataProcess(delegate(MetadataProcessor.MetadataAccessor accessor)
            {
                foreach (MetadataPhase i in from i in phases where (i is MetadataPhase) && i.PhaseID == 1 orderby i.Priority ascending select i)
                {
                    if (GetTargets(mod, i.Confusion).Count == 0) continue;
                    param.Logger.Log("Executing " + i.Confusion.Name + " Phase 1...");
                    i.Confuser = this;
                    NameValueCollection globalParam;
                    if (globalParams.ContainsKey(i.Confusion))
                        globalParam = globalParams[i.Confusion];
                    else
                        globalParam = new NameValueCollection();
                    i.Process(globalParam, accessor);
                    param.Logger.Progress(now1 / total1); now1++;
                }
            });
            psr.BeforeWriteTables += new MetadataProcessor.MetadataProcess(delegate(MetadataProcessor.MetadataAccessor accessor)
            {
                foreach (MetadataPhase i in from i in phases where (i is MetadataPhase) && i.PhaseID == 2 orderby i.Priority ascending select i)
                {
                    if (GetTargets(mod, i.Confusion).Count == 0) continue;
                    param.Logger.Log("Executing " + i.Confusion.Name + " Phase 2...");
                    i.Confuser = this;
                    NameValueCollection globalParam;
                    if (globalParams.ContainsKey(i.Confusion))
                        globalParam = globalParams[i.Confusion];
                    else
                        globalParam = new NameValueCollection();
                    i.Process(globalParam, accessor);
                    param.Logger.Progress(now1 / total1); now1++;
                }
            });
            psr.AfterWriteTables += new MetadataProcessor.MetadataProcess(delegate(MetadataProcessor.MetadataAccessor accessor)
            {
                foreach (MetadataPhase i in from i in phases where (i is MetadataPhase) && i.PhaseID == 3 orderby i.Priority ascending select i)
                {
                    if (GetTargets(mod, i.Confusion).Count == 0) continue;
                    param.Logger.Log("Executing " + i.Confusion.Name + " Phase 3...");
                    i.Confuser = this;
                    NameValueCollection globalParam;
                    if (globalParams.ContainsKey(i.Confusion))
                        globalParam = globalParams[i.Confusion];
                    else
                        globalParam = new NameValueCollection();
                    i.Process(globalParam, accessor);
                    param.Logger.Progress(now1 / total1); now1++;
                }
            });
            psr.ProcessImage += new MetadataProcessor.ImageProcess(delegate(MetadataProcessor.ImageAccessor accessor)
            {
                param.Logger.StartPhase(4);
                param.Logger.Log(string.Format("Obfuscating Image of module {0}...", mod.Name));
                ImagePhase[] imgPhases = (from i in phases where (i is ImagePhase) orderby (int)i.Priority + i.PhaseID * 10 ascending select (ImagePhase)i).ToArray();
                for (int i = 0; i < imgPhases.Length; i++)
                {
                    if (GetTargets(mod, imgPhases[i].Confusion).Count == 0) continue;
                    param.Logger.Log("Executing " + imgPhases[i].Confusion.Name + " Phase " + imgPhases[i].PhaseID + "...");
                    imgPhases[i].Confuser = this;
                    NameValueCollection globalParam;
                    if (globalParams.ContainsKey(imgPhases[i].Confusion))
                        globalParam = globalParams[imgPhases[i].Confusion];
                    else
                        globalParam = new NameValueCollection();
                    imgPhases[i].Process(globalParam, accessor);
                    param.Logger.Progress((double)i / imgPhases.Length);
                }
            });
            psr.ProcessPe += new MetadataProcessor.PeProcess(delegate(Stream str)
            {
                param.Logger.Log(string.Format("Obfuscating PE of module {0}...", mod.Name));
                PePhase[] pePhases = (from i in phases where (i is PePhase) orderby (int)i.Priority + i.PhaseID * 10 ascending select (PePhase)i).ToArray();
                for (int i = 0; i < pePhases.Length; i++)
                {
                    if (GetTargets(mod, pePhases[i].Confusion).Count == 0) continue;
                    param.Logger.Log("Executing " + pePhases[i].Confusion.Name + " Phase " + pePhases[i].PhaseID + "...");
                    pePhases[i].Confuser = this;
                    NameValueCollection globalParam;
                    if (globalParams.ContainsKey(pePhases[i].Confusion))
                        globalParam = globalParams[pePhases[i].Confusion];
                    else
                        globalParam = new NameValueCollection();
                    pePhases[i].Process(globalParam, str);
                    param.Logger.Progress((double)i / pePhases.Length);
                }
            });
            param.Logger.Log(string.Format("Obfuscating metadata of module {0}...", mod.Name));
            psr.Process(mod, stream, parameters);
        }
        void Finalize(ModuleDefinition[] mods, byte[][] pes)
        {
            Packer packer = (Packer)(assemblies[0] as IAnnotationProvider).Annotations["Packer"];
            if (assemblies[0].MainModule.Kind == ModuleKind.Dll ||
                assemblies[0].MainModule.Kind == ModuleKind.NetModule)
            {
                param.Logger.Log("Warning: Cannot pack a library or net module!");
                packer = null;
            }
            if (packer != null)
            {
                if (!Directory.Exists(param.DestinationPath))
                    Directory.CreateDirectory(param.DestinationPath);

                param.Logger.Log("Packing output assemblies...");
                packer.Confuser = this;
                PackerParameter pParam = new PackerParameter();
                pParam.Modules = mods; pParam.PEs = pes;
                pParam.Parameters = (NameValueCollection)(assemblies[0] as IAnnotationProvider).Annotations["PackerParams"];
                string[] final = packer.Pack(param, pParam);
                for (int i = 0; i < final.Length; i++)
                    File.Move(final[i], Path.Combine(param.DestinationPath, Path.GetFileName(final[i])));
            }
            else
            {
                param.Logger.Log("Writing outputs...");
                for (int i = 0; i < pes.Length; i++)
                {
                    string dest = param.Marker.GetDestinationPath(mods[i], param.DestinationPath);
                    if (!Directory.Exists(Path.GetDirectoryName(dest)))
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    Stream dstStream = new FileStream(dest, FileMode.Create, FileAccess.Write);
                    try
                    {
                        dstStream.Write(pes[i], 0, pes[i].Length);
                    }
                    finally
                    {
                        dstStream.Dispose();
                    }
                }
            }
        }



        void MarkModule(ModuleDefinition mod)
        {
            TypeDefinition att = new TypeDefinition("", "ConfusedByAttribute", TypeAttributes.Class | TypeAttributes.NotPublic, mod.Import(typeof(Attribute)));
            MethodDefinition ctor = new MethodDefinition(".ctor", MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.Public, mod.Import(typeof(void)));
            ctor.Parameters.Add(new ParameterDefinition(mod.Import(typeof(string))));
            ILProcessor psr = (ctor.Body = new MethodBody(ctor)).GetILProcessor();
            psr.Emit(OpCodes.Ldarg_0);
            psr.Emit(OpCodes.Call, mod.Import(typeof(Attribute).GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null)));
            psr.Emit(OpCodes.Ret);
            att.Methods.Add(ctor);
            mod.Types.Add(att);

            CustomAttribute ca = new CustomAttribute(ctor);
            ca.ConstructorArguments.Add(new CustomAttributeArgument(mod.Import(typeof(string)), string.Format("Confuser v" + typeof(Confuser).Assembly.GetName().Version.ToString())));
            mod.CustomAttributes.Add(ca);
        }

        IDictionary<IConfusion, NameValueCollection> GetGlobalParams(AssemblyDefinition asm)
        {
            return (asm as IAnnotationProvider).Annotations["GlobalParams"] as IDictionary<IConfusion, NameValueCollection>;
        }

        List<IAnnotationProvider> GetTargets(ModuleDefinition mod, IConfusion cion)
        {
            List<IAnnotationProvider> mems = new List<IAnnotationProvider>();
            IDictionary<IConfusion, NameValueCollection> sets = (mod as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
            if (sets != null)
                foreach (KeyValuePair<IConfusion, NameValueCollection> kv in sets)
                    if (kv.Key == cion && (kv.Key.Target & Target.Module) == Target.Module)
                        mems.Add(mod);
            foreach (TypeDefinition type in mod.Types)
            {
                sets = (type as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> kv in sets)
                        if (kv.Key == cion && (kv.Key.Target & Target.Types) == Target.Types)
                            mems.Add(type);
                GetTargets(type, mems, cion);
            }
            return mems;
        }
        void GetTargets(TypeDefinition type, List<IAnnotationProvider> mems, IConfusion cion)
        {
            foreach (TypeDefinition nType in type.NestedTypes)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (nType as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> kv in sets)
                        if (kv.Key == cion && (kv.Key.Target & Target.Types) == Target.Types)
                            mems.Add(nType);
                GetTargets(nType, mems, cion);
            }
            foreach (MethodDefinition mtd in type.Methods)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (mtd as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> kv in sets)
                        if (kv.Key == cion && (kv.Key.Target & Target.Methods) == Target.Methods)
                            mems.Add(mtd);
            }
            foreach (FieldDefinition fld in type.Fields)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (fld as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> kv in sets)
                        if (kv.Key == cion && (kv.Key.Target & Target.Fields) == Target.Fields)
                            mems.Add(fld);
            }
            foreach (EventDefinition evt in type.Events)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (evt as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> kv in sets)
                        if (kv.Key == cion && (kv.Key.Target & Target.Events) == Target.Events)
                            mems.Add(evt);
            }
            foreach (PropertyDefinition prop in type.Properties)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (prop as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> kv in sets)
                        if (kv.Key == cion && (kv.Key.Target & Target.Properties) == Target.Properties)
                            mems.Add(prop);
            }
        }

        internal Dictionary<IMemberDefinition, HelperAttribute> helpers;
        void MarkObfuscateHelpers(ModuleDefinition mod)
        {
            TypeDefinition modType = mod.GetType("<Module>");
            IDictionary<IConfusion, NameValueCollection> sets = (modType as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
            if (sets == null) return;
            Dictionary<IConfusion, NameValueCollection> sub = new Dictionary<IConfusion, NameValueCollection>();
            foreach (var i in sets)
            {
                bool ok = true;
                foreach (Phase phase in i.Key.Phases)
                    if (!(phase is MetadataPhase) && (phase.PhaseID == 1 && !phase.Confusion.SupportLateAddition))
                    {
                        ok = false;
                        break;
                    }
                if (ok)
                    sub.Add(i.Key, i.Value);
            }
            foreach (KeyValuePair<IMemberDefinition, HelperAttribute> def in helpers)
            {
                if ((def.Key as IAnnotationProvider).Annotations.Contains("ConfusionSets")) continue;

                Dictionary<IConfusion, NameValueCollection> n = new Dictionary<IConfusion, NameValueCollection>();

                Target target = 0;
                if (def.Key is TypeDefinition) target = Target.Types;
                else if (def.Key is MethodDefinition) target = Target.Methods;
                else if (def.Key is FieldDefinition) target = Target.Fields;
                else if (def.Key is EventDefinition) target = Target.Events;
                else if (def.Key is PropertyDefinition) target = Target.Properties;
                foreach (KeyValuePair<IConfusion, NameValueCollection> s in sub)
                {
                    if (s.Key.Target != target || (s.Key.Behaviour & (Behaviour)def.Value) != 0) continue;
                    n.Add(s.Key, s.Value);
                }

                (def.Key as IAnnotationProvider).Annotations["ConfusionSets"] = n;
            }
        }
    }
}
