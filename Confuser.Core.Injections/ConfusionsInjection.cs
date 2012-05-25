using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;

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
    [DllImport("kernel32.dll")]
    static extern bool IsDebuggerPresent();
    [DllImport("kernel32.dll")]
    static extern int OutputDebugString(string str);

    public static void Initialize()
    {
        if (Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING") != null ||
            Environment.GetEnvironmentVariable("COR_PROFILER") != null)
            Environment.FailFast("Profiler detected");

        Thread thread = new Thread(AntiDebug);
        thread.IsBackground = true;
        thread.Start(null);
    }
    static void AntiDebug(object thread)
    {
        Thread th = thread as Thread;
        if (th == null)
        {
            th = new Thread(AntiDebug);
            th.IsBackground = true;
            th.Start(Thread.CurrentThread);
            Thread.Sleep(500);
        }
        while (true)
        {
            //Managed
            if (Debugger.IsAttached || Debugger.IsLogging())
                Environment.FailFast("Debugger detected (Managed)");

            //IsDebuggerPresent
            if (IsDebuggerPresent())
                Environment.FailFast("=_=");

            //Open process
            IntPtr ps = Process.GetCurrentProcess().Handle;
            if (ps == IntPtr.Zero)
                Environment.FailFast("Cannot open process");

            //OutputDebugString
            if (OutputDebugString("=_=") > IntPtr.Size)
                Environment.FailFast("Debugger detected");

            //Close
            try
            {
                CloseHandle(IntPtr.Zero);
            }
            catch
            {
                Environment.FailFast("Debugger detected");
            }

            if (!th.IsAlive)
                Environment.FailFast("Loop broken");

            Thread.Sleep(1000);
        }
    }

    public static void InitializeSafe()
    {
        if (Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING") != null ||
            Environment.GetEnvironmentVariable("COR_PROFILER") != null)
            Environment.FailFast("Profiler detected");

        Thread thread = new Thread(AntiDebugSafe);
        thread.IsBackground = true;
        thread.Start(null);
    }
    private static void AntiDebugSafe(object thread)
    {
        Thread th = thread as Thread;
        if (th == null)
        {
            th = new Thread(AntiDebugSafe);
            th.IsBackground = true;
            th.Start(Thread.CurrentThread);
            Thread.Sleep(500);
        }
        while (true)
        {
            if (Debugger.IsAttached || Debugger.IsLogging())
                Environment.FailFast("Debugger detected (Managed)");

            if (!th.IsAlive)
                Environment.FailFast("Loop broken");

            Thread.Sleep(1000);
        }
    }
}

