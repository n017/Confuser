using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
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
        string refers;
        public string ReferencesPath { get { return refers; } set { refers = value; } }

        IConfusion[] cions;
        public IConfusion[] Confusions { get { return cions; } set { cions = value; } }

        Preset preset = Preset.Normal;
        public Preset DefaultPreset { get { return preset; } set { preset = value; } }

        bool comps = false;
        public bool CompressOutput { get { return comps; } set { comps = value; } }

        string sn = "";
        public string StrongNameKeyPath { get { return sn; } set { sn = value; } }

        Logger log = new Logger();
        public Logger Logger { get { return log; } }

        IMarker mkr = new DefaultMarker();
        public IMarker Marker { get { return mkr; } set { mkr = value; } }
    }

    public class Confuser
    {
        Logger log;
        internal void Log(string message) { log.Log(message); }

        public void Confuse(string src, string dst, ConfuserParameter param)
        {
            Confuse(File.OpenRead(src), File.OpenWrite(dst), param);
        }
        public void Confuse(Stream src, Stream dst, ConfuserParameter param)
        {
            try
            {
                param.Logger.StartPhase(1);
                param.Logger.Log("Started at " + DateTime.Now.ToShortTimeString() + ".");

                param.Logger.Log("Loading...");

                System.Reflection.StrongNameKeyPair sn = null;
                if (string.IsNullOrEmpty(param.StrongNameKeyPath))
                    param.Logger.Log("Strong name key not specified.");
                else if (!File.Exists(param.StrongNameKeyPath))
                    param.Logger.Log("Strong name key not found. Output assembly will not be signed.");
                else
                    sn = new System.Reflection.StrongNameKeyPair(new FileStream(param.StrongNameKeyPath, FileMode.Open));

                (GlobalAssemblyResolver.Instance as DefaultAssemblyResolver).AssemblyCache.Clear();
                AssemblyDefinition[] asms = ExtractAssemblies(src);

                IMarker mkr = param.Marker;
                mkr.Initalize(param.Confusions);
                for (int z = 0; z < asms.Length; z++)
                {
                    param.Logger.Log(string.Format("Analysing assembly...({0}/{1})", z + 1, asms.Length));
                    mkr.MarkAssembly(asms[z], param.DefaultPreset);
                }

                log = param.Logger;
                param.Logger.StartPhase(2);

                for (int z = 0; z < asms.Length; z++)
                {
                    param.Logger.Log(string.Format("Processing assembly...({0}/{1})", z + 1, asms.Length));
                    AssemblyDefinition asm = asms[z];

                    Dictionary<IConfusion, List<object>> mems = new Dictionary<IConfusion, List<object>>();
                    foreach (IConfusion cion in param.Confusions)
                        mems.Add(cion, new List<object>());
                    IDictionary<IConfusion, NameValueCollection> globalParams = FillAssembly(asm, mems);
                    Dictionary<Phase, List<object>> trueMems = new Dictionary<Phase, List<object>>();
                    foreach (KeyValuePair<IConfusion, List<object>> mem in mems)
                    {
                        if (mem.Value.Count == 0) continue;
                        foreach (Phase p in mem.Key.Phases)
                            trueMems.Add(p, mem.Value);
                    }

                    param.Logger.Log("Loading assembly reference...");

                    for (int i = 0; i < asm.MainModule.AssemblyReferences.Count; i++)
                    {
                        (GlobalAssemblyResolver.Instance as BaseAssemblyResolver).AddSearchDirectory(param.ReferencesPath);
                        GlobalAssemblyResolver.Instance.Resolve(asm.MainModule.AssemblyReferences[i]);
                        param.Logger.Progress((double)(i + 1) / asm.MainModule.AssemblyReferences.Count);
                    }

                    //global cctor which used in many confusion
                    MethodDefinition cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.HideBySig |
                        MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
                        MethodAttributes.Static, asm.MainModule.Import(typeof(void)));
                    cctor.Body = new MethodBody(cctor);
                    cctor.Body.GetILProcessor().Emit(OpCodes.Ret);
                    asm.MainModule.GetType("<Module>").Methods.Add(cctor);

                    List<IEngine> engines = new List<IEngine>();
                    foreach (Phase phase in trueMems.Keys)
                        if (phase.GetEngine() != null)
                            engines.Add(phase.GetEngine());
                    param.Logger.Log("Running analysis engines...");
                    for (int i = 0; i < engines.Count; i++)
                    {
                        engines[i].Analysis(asm);
                        param.Logger.Progress((double)(i + 1) / engines.Count);
                    }


                    ConfusionParameter cParam = new ConfusionParameter();
                    bool end1 = false;
                    foreach (StructurePhase i in from i in trueMems.Keys where (i is StructurePhase) orderby (int)i.Priority + i.PhaseID * 10 ascending select i)
                    {
                        if (!end1 && i.PhaseID > 1)
                        {
                            MarkAssembly(asm);
                            foreach (ModuleDefinition mod in asm.Modules)
                            {
                                MarkObfuscateHelpers(mod, trueMems);
                                CecilHelper.RefreshTokens(mod);
                            }
                            end1 = true;
                        }

                        i.Confuser = this;
                        param.Logger.Log("Executing " + i.Confusion.Name + " Phase " + i.PhaseID + "...");

                        i.Initialize(asm);
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
                            List<object> idk = trueMems[i];
                            if (idk.Count == 0)
                                continue;

                            if (i is IProgressProvider)
                            {
                                cParam.Parameters = new NameValueCollection();
                                foreach (object mem in idk)
                                {
                                    NameValueCollection memParam = (from set in (mem as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection> where set.Key.Phases.Contains(i) select set.Value).FirstOrDefault();
                                    string hash=mem.GetHashCode().ToString("X8");
                                    foreach (string pkey in memParam.AllKeys)
                                        cParam.Parameters[hash + "_" + pkey] = memParam[pkey];
                                }
                                cParam.Target = idk;
                                (i as IProgressProvider).SetProgresser(param.Logger);
                                i.Process(cParam);
                            }
                            else
                            {
                                double total = idk.Count;
                                int interval = 1;
                                if (total > 1000)
                                    interval = (int)total / 100;
                                int now = 0;
                                foreach (object mem in idk)
                                {
                                    cParam.Parameters = (from set in (mem as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection> where set.Key.Phases.Contains(i) select set.Value).FirstOrDefault();
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

                    param.Logger.StartPhase(3);

                    MemoryStream final = new MemoryStream();
                    MetadataProcessor psr = new MetadataProcessor();
                    double total1 = (from i in trueMems.Keys where (i is AdvancedPhase) select i).Count();
                    int now1 = 1;
                    psr.PreProcess += new MetadataProcessor.Do(delegate(MetadataProcessor.MetadataAccessor accessor)
                    {
                        foreach (AdvancedPhase i in from i in trueMems.Keys where (i is AdvancedPhase) && i.PhaseID == 1 orderby i.Priority ascending select i)
                        {
                            param.Logger.Log("Executing " + i.Confusion.Name + " Phase 1...");
                            i.Process(accessor);
                            param.Logger.Progress(now1 / total1); now1++;
                        }
                    });
                    psr.DoProcess += new MetadataProcessor.Do(delegate(MetadataProcessor.MetadataAccessor accessor)
                    {
                        foreach (AdvancedPhase i in from i in trueMems.Keys where (i is AdvancedPhase) && i.PhaseID == 2 orderby i.Priority ascending select i)
                        {
                            param.Logger.Log("Executing " + i.Confusion.Name + " Phase 2...");
                            i.Process(accessor);
                            param.Logger.Progress(now1 / total1); now1++;
                        }
                    });
                    psr.PostProcess += new MetadataProcessor.Do(delegate(MetadataProcessor.MetadataAccessor accessor)
                    {
                        foreach (AdvancedPhase i in from i in trueMems.Keys where (i is AdvancedPhase) && i.PhaseID == 3 orderby i.Priority ascending select i)
                        {
                            param.Logger.Log("Executing " + i.Confusion.Name + " Phase 3...");
                            i.Process(accessor);
                            param.Logger.Progress(now1 / total1); now1++;
                        }
                    });
                    for (int i = 0; i < asm.Modules.Count; i++)
                    {
                        param.Logger.Log("Processing module(" + (i + 1) + "/" + asm.Modules.Count + ")...");
                        psr.Process(asm.MainModule, final, new WriterParameters() { StrongNameKeyPair = sn });
                        now1 = 0;
                    }

                    param.Logger.StartPhase(4);
                    if (param.CompressOutput)
                    {
                        param.Logger.Log("Compressing output assembly...");
                        Compressor.Compress(this, final.ToArray(), asm, dst);
                    }
                    else
                    {
                        byte[] arr = final.ToArray();
                        dst.Write(arr, 0, arr.Length);
                    }

                    param.Logger.Log("Assembly Saved.");
                }
                param.Logger.Finish("Ended at " + DateTime.Now.ToShortTimeString() + ".");
            }
            catch (Exception ex)
            {
                param.Logger.Fatal(ex);
            }
            finally
            {
                src.Dispose();
                dst.Dispose();
            }
        }

        public Thread ConfuseAsync(string src, string dst, ConfuserParameter param)
        {
            return ConfuseAsync(File.OpenRead(src), File.OpenWrite(dst), param);
        }
        public Thread ConfuseAsync(Stream src, Stream dst, ConfuserParameter param)
        {
            Thread thread = new Thread(delegate() { Confuse(src, dst, param); });
            thread.IsBackground = true;
            thread.Name = "Confuuusing";
            thread.Start();
            return thread;
        }

        void MarkAssembly(AssemblyDefinition asm)
        {
            TypeDefinition att = new TypeDefinition("", "ConfusedByAttribute", TypeAttributes.Class | TypeAttributes.NotPublic, asm.MainModule.Import(typeof(Attribute)));
            MethodDefinition ctor = new MethodDefinition(".ctor", MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.Public, asm.MainModule.Import(typeof(void)));
            ctor.Parameters.Add(new ParameterDefinition(asm.MainModule.Import(typeof(string))));
            ILProcessor psr = (ctor.Body = new MethodBody(ctor)).GetILProcessor();
            psr.Emit(OpCodes.Ldarg_0);
            psr.Emit(OpCodes.Call, asm.MainModule.Import(typeof(Attribute).GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null)));
            psr.Emit(OpCodes.Ret);
            att.Methods.Add(ctor);
            asm.MainModule.Types.Add(att);

            CustomAttribute ca = new CustomAttribute(ctor);
            ca.ConstructorArguments.Add(new CustomAttributeArgument(asm.MainModule.Import(typeof(string)), string.Format("Confuser v" + typeof(Confuser).Assembly.GetName().Version.ToString())));
            asm.CustomAttributes.Add(ca);
        }
        AssemblyDefinition[] ExtractAssemblies(Stream src)
        {
            return new AssemblyDefinition[] { AssemblyDefinition.ReadAssembly(src, new ReaderParameters(ReadingMode.Immediate)) };
        }
        IDictionary<IConfusion, NameValueCollection> FillAssembly(AssemblyDefinition asm, Dictionary<IConfusion, List<object>> mems)
        {
            IDictionary<IConfusion, NameValueCollection> sets = (asm as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
            if (sets != null)
                foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sets)
                    if ((cion.Key.Target & Target.Assembly) == Target.Assembly)
                        mems[cion.Key].Add(asm);
            foreach (ModuleDefinition mod in asm.Modules)
                FillModule(mod, mems);
            return (asm as IAnnotationProvider).Annotations["GlobalParams"] as IDictionary<IConfusion, NameValueCollection>;
        }
        void FillModule(ModuleDefinition mod, Dictionary<IConfusion, List<object>> mems)
        {
            foreach (TypeDefinition type in mod.Types)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (type as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sets)
                        if ((cion.Key.Target & Target.Types) == Target.Types)
                            mems[cion.Key].Add(type);
                FillType(type, mems);
            }
        }
        void FillType(TypeDefinition type, Dictionary<IConfusion, List<object>> mems)
        {
            foreach (TypeDefinition nType in type.NestedTypes)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (nType as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sets)
                        if ((cion.Key.Target & Target.Types) == Target.Types) 
                            mems[cion.Key].Add(nType);
                FillType(nType, mems);
            }
            foreach (MethodDefinition mtd in type.Methods)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (mtd as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sets)
                        if ((cion.Key.Target & Target.Methods) == Target.Methods) 
                            mems[cion.Key].Add(mtd);
            }
            foreach (FieldDefinition fld in type.Fields)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (fld as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sets)
                        if ((cion.Key.Target & Target.Fields) == Target.Fields) 
                            mems[cion.Key].Add(fld);
            }
            foreach (EventDefinition evt in type.Events)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (evt as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sets)
                        if ((cion.Key.Target & Target.Events) == Target.Events) 
                            mems[cion.Key].Add(evt);
            }
            foreach (PropertyDefinition prop in type.Properties)
            {
                IDictionary<IConfusion, NameValueCollection> sets = (prop as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
                if (sets != null)
                    foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sets)
                        if ((cion.Key.Target & Target.Properties) == Target.Properties) 
                            mems[cion.Key].Add(prop);
            }
        }
        void MarkObfuscateHelpers(ModuleDefinition mod, Dictionary<Phase, List<object>> mems)
        {
            TypeDefinition modType = mod.GetType("<Module>");
            IDictionary<IConfusion, NameValueCollection> sets = (modType as IAnnotationProvider).Annotations["ConfusionSets"] as IDictionary<IConfusion, NameValueCollection>;
            if (sets == null) return;
            Dictionary<IConfusion, NameValueCollection> sub = new Dictionary<IConfusion, NameValueCollection>();
            foreach (var i in sets)
            {
                bool ok = true;
                foreach (Phase phase in i.Key.Phases)
                    if (!(phase is AdvancedPhase) && phase.PhaseID == 1)
                    {
                        ok = false;
                        break;
                    }
                if (ok)
                    sub.Add(i.Key, i.Value);
            }
            foreach (MethodDefinition mtd in modType.Methods)
            {
                foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sub)
                    if ((cion.Key.Target & Target.Methods) == Target.Methods)
                    {
                        (mtd as IAnnotationProvider).Annotations["ConfusionSets"] = sub;
                        foreach (Phase phase in cion.Key.Phases)
                            mems[phase].Add(mtd);
                    }
            }
            foreach (FieldDefinition fld in modType.Fields)
            {
                foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sub)
                    if ((cion.Key.Target & Target.Fields) == Target.Fields)
                    {
                        (fld as IAnnotationProvider).Annotations["ConfusionSets"] = sub;
                        foreach (Phase phase in cion.Key.Phases)
                            mems[phase].Add(fld);
                    }
            }
            foreach (EventDefinition evt in modType.Events)
            {
                foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sub)
                    if ((cion.Key.Target & Target.Events) == Target.Events)
                    {
                        (evt as IAnnotationProvider).Annotations["ConfusionSets"] = sub;
                        foreach (Phase phase in cion.Key.Phases)
                            mems[phase].Add(evt);
                    }
            }
            foreach (PropertyDefinition prop in modType.Properties)
            {
                foreach (KeyValuePair<IConfusion, NameValueCollection> cion in sub)
                    if ((cion.Key.Target & Target.Properties) == Target.Properties)
                    {
                        (prop as IAnnotationProvider).Annotations["ConfusionSets"] = sub;
                        foreach (Phase phase in cion.Key.Phases)
                            mems[phase].Add(prop);
                    }
            }
        }
    }
}
