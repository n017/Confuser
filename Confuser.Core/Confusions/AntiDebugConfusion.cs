using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Confuser.Core.Confusions
{
    static class AntiDebugWin32
    {
        [DllImport("ntdll.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        static extern int NtQueryInformationProcess(IntPtr ProcessHandle, int ProcessInformationClass,
            byte[] ProcessInformation, uint ProcessInformationLength, out int ReturnLength);
        [DllImport("ntdll.dll", SetLastError = true, CallingConvention = CallingConvention.StdCall)]
        static extern uint NtSetInformationProcess(IntPtr ProcessHandle, int ProcessInformationClass,
            byte[] ProcessInformation, uint ProcessInformationLength);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);

        public static void Initialize()
        {
            System.Diagnostics.Process.EnterDebugMode();
            AntiDebug();
        }

        static void AntiDebug()
        {
            //Managed
            if (Debugger.IsAttached || Debugger.IsLogging())
                Environment.FailFast("Debugger detected (Managed)");

            if (Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING") != null ||
               Environment.GetEnvironmentVariable("COR_PROFILER") != null)
                Environment.FailFast("Profiler detected");

            //Open process
            IntPtr ps = Process.GetCurrentProcess().Handle;
            if (ps == IntPtr.Zero)
                Environment.FailFast("Cannot open process");

            //PEB.BeingDebugged
            byte[] info = new byte[0x18];
            int len;
            NtQueryInformationProcess(ps, 0x0, info, 0x18, out len);
            if (len == 0)
                Environment.FailFast("Cannot query information (PEB)");

            IntPtr pebAdr;
            if (IntPtr.Size == 4)
                pebAdr = (IntPtr)BitConverter.ToInt32(info, 4);
            else
                pebAdr = (IntPtr)BitConverter.ToInt64(info, 8);

            byte[] peb = new byte[0x1d8];
            Marshal.Copy(pebAdr, peb, 0, 0x1d8);
            if (peb[2] != 0)
                Environment.FailFast("Debugger detected (PEB)");

            //DebugPort
            info = new byte[8];
            NtQueryInformationProcess(ps, 0x7, info, (uint)IntPtr.Size, out len);
            if (len != IntPtr.Size)
                Environment.FailFast("Cannot query information (Port)");

            if (BitConverter.ToInt64(info, 0) != 0)
            {
                info.Initialize();
                NtSetInformationProcess(ps, 0x7, info, (uint)IntPtr.Size);
                Environment.FailFast("Debugger detected (Port)");
            }

            //Close
            try
            {
                CloseHandle(IntPtr.Zero);
            }
            catch
            {
                Environment.FailFast("Debugger detected (Closing)");
            }

            Thread.Sleep(1000);
            Thread thread = new Thread(AntiDebug);
            thread.IsBackground = true;
            thread.Start();
        }
    }
    class AntiDebugConfusion : StructurePhase, IConfusion
    {
        public string Name
        {
            get { return "Anti Debug Confusion"; }
        }
        public string Description
        {
            get { return "This confusion prevent the assembly from debugging/profiling."; }
        }
        public string ID
        {
            get { return "anti debug"; }
        }
        public bool StandardCompatible
        {
            get { return true; }
        }
        public Target Target
        {
            get { return Target.Assembly; }
        }
        public Preset Preset
        {
            get { return Preset.Normal; }
        }
        public Phase[] Phases
        {
            get { return new Phase[] { this }; }
        }

        public override Priority Priority
        {
            get { return Priority.AssemblyLevel; }
        }
        public override IConfusion Confusion
        {
            get { return this; }
        }
        public override int PhaseID
        {
            get { return 1; }
        }
        public override bool WholeRun
        {
            get { return true; }
        }

        public override void Initialize(ModuleDefinition mod)
        {
            this.mod = mod;
        }
        public override void DeInitialize()
        {
            //
        }

        ModuleDefinition mod;
        public override void Process(ConfusionParameter parameter)
        {
            AssemblyDefinition self = AssemblyDefinition.ReadAssembly(typeof(StringConfusion).Assembly.Location);
            if (Array.IndexOf(parameter.GlobalParameters.AllKeys, "win32") != -1)
            {
                TypeDefinition type = CecilHelper.Inject(mod, self.MainModule.GetType(typeof(AntiDebugWin32).FullName));
                type.Name = "AntiDebugModule"; type.Namespace = "";
                mod.Types.Add(type);
                TypeDefinition modType = mod.GetType("<Module>");
                ILProcessor psr = modType.GetStaticConstructor().Body.GetILProcessor();
                psr.InsertBefore(psr.Body.Instructions.Count - 1, Instruction.Create(OpCodes.Call, type.Methods.FirstOrDefault(mtd => mtd.Name == "Initialize")));
            }
            else
            {
                MethodDefinition i = CecilHelper.Inject(mod, self.MainModule.GetType(typeof(AntiDebugConfusion).FullName).Methods.FirstOrDefault(mtd => mtd.Name == "AntiDebugSafe"));
                TypeDefinition modType = mod.GetType("<Module>");
                modType.Methods.Add(i);
                ILProcessor psr = modType.GetStaticConstructor().Body.GetILProcessor();
                psr.InsertBefore(psr.Body.Instructions.Count - 1, Instruction.Create(OpCodes.Call, i));
            }
        }

        private static void AntiDebugSafe()
        {
            if (Debugger.IsAttached || Debugger.IsLogging())
                Environment.FailFast("Debugger detected (Managed)");

            if (Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING") != null ||
               Environment.GetEnvironmentVariable("COR_PROFILER") != null)
                Environment.FailFast("Profiler detected");

            Thread.Sleep(1000);
            Thread thread = new Thread(AntiDebugSafe);
            thread.IsBackground = true;
            thread.Start();
        }

    }
}