static class Proxies
{
    public static int PlaceHolder(int val) { return 0; }
    private static void CtorProxy(RuntimeFieldHandle f)
    {
        var fld = FieldInfo.GetFieldFromHandle(f);
        char[] ch = new char[fld.Name.Length];
        for (int i = 0; i < ch.Length; i++)
            ch[i] = (char)((byte)fld.Name[i] ^ i);
        byte[] dat = Convert.FromBase64String(new string(ch));
        var mtd = fld.Module.ResolveMethod(PlaceHolder(BitConverter.ToInt32(dat, 0)) | (dat[4] << 24)) as ConstructorInfo;

        var args = mtd.GetParameters();
        Type[] arg = new Type[args.Length];
        for (int i = 0; i < args.Length; i++)
            arg[i] = args[i].ParameterType;

        DynamicMethod dm;
        if (mtd.DeclaringType.IsInterface || mtd.DeclaringType.IsArray)
            dm = new DynamicMethod("", mtd.DeclaringType, arg, fld.DeclaringType, true);
        else
            dm = new DynamicMethod("", mtd.DeclaringType, arg, mtd.DeclaringType, true);
        var gen = dm.GetILGenerator();
        for (int i = 0; i < arg.Length; i++)
            gen.Emit(System.Reflection.Emit.OpCodes.Ldarg_S, i);
        gen.Emit(System.Reflection.Emit.OpCodes.Newobj, mtd);
        gen.Emit(System.Reflection.Emit.OpCodes.Ret);

        fld.SetValue(null, dm.CreateDelegate(fld.FieldType));
    }
    private static void MtdProxy(RuntimeFieldHandle f)
    {
        var fld = FieldInfo.GetFieldFromHandle(f);
        char[] ch = new char[fld.Name.Length];
        for (int i = 0; i < ch.Length; i++)
            ch[i] = (char)((byte)fld.Name[i] ^ i);
        byte[] dat = Convert.FromBase64String(new string(ch));
        var mtd = fld.Module.ResolveMethod(PlaceHolder(BitConverter.ToInt32(dat, 1)) | ((dat[0] & 0x7f) << 24)) as MethodInfo;

        if (mtd.IsStatic)
            fld.SetValue(null, Delegate.CreateDelegate(fld.FieldType, mtd));
        else
        {
            var tmp = mtd.GetParameters();
            Type[] arg = new Type[tmp.Length + 1];
            arg[0] = typeof(object);
            for (int i = 0; i < tmp.Length; i++)
                arg[i + 1] = tmp[i].ParameterType;

            DynamicMethod dm;
            if (mtd.DeclaringType.IsInterface || mtd.DeclaringType.IsArray)
                dm = new DynamicMethod("", mtd.ReturnType, arg, fld.DeclaringType, true);
            else
                dm = new DynamicMethod("", mtd.ReturnType, arg, mtd.DeclaringType, true);
            var gen = dm.GetILGenerator();
            for (int i = 0; i < arg.Length; i++)
            {
                gen.Emit(OpCodes.Ldarg, i);
                if (i == 0) gen.Emit(OpCodes.Castclass, mtd.DeclaringType);
            }
            gen.Emit((dat[0] & 0x80) != 0 ? OpCodes.Callvirt : OpCodes.Call, mtd);
            gen.Emit(OpCodes.Ret);

            fld.SetValue(null, dm.CreateDelegate(fld.FieldType));
        }
    }
}

static class Encryptions
{
    static Assembly datAsm;
    static Assembly Resources(object sender, ResolveEventArgs args)
    {
        if (datAsm == null)
        {
            Stream str = typeof(Exception).Assembly.GetManifestResourceStream("PADDINGPADDINGPADDING");
            using (BinaryReader rdr = new BinaryReader(new DeflateStream(str, CompressionMode.Decompress)))
            {
                byte[] dat = rdr.ReadBytes(rdr.ReadInt32());
                byte k = 0x11;
                for (int i = 0; i < dat.Length; i++)
                {
                    dat[i] = (byte)(dat[i] ^ k);
                    k = (byte)((k * 0x22) % 0x100);
                }
                datAsm = System.Reflection.Assembly.Load(dat);
                Buffer.BlockCopy(new byte[dat.Length], 0, dat, 0, dat.Length);
            }
        }
        if (Array.IndexOf(datAsm.GetManifestResourceNames(), args.Name) == -1)
            return null;
        else
            return datAsm;
    }

    //private static string SafeStrings(int id)
    //{
    //    Dictionary<int, string> hashTbl;
    //    if ((hashTbl = AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDING") as Dictionary<int, string>) == null)
    //    {
    //        AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDING", hashTbl = new Dictionary<int, string>());
    //        MemoryStream stream = new MemoryStream();
    //        Assembly asm = Assembly.GetCallingAssembly();
    //        using (DeflateStream str = new DeflateStream(asm.GetManifestResourceStream("PADDINGPADDINGPADDING"), CompressionMode.Decompress))
    //        {
    //            byte[] dat = new byte[0x1000];
    //            int read = str.Read(dat, 0, 0x1000);
    //            do
    //            {
    //                stream.Write(dat, 0, read);
    //                read = str.Read(dat, 0, 0x1000);
    //            }
    //            while (read != 0);
    //        }
    //        AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDINGPADDING", stream.ToArray());
    //    }
    //    string ret;
    //    int mdTkn = new StackFrame(1).GetMethod().MetadataToken;
    //    int pos = (mdTkn ^ id) - 12345678;
    //    if (!hashTbl.TryGetValue(pos, out ret))
    //    {
    //        using (BinaryReader rdr = new BinaryReader(new MemoryStream((byte[])AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDINGPADDING"))))
    //        {
    //            rdr.BaseStream.Seek(pos, SeekOrigin.Begin);
    //            int len = (int)((~rdr.ReadUInt32()) ^ 87654321);
    //            byte[] b = rdr.ReadBytes(len);

