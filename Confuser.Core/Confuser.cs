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
        Logger log;
        internal void Log(string message) { log.Log(message); }

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
                log = param.Logger;
                log.StartPhase(1);
                log.Log("Started at " + DateTime.Now.ToShortTimeString() + ".");
                log.Log("Loading...");

                System.Reflection.StrongNameKeyPair sn = null;
                if (string.IsNullOrEmpty(param.StrongNameKeyPath))
                    log.Log("Strong name key not specified.");
                else if (!File.Exists(param.StrongNameKeyPath))
                    log.Log("Strong name key not found. Output assembly will not be signed.");
                else
                    sn = new System.Reflection.StrongNameKeyPair(new FileStream(param.StrongNameKeyPath, FileMode.Open));

                Marker mkr = param.Marker;

                GlobalAssemblyResolver.Instance.AssemblyCache.Clear();
                GlobalAssemblyResolver.Instance.ClearSearchDirectory();
                GlobalAssemblyResolver.Instance.AddSearchDirectory(param.ReferencesPath);
                AssemblyDefinition[] asms = mkr.ExtractDatas(param.SourceAssembly);

                mkr.Initalize(param.Confusions, param.Packers);
                log.Log(string.Format("Analysing assemblies..."));
                for (int z = 0; z < asms.Length; z++)
                {
                    mkr.MarkAssembly(asms[z], param.DefaultPreset, this);
                }

                helpers = new Dictionary<IMemberDefinition, HelperAttribute>();

                List<IEngine> engines = new List<IEngine>();
                foreach (IConfusion cion in param.Confusions)
                    foreach (Phase phase in cion.Phases)
                        if (phase.GetEngine() != null)
                            engines.Add(phase.GetEngine());
                for (int i = 0; i < engines.Count; i++)
                {
                    engines[i].Analysis(param.Logger, asms);
                    log.Progress((double)(i + 1) / engines.Count);
                }

                List<byte[]> pes = new List<byte[]>();
                List<ModuleDefinition> mods = new List<ModuleDefinition>();
                foreach (AssemblyDefinition asm in asms)
                {
                    log.StartPhase(2);
                    log.Log(string.Format("Obfuscating assembly {0}...", asm.FullName));

                    IDictionary<IConfusion, NameValueCollection> globalParams = GetGlobalParams(asm);
                    List<Phase> phases = new List<Phase>();
                    foreach (IConfusion cion in param.Confusions)
                        foreach (Phase phase in cion.Phases)
                            phases.Add(phase);

                    foreach (ModuleDefinition mod in asm.Modules)
                    {
                        log.Log(string.Format("Obfuscating structure of module {0}...", mod.Name));

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
                            log.Log("Executing " + i.Confusion.Name + " Phase " + i.PhaseID + "...");

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
                                log.Progress(1);
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
                                    (i as IProgressProvider).SetProgresser(log);
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
                                            log.Progress((now + 1) / total);
                                        now++;
                                    }
                                }
                                log.Progress(1);
                            }
                            i.DeInitialize();
                        }

                        log.StartPhase(3);

                        MemoryStream final = new MemoryStream();
                        MetadataProcessor psr = new MetadataProcessor();
                        double total1 = (from i in phases where (i is MetadataPhase) select i).Count();
                        int now1 = 1;
                        psr.BeforeBuildModule += new MetadataProcessor.MetadataProcess(delegate(MetadataProcessor.MetadataAccessor accessor)
                        {
                            foreach (MetadataPhase i in from i in phases where (i is MetadataPhase) && i.PhaseID == 1 orderby i.Priority ascending select i)
                            {
                                if (GetTargets(mod, i.Confusion).Count == 0) continue;
                                log.Log("Executing " + i.Confusion.Name + " Phase 1...");
                                i.Process(globalParams[i.Confusion], accessor);
                                log.Progress(now1 / total1); now1++;
                            }
                        });
                        psr.BeforeWriteTables += new MetadataProcessor.MetadataProcess(delegate(MetadataProcessor.MetadataAccessor accessor)
                        {
                            foreach (MetadataPhase i in from i in phases where (i is MetadataPhase) && i.PhaseID == 2 orderby i.Priority ascending select i)
                            {
                                if (GetTargets(mod, i.Confusion).Count == 0) continue;
                                log.Log("Executing " + i.Confusion.Name + " Phase 2...");
                                i.Process(globalParams[i.Confusion], accessor);
                                log.Progress(now1 / total1); now1++;
                            }
                        });
                        psr.AfterWriteTables += new MetadataProcessor.MetadataProcess(delegate(MetadataProcessor.MetadataAccessor accessor)
                        {
                            foreach (MetadataPhase i in from i in phases where (i is MetadataPhase) && i.PhaseID == 3 orderby i.Priority ascending select i)
                            {
                                if (GetTargets(mod, i.Confusion).Count == 0) continue;
                                log.Log("Executing " + i.Confusion.Name + " Phase 3...");
                                i.Process(globalParams[i.Confusion], accessor);
                                log.Progress(now1 / total1); now1++;
                            }
                        });
                        psr.ProcessPe += new MetadataProcessor.PeProcess(delegate(Stream stream)
                        {
                            log.StartPhase(4);
                            log.Log(string.Format("Obfuscating PE of module {0}...", mod.Name));
                            PePhase[] pePhases = (from i in phases where (i is PePhase) orderby (int)i.Priority + i.PhaseID * 10 ascending select (PePhase)i).ToArray();
                            for (int i = 0; i < pePhases.Length; i++)
                            {
                                if (GetTargets(mod, pePhases[i].Confusion).Count == 0) continue;
                                log.Log("Executing " + pePhases[i].Confusion.Name + " Phase 3...");
                                pePhases[i].Process(globalParams[pePhases[i].Confusion], stream);
                                log.Progress((double)i / pePhases.Length);
                            }
                        });
                        log.Log(string.Format("Obfuscating metadata of module {0}...", mod.Name));
                        psr.Process(mod, final, new WriterParameters() { StrongNameKeyPair = sn });

                        pes.Add(final.ToArray());
                        mods.Add(mod);

                        log.Log("Module " + mod.Name + " Done.");
                    }
                }

                Packer packer = (Packer)(asms[0] as IAnnotationProvider).Annotations["Packer"];
                if (asms[0].MainModule.Kind == ModuleKind.Dll ||
                    asms[0].MainModule.Kind == ModuleKind.NetModule)
                {
                    log.Log("Warning: Cannot pack a library or net module!");
                    packer = null;
                }
                if (packer != null)
                {
                    string dest = param.Marker.GetDestinationPath(asms[0].MainModule, param.DestinationPath);
                    if (!Directory.Exists(System.IO.Path.GetDirectoryName(dest)))
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));
                    Stream dstStream = new FileStream(dest, FileMode.Create, FileAccess.Write);

                    try
                    {
                        log.Log("Packing output assemblies...");
                        packer.Confuser = this;
                        PackerParameter pParam = new PackerParameter();
                        pParam.Modules = mods.ToArray(); pParam.PEs = pes.ToArray();
                        pParam.Parameters = (NameValueCollection)(asms[0] as IAnnotationProvider).Annotations["PackerParams"];
                        byte[] final = packer.Pack(param, pParam);
                        dstStream.Write(final, 0, final.Length);
                    }
                    finally
                    {
                        dstStream.Dispose();
                    }
                }
                else
                {
                    log.Log("Writing outputs...");
                    for (int i = 0; i < pes.Count; i++)
                    {
                        string dest = param.Marker.GetDestinationPath(mods[i], param.DestinationPath);
                        if (!Directory.Exists(System.IO.Path.GetDirectoryName(dest)))
                            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest));
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

                log.Finish("Ended at " + DateTime.Now.ToShortTimeString() + ".");
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
