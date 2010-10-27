using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

static class AntiDebugger
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

static class Proxies
{
    private static void CtorProxy(RuntimeFieldHandle f)
    {
        var fld = System.Reflection.FieldInfo.GetFieldFromHandle(f);
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var mtd = asm.GetModules()[0].ResolveMethod(BitConverter.ToInt32(Encoding.Unicode.GetBytes(fld.Name.ToCharArray(), 1, 2), 0)) as System.Reflection.ConstructorInfo;
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(mtd.DeclaringType.TypeHandle);

        var args = mtd.GetParameters();
        Type[] arg = new Type[args.Length];
        for (int i = 0; i < args.Length; i++)
            arg[i] = args[i].ParameterType;

        var dm = new System.Reflection.Emit.DynamicMethod("", mtd.DeclaringType, arg, mtd.DeclaringType, true);
        var gen = dm.GetILGenerator();
        for (int i = 0; i < arg.Length; i++)
            gen.Emit(System.Reflection.Emit.OpCodes.Ldarg_S, i);
        gen.Emit(System.Reflection.Emit.OpCodes.Newobj, mtd);
        gen.Emit(System.Reflection.Emit.OpCodes.Ret);

        fld.SetValue(null, dm.CreateDelegate(fld.FieldType));
    }

    private static void MtdProxy(RuntimeFieldHandle f)
    {
        var fld = System.Reflection.FieldInfo.GetFieldFromHandle(f);
        var asm = System.Reflection.Assembly.GetExecutingAssembly();
        var mtd = asm.GetModules()[0].ResolveMethod(BitConverter.ToInt32(Encoding.Unicode.GetBytes(fld.Name.ToCharArray(), 2, 2), 0)) as System.Reflection.MethodInfo;

        if (mtd.IsStatic)
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(mtd.DeclaringType.TypeHandle);
            fld.SetValue(null, Delegate.CreateDelegate(fld.FieldType, mtd));
        }
        else
        {
            var tmp = mtd.GetParameters();
            Type[] arg = new Type[tmp.Length + 1];
            arg[0] = typeof(object);
            for (int i = 0; i < tmp.Length; i++)
                arg[i + 1] = tmp[i].ParameterType;

            System.Reflection.Emit.DynamicMethod dm;
            if (mtd.DeclaringType.IsInterface)
                dm = new System.Reflection.Emit.DynamicMethod("", mtd.ReturnType, arg, true);
            else
                dm = new System.Reflection.Emit.DynamicMethod("", mtd.ReturnType, arg, mtd.DeclaringType, true);
            var gen = dm.GetILGenerator();
            for (int i = 0; i < arg.Length; i++)
                gen.Emit(System.Reflection.Emit.OpCodes.Ldarg, i);
            gen.Emit((fld.Name[1] == '\r') ? System.Reflection.Emit.OpCodes.Callvirt : System.Reflection.Emit.OpCodes.Call, mtd);
            gen.Emit(System.Reflection.Emit.OpCodes.Ret);

            fld.SetValue(null, dm.CreateDelegate(fld.FieldType));
        }
    }
}

static class Encryptions
{
    static Assembly Resources(object sender, ResolveEventArgs args)
    {
        Assembly datAsm;
        if ((datAsm = AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDING") as System.Reflection.Assembly) == null)
        {
            Stream str = typeof(Exception).Assembly.GetManifestResourceStream("PADDINGPADDINGPADDING");
            using (BinaryReader rdr = new BinaryReader(new DeflateStream(str, CompressionMode.Decompress)))
            {
                byte[] enDat = rdr.ReadBytes(rdr.ReadInt32());
                byte[] final = new byte[enDat.Length / 2];
                for (int i = 0; i < enDat.Length; i += 2)
                {
                    final[i / 2] = (byte)((enDat[i + 1] ^ 0x11) * 0x22 + (enDat[i] ^ 0x11));
                }
                using (BinaryReader rdr1 = new BinaryReader(new DeflateStream(new MemoryStream(final), CompressionMode.Decompress)))
                {
                    byte[] fDat = rdr1.ReadBytes(rdr1.ReadInt32());
                    datAsm = System.Reflection.Assembly.Load(fDat);
                    AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDING", datAsm);
                }
            }
        }
        if (Array.IndexOf(datAsm.GetManifestResourceNames(), args.Name) == -1)
            return null;
        else
            return datAsm;
    }

    private static string SafeStrings(int id)
    {
        Dictionary<int, string> hashTbl;
        if ((hashTbl = AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDING") as Dictionary<int, string>) == null)
            AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDING", hashTbl = new Dictionary<int, string>());
        string ret;
        if (!hashTbl.TryGetValue(id, out ret))
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetCallingAssembly();
            Stream str = asm.GetManifestResourceStream("PADDINGPADDINGPADDING");
            int mdTkn = new StackFrame(1).GetMethod().MetadataToken;
            using (BinaryReader rdr = new BinaryReader(new DeflateStream(str, CompressionMode.Decompress)))
            {
                rdr.ReadBytes((mdTkn ^ id) - 12345678);
                int len = (int)((~rdr.ReadUInt32()) ^ 87654321);
                byte[] b = rdr.ReadBytes(len);

                ///////////////////
                Random rand = new Random((int)mdTkn);

                int key = 0;
                for (int i = 0; i < b.Length; i++)
                {
                    byte o = b[i];
                    b[i] = (byte)(b[i] ^ (rand.Next() & key));
                    key += o;
                }
                return Encoding.UTF8.GetString(b);
                ///////////////////
            }
        }
        return ret;
    }
    private static string Strings(int id)
    {
        Dictionary<int, string> hashTbl;
        if ((hashTbl = AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDING") as Dictionary<int, string>) == null)
            AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDING", hashTbl = new Dictionary<int, string>());
        string ret;
        if (!hashTbl.TryGetValue(id, out ret))
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetCallingAssembly();
            Stream str = asm.GetManifestResourceStream("PADDINGPADDINGPADDING");
            int mdTkn = new StackFrame(1).GetMethod().MetadataToken;
            using (BinaryReader rdr = new BinaryReader(new DeflateStream(str, CompressionMode.Decompress)))
            {
                rdr.ReadBytes((mdTkn ^ id) - 12345678);
                int len = (int)((~rdr.ReadUInt32()) ^ 87654321);

                ///////////////////
                byte[] f = new byte[(len + 7) & ~7];

                for (int i = 0; i < f.Length; i++)
                {
                    f[i] = 123;
                }

                hashTbl[id] = (ret = Encoding.Unicode.GetString(f, 0, len));
                ///////////////////
            }
        }
        return ret;
    }
    static int Read7BitEncodedInt(BinaryReader rdr)
    {
        // Read out an int 7 bits at a time. The high bit
        // of the byte when on means to continue reading more bytes.
        int count = 0;
        int shift = 0;
        byte b;
        do
        {
            b = rdr.ReadByte();
            count |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return count;
    }
}