    //            ///////////////////

    //            uint seed = 88888888;
    //            ushort _m = (ushort)(seed >> 16);
    //            ushort _c = (ushort)(seed & 0xffff);
    //            ushort m = _c; ushort c = _m;
    //            byte[] k = new byte[b.Length];
    //            for (int i = 0; i < k.Length; i++)
    //            {
    //                k[i] = (byte)((seed * m + c) % 0x100);
    //                m = (ushort)((seed * m + _m) % 0x10000);
    //                c = (ushort)((seed * c + _c) % 0x10000);
    //            }

    //            int key = 0;
    //            for (int i = 0; i < b.Length; i++)
    //            {
    //                byte o = b[i];
    //                b[i] = (byte)(b[i] ^ (key / k[i]));
    //                key += o;
    //            }
    //            hashTbl[pos] = (ret = Encoding.UTF8.GetString(b));
    //            ///////////////////
    //        }
    //    }
    //    return ret;
    //}
    //private static string Strings(int id)
    //{
    //    Dictionary<int, string> hashTbl;
    //    if ((hashTbl = AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDING") as Dictionary<int, string>) == null)
    //    {
    //        AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDING", hashTbl = new Dictionary<int, string>());
    //        MemoryStream stream = new MemoryStream();
    //        Assembly asm = Assembly.GetCallingAssembly();
    //        using (DeflateStream str = new DeflateStream(asm.GetManifestResourceStream("PADDINGPADDINGPADDING"), CompressionMode.Decompress))
    //        {
    //            byte[] dat = new byte[0x1000];
    //            int read = str.Read(dat, 0, 0x1000);
    //            do
    //            {
    //                stream.Write(dat, 0, read);
    //                read = str.Read(dat, 0, 0x1000);
    //            }
    //            while (read != 0);
    //        }
    //        AppDomain.CurrentDomain.SetData("PADDINGPADDINGPADDINGPADDING", stream.ToArray());
    //    }
    //    string ret;
    //    int mdTkn = new StackFrame(1).GetMethod().MetadataToken;
    //    int pos = (mdTkn ^ id) - 12345678;
    //    if (!hashTbl.TryGetValue(pos, out ret))
    //    {
    //        using (BinaryReader rdr = new BinaryReader(new MemoryStream((byte[])AppDomain.CurrentDomain.GetData("PADDINGPADDINGPADDINGPADDING"))))
    //        {
    //            rdr.BaseStream.Seek(pos, SeekOrigin.Begin);
    //            int len = (int)((~rdr.ReadUInt32()) ^ 87654321);

    //            ///////////////////
    //            byte[] f = new byte[(len + 7) & ~7];

    //            for (int i = 0; i < f.Length; i++)
    //            {
    //                Poly.PolyStart();
    //                int count = 0;
    //                int shift = 0;
    //                byte b;
    //                do
    //                {
    //                    b = rdr.ReadByte();
    //                    count |= (b & 0x7F) << shift;
    //                    shift += 7;
    //                } while ((b & 0x80) != 0);

    //                f[i] = (byte)Poly.PlaceHolder(count);
    //            }

    //            hashTbl[pos] = (ret = Encoding.Unicode.GetString(f, 0, len));
    //            ///////////////////
    //        }
    //    }
    //    return ret;
    //}

    public static int PlaceHolder(int val) { return 0; }

