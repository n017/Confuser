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
    public class LogEventArgs : EventArgs
    {
        public LogEventArgs(string message) { this.message = message; }
        string message;
        public string Message { get { return message; } }
    }
    public class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(Exception ex) { this.ex = ex; }
        Exception ex;
        public Exception Exception { get { return ex; } }
    }

    public delegate void LogEventHandler(object sender, LogEventArgs e);
    public delegate void ExceptionEventHandler(object sender, ExceptionEventArgs e);

    public class ConfusionParameter
    {
        Confusion c;
        public Confusion Confusion { get { return c; } set { c = value; } }
        IMemberDefinition[] defs;
        public IMemberDefinition[] Targets { get { return defs; } set { defs = value; } }
    }

    public class Confuser
    {
        public event LogEventHandler Log;
        public event EventHandler Finish;
        public event ExceptionEventHandler Fault;

        bool cps = false;
        public bool CompressOutput { get { return cps; } set { cps = value; } }

        internal void LogMessage(string message)
        {
            if (Log != null)
                Log(this, new LogEventArgs(message));
        }

        public void Confuse(AssemblyDefinition src, string dst, ConfusionParameter[] parameters)
        {
            Confuse(src, File.OpenWrite(dst), parameters);
        }
        public void Confuse(AssemblyDefinition src, Stream dst, ConfusionParameter[] parameters)
        {
            try
            {
                LogMessage("Started at " + DateTime.Now.ToShortTimeString() + ".");
                LogMessage("");

                LogMessage("Loading assembly reference...");
                foreach (AssemblyNameReference r in src.MainModule.AssemblyReferences)
                {
                    GlobalAssemblyResolver.Instance.Resolve(r);
                    LogMessage(">" + r.FullName);
                }

                //global cctor which used in many confusion
                MethodDefinition cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static, src.MainModule.Import(typeof(void)));
                cctor.Body = new MethodBody(cctor);
                cctor.Body.GetILProcessor().Emit(OpCodes.Ret);
                src.MainModule.GetType("<Module>").Methods.Add(cctor);

                Dictionary<Confusion, IMemberDefinition[]> cs = new Dictionary<Confusion, IMemberDefinition[]>();
                foreach (ConfusionParameter para in parameters)
                    cs.Add(para.Confusion, para.Targets);

                foreach (Confusion i in from i in cs.Keys where (i is StructureConfusion) && ((i.Phases & Phases.Phase1) == Phases.Phase1) orderby i.Priority ascending select i)
                {
                    LogMessage("Executing " + i.Name + " Phase 1...");
                    (i as StructureConfusion).Confuse(1, this, src, cs[i]);
                }
                MarkAssembly(src);
                CecilHelper.RefreshTokens(src.MainModule);
                foreach (Confusion i in from i in cs.Keys where (i is StructureConfusion) && ((i.Phases & Phases.Phase2) == Phases.Phase2) orderby i.Priority ascending select i)
                {
                    LogMessage("Executing " + i.Name + " Phase 2...");
                    (i as StructureConfusion).Confuse(2, this, src, cs[i]);
                }
                foreach (Confusion i in from i in cs.Keys where (i is StructureConfusion) && ((i.Phases & Phases.Phase3) == Phases.Phase3) orderby i.Priority ascending select i)
                {
                    LogMessage("Executing " + i.Name + " Phase 3...");
                    (i as StructureConfusion).Confuse(3, this, src, cs[i]);
                }

                LogMessage("");

                MemoryStream final = new MemoryStream();
                MetadataProcessor psr = new MetadataProcessor();
                psr.PreProcess += new MetadataProcessor.Do(delegate(MetadataProcessor.MetadataAccessor accessor)
                {
                    foreach (Confusion i in from i in cs.Keys where (i is AdvancedConfusion) && ((i.Phases & Phases.Phase1) == Phases.Phase1) orderby i.Priority ascending select i)
                    {
                        LogMessage("Executing " + i.Name + " Phase 1...");
                        (i as AdvancedConfusion).Confuse(1, this, accessor);
                    }
                });
                psr.DoProcess += new MetadataProcessor.Do(delegate(MetadataProcessor.MetadataAccessor accessor)
                {
                    foreach (Confusion i in from i in cs.Keys where (i is AdvancedConfusion) && ((i.Phases & Phases.Phase2) == Phases.Phase2) orderby i.Priority ascending select i)
                    {
                        LogMessage("Executing " + i.Name + " Phase 2...");
                        (i as AdvancedConfusion).Confuse(2, this, accessor);
                    }
                });
                psr.PostProcess += new MetadataProcessor.Do(delegate(MetadataProcessor.MetadataAccessor accessor)
                {
                    foreach (Confusion i in from i in cs.Keys where (i is AdvancedConfusion) && ((i.Phases & Phases.Phase3) == Phases.Phase3) orderby i.Priority ascending select i)
                    {
                        LogMessage("Executing " + i.Name + " Phase 3...");
                        (i as AdvancedConfusion).Confuse(3, this, accessor);
                    }
                });
                psr.Process(src.MainModule, final);

                LogMessage("");
                if (cps)
                {
                    LogMessage("Compressing output assembly...");
                    Compressor.Compress(this, final.ToArray(), src, dst);
                }
                else
                {
                    byte[] arr = final.ToArray();
                    dst.Write(arr, 0, arr.Length);
                }

                LogMessage("Assembly Saved.");
            }
            catch (Exception ex)
            {
                LogMessage("Fault Exception occured:");
                LogMessage(ex.ToString());
                if (Fault != null)
                    Fault(this, new ExceptionEventArgs(ex));
            }

            LogMessage("");
            LogMessage("Ended at " + DateTime.Now.ToShortTimeString() + ".");
            if (Finish != null)
                Finish(this, new EventArgs());
        }

        public void ConfuseAsync(AssemblyDefinition src, string dst, ConfusionParameter[] parameters)
        {
            ConfuseAsync(src, File.OpenWrite(dst), parameters);
        }
        public void ConfuseAsync(AssemblyDefinition src, Stream dst, ConfusionParameter[] parameters)
        {
            ThreadPool.QueueUserWorkItem(delegate { Confuse(src, dst, parameters); });
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
            ca.ConstructorArguments.Add(new CustomAttributeArgument(asm.MainModule.Import(typeof(string)),string.Format("Confuser v" + typeof(Confuser).Assembly.GetName().Version.ToString())));
            asm.CustomAttributes.Add(ca);
        }
    }
}
