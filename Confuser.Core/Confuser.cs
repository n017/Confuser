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
using Confuser.Core.Project;

namespace Confuser.Core
{
    public class AssemblyEventArgs : EventArgs
    {
        public AssemblyEventArgs(AssemblyDefinition asmDef) { this.Assembly = asmDef; }
        public AssemblyDefinition Assembly { get; private set; }
    }
    public class LogEventArgs : EventArgs
    {
        public LogEventArgs(string msg) { this.Message = msg; }
        public string Message { get; private set; }
    }
    public class ProgressEventArgs : EventArgs
    {
        public ProgressEventArgs(int progress, int overall)
        {
            this.Progress = progress;
            this.Total = overall;
        }
        public int Progress { get; private set; }
        public int Total { get; private set; }
    }
    public class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(Exception ex) { this.Exception = ex; }
        public Exception Exception { get; private set; }
    }

    public class Logger : IProgresser
    {
        public event EventHandler<LogEventArgs> Phase;
        public event EventHandler<AssemblyEventArgs> BeginAssembly;
        public event EventHandler<AssemblyEventArgs> EndAssembly;
        public event EventHandler<LogEventArgs> Log;
        public event EventHandler<ProgressEventArgs> Progress;
        public event EventHandler<ExceptionEventArgs> Fault;
        public event EventHandler<LogEventArgs> End;

        class Asm : IDisposable
        {
            public AssemblyDefinition asmDef;
            public Logger logger;
            public void Dispose()
            {
                if (logger.EndAssembly != null)
                    logger.EndAssembly(logger, new AssemblyEventArgs(asmDef));
            }
        }

        internal void _BeginPhase(string phase)
        {
            if (Phase != null)
                Phase(this, new LogEventArgs(phase));
        }
        internal IDisposable _Assembly(AssemblyDefinition asmDef)
        {
            if (BeginAssembly != null)
                BeginAssembly(this, new AssemblyEventArgs(asmDef));
            return new Asm() { asmDef = asmDef, logger = this };
        }
        public void _Log(string message)
        {
            if (Log != null)
                Log(this, new LogEventArgs(message));
        }
        public void _Progress(int progress, int overall)
        {
            if (Progress != null)
                Progress(this, new ProgressEventArgs(progress, overall));
        }
        internal void _Fatal(Exception ex)
        {
            if (Fault != null)
                Fault(this, new ExceptionEventArgs(ex));
            else
                throw ex;
        }
        internal void _Finish(string message)
        {
            if (End != null)
                End(this, new LogEventArgs(message));
        }


        void IProgresser.SetProgress(int progress, int overall)
        {
            _Progress(progress, overall);
        }
    }
    public interface IProgresser
    {
        void SetProgress(int progress, int overall);
    }
    public interface IProgressProvider
    {
        void SetProgresser(IProgresser progresser);
    }

    public class ConfuserParameter
    {
        public ConfuserProject Project { get; set; }

        Logger log = new Logger();
        public Logger Logger { get { return log; } }

        Marker mkr = new Marker();
        public Marker Marker { get { return mkr; } set { mkr = value; } }

        internal MetadataProcessor.MetadataProcess ProcessMetadata;
        internal MetadataProcessor.ImageProcess ProcessImage;
    }

    public struct Tuple<T1, T2>
    {
        public Tuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }
        public T1 Item1;
        public T2 Item2;
    }

    public struct Tuple<T1, T2, T3>
    {
        public Tuple(T1 item1, T2 item2, T3 item3)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
        }
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;
    }

    public class Confuser
    {
        internal ConfuserParameter param;
        internal List<AssemblySetting> settings;
        internal MarkerSetting mkrSettings;
        internal List<Analyzer> analyzers;
        internal List<IConfusion> confusions;
        internal List<Packer> packers;
        internal void Log(string message) { param.Logger._Log(message); }

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
                param.Logger._BeginPhase("Initializing...");
                Log("Started at " + DateTime.Now.ToShortTimeString() + ".");
                Log("Loading...");

                System.Reflection.StrongNameKeyPair sn;
                Initialize(out sn);

                List<Phase> phases = new List<Phase>();
                foreach (IConfusion cion in confusions)
                    foreach (Phase phase in cion.Phases)
                        phases.Add(phase);

                param.Logger._BeginPhase("Obfuscating Phase 1...");
                List<byte[]> pes = new List<byte[]>();
                List<ModuleDefinition> mods = new List<ModuleDefinition>();

                for (int i = 0; i < settings.Count; i++)
                {
                    using (param.Logger._Assembly(settings[i].Assembly))
                    {
                        Log(string.Format("Obfuscating assembly {0}...", settings[i].Assembly.FullName));

                        foreach (ModuleSetting mod in settings[i].Modules)
                        {
                            Log(string.Format("Obfuscating structure of module {0}...", mod.Module.Name));

                            helpers.Clear();
                            helpers.Add(mod.Module.GetType("<Module>").GetStaticConstructor(), HelperAttribute.NoEncrypt);

                            ProcessStructuralPhases(mod, settings[i].GlobalParameters, phases);
                        }
                    }
                }

                param.Logger._BeginPhase("Obfuscating Phase 2...");
                foreach (var i in settings)
                    using (param.Logger._Assembly(i.Assembly))
                        foreach (var j in i.Modules)
                        {
                            MemoryStream final = new MemoryStream();
                            ProcessMdPePhases(j, i.GlobalParameters, phases, final, new WriterParameters() { StrongNameKeyPair = (j.Module.Attributes & ModuleAttributes.StrongNameSigned) != 0 ? sn : null });

                            pes.Add(final.ToArray());
                            mods.Add(j.Module);
                        }

                Finalize(mods.ToArray(), pes.ToArray());

                param.Logger._Finish("Ended at " + DateTime.Now.ToShortTimeString() + ".");
            }
            catch (Exception ex)
            {
                param.Logger._Fatal(ex);
            }
            finally
            {
                GlobalAssemblyResolver.Instance.AssemblyCache.Clear();
                GC.Collect();
            }
        }

        void LoadAssembly(System.Reflection.Assembly asm)
        {
            foreach (Type type in asm.GetTypes())
            {
                if (typeof(IConfusion).IsAssignableFrom(type) && type != typeof(IConfusion))
                    confusions.Add(Activator.CreateInstance(type) as IConfusion);
                if (typeof(Packer).IsAssignableFrom(type) && type != typeof(Packer))
                    packers.Add(Activator.CreateInstance(type) as Packer);
            }
        }

        void UpdateCustomAttributeRef(ICustomAttributeProvider ca)
        {
            if (!ca.HasCustomAttributes) return;
            foreach (var i in ca.CustomAttributes)
            {
                foreach (var arg in i.ConstructorArguments)
                    UpdateCustomAttributeArgs(arg);
                foreach (var arg in i.Fields)
                    UpdateCustomAttributeArgs(arg.Argument);
                foreach (var arg in i.Properties)
                    UpdateCustomAttributeArgs(arg.Argument);
            }
        }
        void UpdateCustomAttributeArgs(CustomAttributeArgument arg)
        {
            if (arg.Value is TypeReference)
            {
                TypeReference typeRef = arg.Value as TypeReference;
                if (typeRef.Scope is AssemblyNameReference)
                {
                    AssemblyNameReference nameRef = typeRef.Scope as AssemblyNameReference;
                    foreach (var i in settings)
                        if (i.Assembly.Name.Name == nameRef.Name)
                            typeRef.Scope = i.Assembly.Name;
                }
            }
            else if (arg.Value is CustomAttributeArgument[])
                foreach (var i in arg.Value as CustomAttributeArgument[])
                    UpdateCustomAttributeArgs(i);
        }
        void UpdateAssemblyReference(TypeDefinition typeDef, string from, string to)
        {
            UpdateCustomAttributeRef(typeDef);
            if (typeDef.HasGenericParameters)
                foreach (var p in typeDef.GenericParameters)
                    UpdateCustomAttributeRef(p);
            foreach (var i in typeDef.Methods)
            {
                if (i.HasParameters)
                    foreach (var p in i.Parameters)
                        UpdateCustomAttributeRef(p);
                if (i.HasGenericParameters)
                    foreach (var p in i.GenericParameters)
                        UpdateCustomAttributeRef(p);
                UpdateCustomAttributeRef(i.MethodReturnType);
                UpdateCustomAttributeRef(i);
            }
            foreach (var i in typeDef.Fields)
                UpdateCustomAttributeRef(i);
            foreach (var i in typeDef.Properties)
                UpdateCustomAttributeRef(i);
            foreach (var i in typeDef.Events)
                UpdateCustomAttributeRef(i);

            foreach (var i in typeDef.NestedTypes)
                UpdateAssemblyReference(i, from, to);
            foreach (var i in typeDef.Methods)
            {
                if (!i.HasBody) continue;
                foreach (var inst in i.Body.Instructions)
                {
                    if (inst.Operand is string)
                    {
                        string op = (string)inst.Operand;
                        if (op.Contains(from))
                            op = op.Replace(from, to);
                        inst.Operand = op;
                    }
                }
            }
        }
        string ToString(byte[] arr)
        {
            if (arr == null || arr.Length == 0) return "null";
            return BitConverter.ToString(arr).Replace("-", "").ToLower();
        }

        void Initialize(out System.Reflection.StrongNameKeyPair sn)
        {
            sn = null;
            if (string.IsNullOrEmpty(param.Project.SNKeyPath))
                Log("Strong name key not specified.");
            else if (!File.Exists(param.Project.SNKeyPath))
                Log("Strong name key not found. Output assembly will not be signed.");
            else
                sn = new System.Reflection.StrongNameKeyPair(new FileStream(param.Project.SNKeyPath, FileMode.Open));

            Marker mkr = param.Marker;

            GlobalAssemblyResolver.Instance.AssemblyCache.Clear();
            GlobalAssemblyResolver.Instance.ClearSearchDirectories();
            foreach (var i in param.Project)
                GlobalAssemblyResolver.Instance.AddSearchDirectory(Path.GetDirectoryName(i.Path));

            confusions = new List<IConfusion>();
            packers = new List<Packer>();
            LoadAssembly(typeof(Confuser).Assembly);
            foreach (var i in param.Project.Plugins)
                LoadAssembly(System.Reflection.Assembly.LoadFile(i));

            Log(string.Format("Loading assemblies..."));
            mkr.Initalize(confusions, packers);
            mkrSettings = mkr.MarkAssemblies(this, (sender, e) => Log(e.Message));
            settings = mkrSettings.Assemblies.ToList();

            if (mkrSettings.Packer != null && (
                settings[0].Assembly.MainModule.Kind == ModuleKind.Dll ||
                settings[0].Assembly.MainModule.Kind == ModuleKind.NetModule))
            {
                Log("Warning: Cannot pack a library or net module!");
                mkrSettings.Packer = null;
            }

            Dictionary<string, string> repl = new Dictionary<string, string>();
            foreach (var i in settings)
            {
                AssemblyDefinition asm = i.Assembly;
                string o1 = ToString(asm.Name.PublicKeyToken);
                string o2 = ToString(asm.Name.PublicKey);
                if (sn != null)
                {
                    asm.Name.PublicKey = sn.PublicKey;
                    asm.MainModule.Attributes |= ModuleAttributes.StrongNameSigned;
                }
                else
                {
                    asm.Name.PublicKey = null;
                    asm.MainModule.Attributes &= ~ModuleAttributes.StrongNameSigned;
                }
                string n1 = ToString(asm.Name.PublicKeyToken);
                string n2 = ToString(asm.Name.PublicKey);
                if (o1 != n1 && !repl.ContainsKey(o1))
                    repl.Add(o1, n1);
                if (o2 != n2 && !repl.ContainsKey(o2))
                    repl.Add(o2, n2);
            }

            foreach (var asm in settings)
                foreach (var mod in asm.Assembly.Modules)
                {
                    //global cctor which used in many confusion
                    if (mod.GetType("<Module>").GetStaticConstructor() == null)
                    {
                        MethodDefinition cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.HideBySig |
                            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
                            MethodAttributes.Static, mod.TypeSystem.Void);
                        cctor.Body = new MethodBody(cctor);
                        cctor.Body.GetILProcessor().Emit(OpCodes.Ret);
                        mod.GetType("<Module>").Methods.Add(cctor);
                    }
                    else
                    {
                        MethodDefinition cctor = mod.GetType("<Module>").GetStaticConstructor();
                        ((IAnnotationProvider)cctor).Annotations.Clear();
                    }

                    for (int i = 0; i < mod.AssemblyReferences.Count; i++)
                    {
                        AssemblyNameReference nameRef = mod.AssemblyReferences[i];
                        foreach (var asmRef in settings)
                            if (asmRef.Assembly.Name.Name == nameRef.Name)
                            {
                                nameRef = asmRef.Assembly.Name;
                                break;
                            }
                        mod.AssemblyReferences[i] = nameRef;
                    }
                }

            foreach (var i in repl)
            {
                foreach (var j in settings)
                {
                    UpdateCustomAttributeRef(j.Assembly);
                    foreach (var k in j.Assembly.Modules)
                    {
                        UpdateCustomAttributeRef(k);
                        foreach (var l in k.Types)
                            UpdateAssemblyReference(l, i.Key, i.Value);
                    }
                }
            }

            foreach (var i in settings)
                GlobalAssemblyResolver.Instance.AssemblyCache.Add(i.Assembly.FullName, i.Assembly);
            helpers = new Dictionary<IMemberDefinition, HelperAttribute>();

            Log(string.Format("Analyzing assemblies..."));
            analyzers = new List<Analyzer>();
            Dictionary<Analyzer, string> aPhases = new Dictionary<Analyzer, string>();
            foreach (IConfusion cion in confusions)
                foreach (Phase phase in cion.Phases)
                {
                    Analyzer analyzer = phase.GetAnalyzer();
                    if (analyzer != null)
                    {
                        analyzers.Add(analyzer);
                        aPhases.Add(analyzer, cion.Name);
                        analyzer.SetLogger(param.Logger);
                        analyzer.SetProgresser(param.Logger);
                    }
                }
            foreach (var i in analyzers)
            {
                Log(string.Format("Analyzing {0}...", aPhases[i]));
                i.Analyze(settings.Select(_ => _.Assembly));
            }
        }
        void ProcessStructuralPhases(ModuleSetting mod, ObfuscationSettings globalParams, IEnumerable<Phase> phases)
        {
            if (mkrSettings.Packer != null)
                mkrSettings.Packer.ProcessModulePhase1(mod.Module,
                    mod.Module.IsMain && mkrSettings.Assemblies[0].Assembly == mod.Module.Assembly);

            ConfusionParameter cParam = new ConfusionParameter();
            bool end1 = false;
            foreach (StructurePhase i in from i in phases where (i is StructurePhase) orderby (int)i.Priority + i.PhaseID * 10 ascending select i)
            {
                if (!end1 && i.PhaseID > 1)
                {
                    MarkModule(mod.Module);
                    MarkObfuscateHelpers(mod);
                    CecilHelper.RefreshTokens(mod.Module);
                    end1 = true;
                }
                var mems = GetTargets(mod, i.Confusion);
                if (mems.Count == 0) continue;

                i.Confuser = this;
                Log("Executing " + i.Confusion.Name + " Phase " + i.PhaseID + "...");

                i.Initialize(mod.Module);
                if (globalParams.ContainsKey(i.Confusion))
                    cParam.GlobalParameters = globalParams[i.Confusion];
                else
                    cParam.GlobalParameters = new NameValueCollection();

                int total = mems.Count;
                if (i.WholeRun == true)
                {
                    cParam.Parameters = null;
                    cParam.Target = null;
                    i.Process(cParam);
                    param.Logger._Progress(total, total);
                }
                else
                {
                    if (i is IProgressProvider)
                    {
                        cParam.Parameters = null;
                        cParam.Target = mems;
                        (i as IProgressProvider).SetProgresser(param.Logger);
                        i.Process(cParam);
                    }
                    else
                    {
                        int interval = 1;
                        if (total > 1000)
                            interval = (int)total / 100;
                        int now = 0;
                        foreach (var mem in mems)
                        {
                            cParam.Parameters = mem.Item2;
                            cParam.Target = mem.Item1;
                            i.Process(cParam);
                            if (now % interval == 0 || now == total - 1)
                                param.Logger._Progress(now + 1, total);
                            now++;
                        }
                    }
                    param.Logger._Progress(total, total);
                }
                i.DeInitialize();
            }

            if (mkrSettings.Packer != null)
                mkrSettings.Packer.ProcessModulePhase3(mod.Module,
                    mod.Module.IsMain && mkrSettings.Assemblies[0].Assembly == mod.Module.Assembly);
        }
        void ProcessMdPePhases(ModuleSetting mod, ObfuscationSettings globalParams, IEnumerable<Phase> phases, Stream stream, WriterParameters parameters)
        {
            MetadataProcessor psr = new MetadataProcessor();
            int total1 = (from i in phases where (i is MetadataPhase) select i).Count();
            int now1 = 1;
            psr.BeforeBuildModule += new MetadataProcessor.MetadataProcess(delegate(MetadataProcessor.MetadataAccessor accessor)
            {
                foreach (MetadataPhase i in from i in phases where (i is MetadataPhase) && i.PhaseID == 1 orderby i.Priority ascending select i)
                {
                    if (GetTargets(mod, i.Confusion).Count == 0) continue;
                    Log("Executing " + i.Confusion.Name + " Phase 1...");
                    i.Confuser = this;
                    NameValueCollection globalParam;
                    if (globalParams.ContainsKey(i.Confusion))
                        globalParam = globalParams[i.Confusion];
                    else
                        globalParam = new NameValueCollection();
                    i.Process(globalParam, accessor);
                    param.Logger._Progress(now1, total1); now1++;
                }

                if (mkrSettings.Packer != null)
                    mkrSettings.Packer.ProcessMetadataPhase1(accessor,
                        mod.Module.IsMain && mkrSettings.Assemblies[0].Assembly == mod.Module.Assembly);
            });
            psr.BeforeWriteTables += new MetadataProcessor.MetadataProcess(delegate(MetadataProcessor.MetadataAccessor accessor)
            {
                foreach (MetadataPhase i in from i in phases where (i is MetadataPhase) && i.PhaseID == 2 orderby i.Priority ascending select i)
                {
                    if (GetTargets(mod, i.Confusion).Count == 0) continue;
                    Log("Executing " + i.Confusion.Name + " Phase 2...");
                    i.Confuser = this;
                    NameValueCollection globalParam;
                    if (globalParams.ContainsKey(i.Confusion))
                        globalParam = globalParams[i.Confusion];
                    else
                        globalParam = new NameValueCollection();
                    i.Process(globalParam, accessor);
                    param.Logger._Progress(now1, total1); now1++;
                }

                if (mkrSettings.Packer != null)
                    mkrSettings.Packer.ProcessMetadataPhase2(accessor,
                        mod.Module.IsMain && mkrSettings.Assemblies[0].Assembly == mod.Module.Assembly);
                if (param.ProcessMetadata != null)
                    param.ProcessMetadata(accessor);
            });
            psr.AfterWriteTables += new MetadataProcessor.MetadataProcess(delegate(MetadataProcessor.MetadataAccessor accessor)
            {
                foreach (MetadataPhase i in from i in phases where (i is MetadataPhase) && i.PhaseID == 3 orderby i.Priority ascending select i)
                {
                    if (GetTargets(mod, i.Confusion).Count == 0) continue;
                    Log("Executing " + i.Confusion.Name + " Phase 3...");
                    i.Confuser = this;
                    NameValueCollection globalParam;
                    if (globalParams.ContainsKey(i.Confusion))
                        globalParam = globalParams[i.Confusion];
                    else
                        globalParam = new NameValueCollection();
                    i.Process(globalParam, accessor);
                    param.Logger._Progress(now1, total1); now1++;
                }
            });
            psr.ProcessImage += new MetadataProcessor.ImageProcess(delegate(MetadataProcessor.ImageAccessor accessor)
            {
                Log(string.Format("Obfuscating Image of module {0}...", mod.Module.Name));

                if (mkrSettings.Packer != null)
                    mkrSettings.Packer.ProcessImage(accessor,
                        mod.Module.IsMain && mkrSettings.Assemblies[0].Assembly == mod.Module.Assembly);
                if (param.ProcessImage != null)
                    param.ProcessImage(accessor);

                ImagePhase[] imgPhases = (from i in phases where (i is ImagePhase) orderby (int)i.Priority + i.PhaseID * 10 ascending select (ImagePhase)i).ToArray();
                for (int i = 0; i < imgPhases.Length; i++)
                {
                    if (GetTargets(mod, imgPhases[i].Confusion).Count == 0) continue;
                    Log("Executing " + imgPhases[i].Confusion.Name + " Phase " + imgPhases[i].PhaseID + "...");
                    imgPhases[i].Confuser = this;
                    NameValueCollection globalParam;
                    if (globalParams.ContainsKey(imgPhases[i].Confusion))
                        globalParam = globalParams[imgPhases[i].Confusion];
                    else
                        globalParam = new NameValueCollection();
                    imgPhases[i].Process(globalParam, accessor);
                    param.Logger._Progress(i, imgPhases.Length);
                }
            });
            psr.ProcessPe += new MetadataProcessor.PeProcess(delegate(Stream str, MetadataProcessor.ImageAccessor accessor)
            {
                Log(string.Format("Obfuscating PE of module {0}...", mod.Module.Name));
                PePhase[] pePhases = (from i in phases where (i is PePhase) orderby (int)i.Priority + i.PhaseID * 10 ascending select (PePhase)i).ToArray();
                for (int i = 0; i < pePhases.Length; i++)
                {
                    if (GetTargets(mod, pePhases[i].Confusion).Count == 0) continue;
                    Log("Executing " + pePhases[i].Confusion.Name + " Phase " + pePhases[i].PhaseID + "...");
                    pePhases[i].Confuser = this;
                    NameValueCollection globalParam;
                    if (globalParams.ContainsKey(pePhases[i].Confusion))
                        globalParam = globalParams[pePhases[i].Confusion];
                    else
                        globalParam = new NameValueCollection();
                    pePhases[i].Process(globalParam, str, accessor);
                    param.Logger._Progress(i, pePhases.Length);
                }
            });
            Log(string.Format("Obfuscating metadata of module {0}...", mod.Module.Name));
            psr.Process(mod.Module, stream, parameters);
        }
        void Finalize(ModuleDefinition[] mods, byte[][] pes)
        {
            param.Logger._BeginPhase("Finalizing...");
            Packer packer = mkrSettings.Packer;
            if (packer != null)
            {
                if (!Directory.Exists(param.Project.OutputPath))
                    Directory.CreateDirectory(param.Project.OutputPath);

                Log("Packing output assemblies...");
                packer.Confuser = this;
                PackerParameter pParam = new PackerParameter();
                pParam.Assemblies = settings.ToArray();
                pParam.Modules = mods; pParam.PEs = pes;
                pParam.Parameters = mkrSettings.PackerParameters;
                string[] final = packer.Pack(param, pParam);
                for (int i = 0; i < final.Length; i++)
                {
                    string path = Path.Combine(param.Project.OutputPath, Path.GetFileName(final[i]));
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(final[i], Path.Combine(param.Project.OutputPath, Path.GetFileName(final[i])));
                }
            }
            else
            {
                Log("Writing outputs...");
                for (int i = 0; i < pes.Length; i++)
                {
                    string filename = Path.GetFileName(mods[i].FullyQualifiedName);
                    if (string.IsNullOrEmpty(filename)) filename = mods[i].Name;

                    string dest = Path.Combine(param.Project.OutputPath, Path.GetFileName(mods[i].FullyQualifiedName));
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
            MethodDefinition ctor = new MethodDefinition(".ctor", MethodAttributes.RTSpecialName | MethodAttributes.SpecialName | MethodAttributes.Public, mod.TypeSystem.Void);
            ctor.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
            ILProcessor psr = (ctor.Body = new MethodBody(ctor)).GetILProcessor();
            psr.Emit(OpCodes.Ldarg_0);
            psr.Emit(OpCodes.Call, mod.Import(typeof(Attribute).GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, Type.EmptyTypes, null)));
            psr.Emit(OpCodes.Ret);
            att.Methods.Add(ctor);
            mod.Types.Add(att);

            CustomAttribute ca = new CustomAttribute(ctor);
            ca.ConstructorArguments.Add(new CustomAttributeArgument(mod.TypeSystem.String, string.Format("Confuser v" + typeof(Confuser).Assembly.GetName().Version.ToString())));
            mod.CustomAttributes.Add(ca);
        }

        List<MemberSetting> newAdded = new List<MemberSetting>();
        List<Tuple<IAnnotationProvider, NameValueCollection>> GetTargets(ModuleSetting mod, IConfusion cion)
        {
            List<Tuple<IAnnotationProvider, NameValueCollection>> mems = new List<Tuple<IAnnotationProvider, NameValueCollection>>();
            if (mod.Parameters.ContainsKey(cion) && (cion.Target & Target.Module) != 0)
            {
                mems.Add(new Tuple<IAnnotationProvider, NameValueCollection>(mod.Module, mod.Parameters[cion]));
            }
            foreach (MemberSetting _mem in mod.Members)
                GetTargets(_mem, mems, cion);
            foreach (MemberSetting _mem in newAdded)
                GetTargets(_mem, mems, cion);
            return mems;
        }
        void GetTargets(MemberSetting mem, List<Tuple<IAnnotationProvider, NameValueCollection>> mems, IConfusion cion)
        {
            if (mem.Parameters.ContainsKey(cion))
            {
                if (mem.Object is TypeDefinition && (cion.Target & Target.Types) != 0)
                    mems.Add(new Tuple<IAnnotationProvider, NameValueCollection>(mem.Object, mem.Parameters[cion]));
                else if (mem.Object is MethodDefinition && (cion.Target & Target.Methods) != 0)
                    mems.Add(new Tuple<IAnnotationProvider, NameValueCollection>(mem.Object, mem.Parameters[cion]));
                else if (mem.Object is FieldDefinition && (cion.Target & Target.Fields) != 0)
                    mems.Add(new Tuple<IAnnotationProvider, NameValueCollection>(mem.Object, mem.Parameters[cion]));
                else if (mem.Object is PropertyDefinition && (cion.Target & Target.Properties) != 0)
                    mems.Add(new Tuple<IAnnotationProvider, NameValueCollection>(mem.Object, mem.Parameters[cion]));
                else if (mem.Object is EventDefinition && (cion.Target & Target.Events) != 0)
                    mems.Add(new Tuple<IAnnotationProvider, NameValueCollection>(mem.Object, mem.Parameters[cion]));
            }
            foreach (MemberSetting _mem in mem.Members)
                GetTargets(_mem, mems, cion);
        }
        MemberSetting? GetSetting(IMemberDefinition mem)
        {
            foreach (MemberSetting _mem in newAdded)
            {
                if (_mem.Object == mem) return _mem;
                var r = GetSetting(_mem, mem);
                if (r != null) return r.Value;
            }
            foreach (var i in settings)
                foreach (var j in i.Modules)
                {
                    if (j.Module != ((MemberReference)mem).Module) continue;
                    TypeDefinition root = mem is TypeDefinition ? mem as TypeDefinition : mem.DeclaringType;
                    while (root.DeclaringType != null) root = root.DeclaringType;
                    foreach (var k in j.Members)
                    {
                        if (k.Object == root)
                            return GetSetting(k, mem);
                    }
                    return null;
                }
            return null;
        }
        MemberSetting? GetSetting(MemberSetting set, IMemberDefinition mem)
        {
            MemberSetting? ret = null;
            foreach (var i in set.Members)
            {
                if (i.Object == mem) return i;
                else
                {
                    ret = GetSetting(i, mem);
                    if (ret != null) return ret;
                }
            }
            return ret;
        }

        internal Dictionary<IMemberDefinition, HelperAttribute> helpers;
        void MarkObfuscateHelpers(ModuleSetting mod)
        {
            if (mod.Members.Length == 0) return;
            ObfuscationSettings sets = mod.Members[0].Parameters;
            if (sets == null) return;
            ObfuscationSettings sub = new ObfuscationSettings();
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
                if (GetSetting(def.Key) != null) continue;

                ObfuscationSettings n = new ObfuscationSettings();

                Target target = 0;
                if (def.Key is TypeDefinition) target = Target.Types;
                else if (def.Key is MethodDefinition) target = Target.Methods;
                else if (def.Key is FieldDefinition) target = Target.Fields;
                else if (def.Key is EventDefinition) target = Target.Events;
                else if (def.Key is PropertyDefinition) target = Target.Properties;
                foreach (var s in sub)
                {
                    if ((s.Key.Target & target) == 0 || (s.Key.Behaviour & (Behaviour)def.Value) != 0) continue;
                    n.Add(s.Key, s.Value);
                }

                newAdded.Add(new MemberSetting(def.Key) { Parameters = n, Members = new MemberSetting[0] });
            }
        }
    }
}