    static Dictionary<uint, object> constTbl;
    static Stream constStream;
    static void Initialize()
    {
        constTbl = new Dictionary<uint, object>();
        constStream = new MemoryStream();
        Assembly asm = Assembly.GetExecutingAssembly();
        DeflateStream str = new DeflateStream(asm.GetManifestResourceStream(Encoding.UTF8.GetString(BitConverter.GetBytes(0x12345678))), CompressionMode.Decompress);
        {
            byte[] dat = new byte[0x1000];
            int read = str.Read(dat, 0, 0x1000);
            do
            {
                constStream.Write(dat, 0, read);
                read = str.Read(dat, 0, 0x1000);
            }
            while (read != 0);
        }
        str.Dispose();
    }
    static object Constants(uint a, uint b)
    {
        object ret;
        var method = MethodBase.GetCurrentMethod();
        uint x = (uint)(method.MetadataToken ^ (method.DeclaringType.MetadataToken * a));
        uint h = 0x67452301 ^ x;
        uint h1 = 0x3bd523a0;
        uint h2 = 0x5f6f36c0;
        for (uint i = 1; i <= 64; i++)
        {
            h = (h & 0x00ffffff) << 8 | ((h & 0xff000000) >> 24);
            uint n = (h & 0xff) % 64;
            if (n >= 0 && n < 16)
            {
                h1 |= (((h & 0x0000ff00) >> 8) & ((h & 0x00ff0000) >> 16)) ^ (~h & 0x000000ff);
                h2 ^= (h * i + 1) % 16;
                h += (h1 | h2) ^ 12345678;
            }
            else if (n >= 16 && n < 32)
            {
                h1 ^= ((h & 0x00ff00ff) << 8) ^ (((h & 0x00ffff00) >> 8) | (~h & 0x0000ffff));
                h2 += (h * i) % 32;
                h |= (h1 + ~h2) & 12345678;
            }
            else if (n >= 32 && n < 48)
            {
                h1 += ((h & 0x000000ff) | ((h & 0x00ff0000) >> 16)) + (~h & 0x000000ff);
                h2 -= ~(h + n) % 48;
                h ^= (h1 % h2) | 12345678;
            }
            else if (n >= 48 && n < 64)
            {
                h1 ^= (((h & 0x00ff0000) >> 16) | ~(h & 0x0000ff)) * (~h & 0x00ff0000);
                h2 += (h ^ i - 1) % n;
                h -= ~(h1 ^ h2) + 12345678;
            }
        }
        uint pos = h ^ b;
        if (!constTbl.TryGetValue(pos, out ret))
        {
            byte type;
            byte[] bs;
            lock (constStream)
            {
                BinaryReader rdr = new BinaryReader(constStream);
                rdr.BaseStream.Seek(pos, SeekOrigin.Begin);
                type = rdr.ReadByte();
                bs = rdr.ReadBytes(rdr.ReadInt32());
            }

            byte[] f;
            int len;
            var key = Assembly.GetCallingAssembly().GetModule(method.Module.ScopeName).ResolveSignature((int)(0x263013d3 ^ method.MetadataToken));
            using (BinaryReader r = new BinaryReader(new MemoryStream(bs)))
            {
                len = r.ReadInt32() ^ 0x57425674;
                f = new byte[(len + 7) & ~7];
                for (int i = 0; i < f.Length; i++)
                {
                    int count = 0;
                    int shift = 0;
                    byte c;
                    do
                    {
                        c = r.ReadByte();
                        count |= (c & 0x7F) << shift;
                        shift += 7;
                    } while ((c & 0x80) != 0);

                    count = PlaceHolder(count);
                    f[i] = (byte)(count ^ key[i % 8]);
                }
            }
            if (type == 11)
                ret = BitConverter.ToDouble(f, 0);
            else if (type == 22)
                ret = BitConverter.ToSingle(f, 0);
            else if (type == 33)
                ret = BitConverter.ToInt32(f, 0);
            else if (type == 44)
                ret = BitConverter.ToInt64(f, 0);
            else if (type == 55)
                ret = Encoding.UTF8.GetString(f, 0, len);
            constTbl[pos] = ret;
        }
        return ret;
    }
    static object SafeConstants(uint a, uint b)
    {
        object ret;
        var method = MethodBase.GetCurrentMethod();
        uint x = (uint)(method.MetadataToken ^ (method.DeclaringType.MetadataToken * a));
        uint h = 0x67452301 ^ x;
        uint h1 = 0x3bd523a0;
        uint h2 = 0x5f6f36c0;
        for (uint i = 1; i <= 64; i++)
        {
            h = (h & 0x00ffffff) << 8 | ((h & 0xff000000) >> 24);
            uint n = (h & 0xff) % 64;
            if (n >= 0 && n < 16)
            {
                h1 |= (((h & 0x0000ff00) >> 8) & ((h & 0x00ff0000) >> 16)) ^ (~h & 0x000000ff);
                h2 ^= (h * i + 1) % 16;
                h += (h1 | h2) ^ 12345678;
            }
            else if (n >= 16 && n < 32)
            {
                h1 ^= ((h & 0x00ff00ff) << 8) ^ (((h & 0x00ffff00) >> 8) | (~h & 0x0000ffff));
                h2 += (h * i) % 32;
                h |= (h1 + ~h2) & 12345678;
            }
            else if (n >= 32 && n < 48)
            {
                h1 += ((h & 0x000000ff) | ((h & 0x00ff0000) >> 16)) + (~h & 0x000000ff);
                h2 -= ~(h + n) % 48;
                h ^= (h1 % h2) | 12345678;
            }
            else if (n >= 48 && n < 64)
            {
                h1 ^= (((h & 0x00ff0000) >> 16) | ~(h & 0x0000ff)) * (~h & 0x00ff0000);
                h2 += (h ^ i - 1) % n;
                h -= ~(h1 ^ h2) + 12345678;
            }
        }
        uint pos = h ^ b;
        if (!constTbl.TryGetValue(pos, out ret))
        {
            byte type;
            byte[] f;
            lock (constStream)
            {
                BinaryReader rdr = new BinaryReader(constStream);
                rdr.BaseStream.Seek(pos, SeekOrigin.Begin);
                type = rdr.ReadByte();
                f = rdr.ReadBytes(rdr.ReadInt32());
            }

            var key = Assembly.GetCallingAssembly().GetModule(method.Module.ScopeName).ResolveSignature(0x263013d3 ^ method.MetadataToken);
            uint seed = (pos + type) * 0x57425674;
            ushort _m = (ushort)(seed >> 16);
            ushort _c = (ushort)(seed & 0xffff);
            ushort m = _c; ushort c = _m;
            for (int i = 0; i < f.Length; i++)
            {
                f[i] ^= (byte)(((seed * m + c) % 0x100) ^ key[i % 8]);
                m = (ushort)((seed * m + _m) % 0x10000);
                c = (ushort)((seed * c + _c) % 0x10000);
            }

            if (type == 11)
                ret = BitConverter.ToDouble(f, 0);
            else if (type == 22)
                ret = BitConverter.ToSingle(f, 0);
            else if (type == 33)
                ret = BitConverter.ToInt32(f, 0);
            else if (type == 44)
                ret = BitConverter.ToInt64(f, 0);
            else if (type == 55)
                ret = Encoding.UTF8.GetString(f);
            constTbl[pos] = ret;
        }

