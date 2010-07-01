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
    public class LoggingEventArgs : EventArgs
    {
        public LoggingEventArgs(string m) { this.m = m; }
        string m;
        public string Message { get { return m; } }
    }
    public class ExceptionEventArgs : EventArgs
    {
        public ExceptionEventArgs(Exception ex) { this.ex = ex; }
        Exception ex;
        public Exception Exception { get { return ex; } }
    }

    public delegate void LoggingEventHandler(object sender, LoggingEventArgs e);
    public delegate void ExceptionEventHandler(object sender, ExceptionEventArgs e);

    public class Confuser
    {
        public event LoggingEventHandler Logging;
        public event LoggingEventHandler ScreenLogging;
        public event EventHandler Finish;
        public event ExceptionEventHandler Fault;

        ConfusionCollection c = new ConfusionCollection();
        public ConfusionCollection Confusions { get { return c; } }

        bool cps = false;
        public bool CompressOutput { get { return cps; } set { cps = value; } }

        int lv;
        internal void Log(string mess)
        {
            if (Logging != null)
                Logging(this, new LoggingEventArgs(new string(' ', lv * 4) + mess));
        }
        internal void ScreenLog(string mess)
        {
            if (Logging != null)
                Logging(this, new LoggingEventArgs(new string(' ', lv * 4) + mess));
            if (ScreenLogging != null)
                ScreenLogging(this, new LoggingEventArgs(new string(' ', lv * 4) + mess));
        }
        internal void AddLv() { lv++; }
        internal void SubLv() { lv--; }

        public void Confuse(string src, string dst)
        {
            lv = 0;
            try
            {
                ScreenLog("<start time='" + DateTime.Now.ToShortTimeString() + "'/>");
                lv = 1;

                AssemblyDefinition asm = AssemblyDefinition.ReadAssembly(src, new ReaderParameters() { ReadingMode = ReadingMode.Immediate });

                ScreenLog("<asmrefs>");
                lv = 2;
                foreach (AssemblyNameReference r in asm.MainModule.AssemblyReferences)
                {
                    GlobalAssemblyResolver.Instance.Resolve(r);
                    ScreenLog("<asmref name='" + r.FullName + "'/>");
                }
                lv = 1;
                ScreenLog("</asmrefs>");

                //global cctor which used in many confusion
                MethodDefinition cctor = new MethodDefinition(".cctor", MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.Static, asm.MainModule.Import(typeof(void)));
                cctor.Body = new MethodBody(cctor);
                cctor.Body.GetILProcessor().Emit(OpCodes.Ret);
                asm.MainModule.GetType("<Module>").Methods.Add(cctor);

                ScreenLog("<init/>");

                foreach (Confusion i in from i in c where (i is StructureConfusion) && ((i.Process & ProcessType.Pre) == ProcessType.Pre) orderby i.Priority ascending select i)
                {
                    c.ExecutePreConfusion(i as StructureConfusion, this, asm);
                }
                MarkAssembly(asm);
                CecilHelper.RefreshTokens(asm.MainModule);
                foreach (Confusion i in from i in c where (i is StructureConfusion) && ((i.Process & ProcessType.Real) == ProcessType.Real) orderby i.Priority ascending select i)
                {
                    c.ExecuteConfusion(i as StructureConfusion, this, asm);
                }
                foreach (Confusion i in from i in c where (i is StructureConfusion) && ((i.Process & ProcessType.Post) == ProcessType.Post) orderby i.Priority ascending select i)
                {
                    c.ExecutePostConfusion(i as StructureConfusion, this, asm);
                }

                MemoryStream final = new MemoryStream();
                MetadataProcessor psr = new MetadataProcessor();
                psr.PreProcess += new MetadataProcessor.Do(delegate (MetadataProcessor.MetadataAccessor accessor)
                {
                    foreach (Confusion i in from i in c where (i is AdvancedConfusion) && ((i.Process & ProcessType.Pre) == ProcessType.Pre) orderby i.Priority ascending select i)
                    {
                        c.ExecutePreConfusion(i as AdvancedConfusion, this, accessor);
                    }
                });
                psr.DoProcess += new MetadataProcessor.Do(delegate(MetadataProcessor.MetadataAccessor accessor)
                {
                    foreach (Confusion i in from i in c where (i is AdvancedConfusion) && ((i.Process & ProcessType.Real) == ProcessType.Real) orderby i.Priority ascending select i)
                    {
                        c.ExecuteConfusion(i as AdvancedConfusion, this, accessor);
                    }
                });
                psr.PostProcess += new MetadataProcessor.Do(delegate(MetadataProcessor.MetadataAccessor accessor)
                {
                    foreach (Confusion i in from i in c where (i is AdvancedConfusion) && ((i.Process & ProcessType.Post) == ProcessType.Post) orderby i.Priority ascending select i)
                    {
                        c.ExecutePostConfusion(i as AdvancedConfusion, this, accessor);
                    }
                });
                psr.Process(asm.MainModule, final);

                if (cps)
                {
                    Compressor.Compress(this, final.ToArray(), asm, dst);
                }
                else
                {
                    File.WriteAllBytes(dst, final.ToArray());
                }

                ScreenLog("<saved/>");
            }
            catch (Exception ex)
            {
                ScreenLog("<fault message='" + ex.Message + "'/>");
                if (Fault != null)
                    Fault(this, new ExceptionEventArgs(ex));
            }
            lv = 0;

            ScreenLog("<end time='" + DateTime.Now.ToShortTimeString() + "'/>");
            if (Finish != null)
                Finish(this, new EventArgs());
        }

        public void ConfuseAsync(string src, string dst)
        {
            ThreadPool.QueueUserWorkItem(delegate { Confuse(src, dst); });
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
