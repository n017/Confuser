using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Metadata;

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

    public class Logger
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
    }

    public class ConfuserParameter
    {
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

                AssemblyDefinition[] asms = ExtractAssemblies(src);

                Marker mkr = new Marker(param.Confusions);
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
                    FillAssembly(asm, mems);
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

                    ConfusionParameter cParam = new ConfusionParameter();
                    bool end1 = false;
                    foreach (StructurePhase i in from i in trueMems.Keys where (i is StructurePhase) orderby (int)i.Priority + i.PhaseID * 10 ascending select i)
                    {
                        if (!end1 && i.PhaseID > 1)
                        {
                            MarkAssembly(asm);
                            foreach (ModuleDefinition mod in asm.Modules)
                                CecilHelper.RefreshTokens(mod);
                            end1 = true;
                        }

                        param.Logger.Log("Executing " + i.Confusion.Name + " Phase " + i.PhaseID + "...");

                        if (i.WholeRun == true)
                        {
                            i.Initialize(asm);
                            param.Logger.Progress(1 / 3.0);
                            i.Process(null);
                            param.Logger.Progress(2 / 3.0);
                            i.DeInitialize();
                            param.Logger.Progress(3 / 3.0);
                        }
                        else
                        {
                            List<object> idk = trueMems[i];
                            if (idk.Count == 0)
                                continue;
                            double total = 1 + idk.Count;
                            int now = 1;
                            i.Initialize(asm); param.Logger.Progress(now / total); now++;
                            foreach (object mem in trueMems[i])
                            {
                                cParam.Parameters = (from set in (mem as IAnnotationProvider).Annotations["ConfusionSets"] as List<ConfusionSet> where set.Confusion.Phases.Contains(i) select set.Parameters).First();
                                cParam.Target = mem;
                                i.Process(cParam);
                                param.Logger.Progress(now / total); now++;
                            }
                            i.DeInitialize();
                            param.Logger.Progress(now / total);
                        }
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
            return new AssemblyDefinition[] { AssemblyDefinition.ReadAssembly(src) };
        }
        void FillAssembly(AssemblyDefinition asm, Dictionary<IConfusion, List<object>> mems)
        {
            List<ConfusionSet> sets = (asm as IAnnotationProvider).Annotations["ConfusionSets"] as List<ConfusionSet>;
            if (sets != null)
                foreach (ConfusionSet cion in sets)
                    mems[cion.Confusion].Add(asm);
            foreach (ModuleDefinition mod in asm.Modules)
                FillModule(mod, mems);
        }
        void FillModule(ModuleDefinition mod, Dictionary<IConfusion, List<object>> mems)
        {
            foreach (TypeDefinition type in mod.Types)
            {
                List<ConfusionSet> sets = (type as IAnnotationProvider).Annotations["ConfusionSets"] as List<ConfusionSet>;
                if (sets != null)
                    foreach (ConfusionSet cion in sets)
                        mems[cion.Confusion].Add(type);
                FillType(type, mems);
            }
        }
        void FillType(TypeDefinition type, Dictionary<IConfusion, List<object>> mems)
        {
            foreach (TypeDefinition nType in type.NestedTypes)
            {
                List<ConfusionSet> sets = (nType as IAnnotationProvider).Annotations["ConfusionSets"] as List<ConfusionSet>;
                if (sets != null)
                    foreach (ConfusionSet cion in sets)
                        mems[cion.Confusion].Add(nType);
                FillType(nType, mems);
            }
            foreach (MethodDefinition mtd in type.Methods)
            {
                List<ConfusionSet> sets = (mtd as IAnnotationProvider).Annotations["ConfusionSets"] as List<ConfusionSet>;
                if (sets != null)
                    foreach (ConfusionSet cion in sets)
                        mems[cion.Confusion].Add(mtd);
            }
            foreach (FieldDefinition fld in type.Fields)
            {
                List<ConfusionSet> sets = (fld as IAnnotationProvider).Annotations["ConfusionSets"] as List<ConfusionSet>;
                if (sets != null)
                    foreach (ConfusionSet cion in sets)
                        mems[cion.Confusion].Add(fld);
            }
            foreach (EventDefinition evt in type.Events)
            {
                List<ConfusionSet> sets = (evt as IAnnotationProvider).Annotations["ConfusionSets"] as List<ConfusionSet>;
                if (sets != null)
                    foreach (ConfusionSet cion in sets)
                        mems[cion.Confusion].Add(evt);
            }
            foreach (PropertyDefinition prop in type.Properties)
            {
                List<ConfusionSet> sets = (prop as IAnnotationProvider).Annotations["ConfusionSets"] as List<ConfusionSet>;
                if (sets != null)
                    foreach (ConfusionSet cion in sets)
                        mems[cion.Confusion].Add(prop);
            }
        }
    }
}