        return ret;
    }
}

static class AntiDumping
{
    [DllImportAttribute("kernel32.dll")]
    static unsafe extern bool VirtualProtect(byte* lpAddress, int dwSize, uint flNewProtect, out uint lpflOldProtect);

    public static unsafe void Initialize()
    {
        uint old;
        byte* bas = (byte*)Marshal.GetHINSTANCE(typeof(AntiDumping).Module);
        byte* ptr = bas + 0x3c;
        byte* ptr2;
        ptr = ptr2 = bas + *(uint*)ptr;
        ptr += 0x6;
        ushort sectNum = *(ushort*)ptr;
        ptr += 14;
        ushort optSize = *(ushort*)ptr;
        ptr = ptr2 = ptr + 0x4 + optSize;

        byte* newMod = stackalloc byte[11];
        *(uint*)newMod = 0x6c64746e;
        *((uint*)newMod + 1) = 0x6c642e6c;
        *((ushort*)newMod + 4) = 0x006c;
        *(newMod + 10) = 0;
        byte* newFunc = stackalloc byte[11];
        *(uint*)newFunc = 0x6f43744e;
        *((uint*)newFunc + 1) = 0x6e69746e;
        *((ushort*)newFunc + 4) = 0x6575;
        *(newFunc + 10) = 0;

        if (typeof(AntiDumping).Module.FullyQualifiedName != "<Unknown>")   //Mapped
        {
            //VirtualProtect(ptr - 16, 8, 0x40, out old);
            //*(uint*)(ptr - 12) = 0;
            byte* mdDir = bas + *(uint*)(ptr - 16);
            //*(uint*)(ptr - 16) = 0;

            if (*(uint*)(ptr - 0x78) != 0)
            {
                byte* importDir = bas + *(uint*)(ptr - 0x78);
                byte* oftMod = bas + *(uint*)importDir;
                byte* modName = bas + *(uint*)(importDir + 12);
                byte* funcName = bas + *(uint*)oftMod + 2;
                VirtualProtect(modName, 11, 0x40, out old);
                for (int i = 0; i < 11; i++)
                    *(modName + i) = *(newMod + i);
                VirtualProtect(funcName, 11, 0x40, out old);
                for (int i = 0; i < 11; i++)
                    *(funcName + i) = *(newFunc + i);
            }

            for (int i = 0; i < sectNum; i++)
            {
                VirtualProtect(ptr, 8, 0x40, out old);
                Marshal.Copy(new byte[8], 0, (IntPtr)ptr, 8);
                ptr += 0x28;
            }
            VirtualProtect(mdDir, 0x48, 0x40, out old);
            byte* mdHdr = bas + *(uint*)(mdDir + 8);
            *(uint*)mdDir = 0;
            *((uint*)mdDir + 1) = 0;
            *((uint*)mdDir + 2) = 0;
            *((uint*)mdDir + 3) = 0;

            VirtualProtect(mdHdr, 4, 0x40, out old);
            *(uint*)mdHdr = 0;
            mdHdr += 12;
            mdHdr += *(uint*)mdHdr;
            mdHdr = (byte*)(((uint)mdHdr + 7) & ~3);
            mdHdr += 2;
            ushort numOfStream = *mdHdr;
            mdHdr += 2;
            for (int i = 0; i < numOfStream; i++)
            {
                VirtualProtect(mdHdr, 8, 0x40, out old);
                *(uint*)mdHdr = 0;
                mdHdr += 4;
                *(uint*)mdHdr = 0;
                mdHdr += 4;
                for (int ii = 0; ii < 8; ii++)
                {
                    VirtualProtect(mdHdr, 4, 0x40, out old);
                    *mdHdr = 0; mdHdr++;
                    if (*mdHdr == 0)
                    {
                        mdHdr += 3;
                        break;
                    }
                    *mdHdr = 0; mdHdr++;
                    if (*mdHdr == 0)
                    {
                        mdHdr += 2;
                        break;
                    }
                    *mdHdr = 0; mdHdr++;
                    if (*mdHdr == 0)
                    {
                        mdHdr += 1;
                        break;
                    }
                    *mdHdr = 0; mdHdr++;
                }
            }
        }
        else   //Flat
        {
            //VirtualProtect(ptr - 16, 8, 0x40, out old);
            //*(uint*)(ptr - 12) = 0;
            uint mdDir = *(uint*)(ptr - 16);
            //*(uint*)(ptr - 16) = 0;
            uint importDir = *(uint*)(ptr - 0x78);

            uint[] vAdrs = new uint[sectNum];
            uint[] vSizes = new uint[sectNum];
            uint[] rAdrs = new uint[sectNum];
            for (int i = 0; i < sectNum; i++)
            {
                VirtualProtect(ptr, 8, 0x40, out old);
                Marshal.Copy(new byte[8], 0, (IntPtr)ptr, 8);
                vAdrs[i] = *(uint*)(ptr + 12);
                vSizes[i] = *(uint*)(ptr + 8);
                rAdrs[i] = *(uint*)(ptr + 20);
                ptr += 0x28;
            }


            if (importDir != 0)
            {
                for (int i = 0; i < sectNum; i++)
                    if (vAdrs[i] < importDir && importDir < vAdrs[i] + vSizes[i])
                    {
                        importDir = importDir - vAdrs[i] + rAdrs[i];
                        break;
                    }
                byte* importDirPtr = bas + importDir;
                uint oftMod = *(uint*)importDirPtr;
                for (int i = 0; i < sectNum; i++)
                    if (vAdrs[i] < oftMod && oftMod < vAdrs[i] + vSizes[i])
                    {
                        oftMod = oftMod - vAdrs[i] + rAdrs[i];
                        break;
                    }
                byte* oftModPtr = bas + oftMod;
                uint modName = *(uint*)(importDirPtr + 12);
                for (int i = 0; i < sectNum; i++)
                    if (vAdrs[i] < modName && modName < vAdrs[i] + vSizes[i])
                    {
                        modName = modName - vAdrs[i] + rAdrs[i];
                        break;
                    }
                uint funcName = *(uint*)oftModPtr + 2;
                for (int i = 0; i < sectNum; i++)
                    if (vAdrs[i] < funcName && funcName < vAdrs[i] + vSizes[i])
                    {
                        funcName = funcName - vAdrs[i] + rAdrs[i];
                        break;
                    }
                VirtualProtect(bas + modName, 11, 0x40, out old);
                for (int i = 0; i < 11; i++)
                    *(bas + modName + i) = *(newMod + i);
                VirtualProtect(bas + funcName, 11, 0x40, out old);
                for (int i = 0; i < 11; i++)
                    *(bas + funcName + i) = *(newFunc + i);
            }


            for (int i = 0; i < sectNum; i++)
                if (vAdrs[i] < mdDir && mdDir < vAdrs[i] + vSizes[i])
                {
                    mdDir = mdDir - vAdrs[i] + rAdrs[i];
                    break;
                }
            byte* mdDirPtr = bas + mdDir;
            VirtualProtect(mdDirPtr, 0x48, 0x40, out old);
            uint mdHdr = *(uint*)(mdDirPtr + 8);
            for (int i = 0; i < sectNum; i++)
                if (vAdrs[i] < mdHdr && mdHdr < vAdrs[i] + vSizes[i])
                {
                    mdHdr = mdHdr - vAdrs[i] + rAdrs[i];
                    break;
                }
            *(uint*)mdDirPtr = 0;
            *((uint*)mdDirPtr + 1) = 0;
            *((uint*)mdDirPtr + 2) = 0;
            *((uint*)mdDirPtr + 3) = 0;


            byte* mdHdrPtr = bas + mdHdr;
            VirtualProtect(mdHdrPtr, 4, 0x40, out old);
            *(uint*)mdHdrPtr = 0;
            mdHdrPtr += 12;
            mdHdrPtr += *(uint*)mdHdrPtr;
            mdHdrPtr = (byte*)(((uint)mdHdrPtr + 7) & ~3);
            mdHdrPtr += 2;
            ushort numOfStream = *mdHdrPtr;
            mdHdrPtr += 2;
            for (int i = 0; i < numOfStream; i++)
            {
                VirtualProtect(mdHdrPtr, 8, 0x40, out old);
                *(uint*)mdHdrPtr = 0;
                mdHdrPtr += 4;
                *(uint*)mdHdrPtr = 0;
                mdHdrPtr += 4;
                for (int ii = 0; ii < 8; ii++)
                {
                    VirtualProtect(mdHdrPtr, 4, 0x40, out old);
                    *mdHdrPtr = 0; mdHdrPtr++;
                    if (*mdHdrPtr == 0)
                    {
                        mdHdrPtr += 3;
                        break;
                    }
                    *mdHdrPtr = 0; mdHdrPtr++;
                    if (*mdHdrPtr == 0)
                    {
                        mdHdrPtr += 2;
                        break;
                    }
                    *mdHdrPtr = 0; mdHdrPtr++;
                    if (*mdHdrPtr == 0)
                    {
                        mdHdrPtr += 1;
                        break;
                    }
                    *mdHdrPtr = 0; mdHdrPtr++;
                }
            }
        }
    }
}