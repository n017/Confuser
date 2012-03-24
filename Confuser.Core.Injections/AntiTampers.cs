using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;

static class AntiTamper
{
    static ulong s;
    static ulong l;
    public static unsafe void Initalize()
    {
        Module mod = typeof(AntiTamper).Module;
        IntPtr modPtr = Marshal.GetHINSTANCE(mod);
        s = (ulong)modPtr.ToInt64();
        if (modPtr == (IntPtr)(-1)) Environment.FailFast("Module error");
        bool mapped = mod.FullyQualifiedName != "<Unknown>";
        Stream stream;
        stream = new UnmanagedMemoryStream((byte*)modPtr.ToPointer(), 0xfffffff, 0xfffffff, FileAccess.ReadWrite);

        byte[] buff;
        int checkSumOffset;
        ulong checkSum;
        byte[] iv;
        byte[] dats;
        int sn;
        int snLen;
        using (BinaryReader rdr = new BinaryReader(stream))
        {
            stream.Seek(0x3c, SeekOrigin.Begin);
            uint offset = rdr.ReadUInt32();
            stream.Seek(offset, SeekOrigin.Begin);
            stream.Seek(0x6, SeekOrigin.Current);
            uint sections = rdr.ReadUInt16();
            stream.Seek(0xC, SeekOrigin.Current);
            uint optSize = rdr.ReadUInt16();
            stream.Seek(offset = offset + 0x18, SeekOrigin.Begin);  //Optional hdr
            bool pe32 = (rdr.ReadUInt16() == 0x010b);
            stream.Seek(0x3e, SeekOrigin.Current);
            checkSumOffset = (int)stream.Position;
            uint md = rdr.ReadUInt32() ^ 0x11111111;
            if (md == 0x11111111)
                return;

            stream.Seek(offset = offset + optSize, SeekOrigin.Begin);  //sect hdr
            uint datLoc = 0;
            for (int i = 0; i < sections; i++)
            {
                int h = 0;
                for (int j = 0; j < 8; j++)
                {
                    byte chr = rdr.ReadByte();
                    if (chr != 0) h += chr;
                }
                uint vSize = rdr.ReadUInt32();
                uint vLoc = rdr.ReadUInt32();
                uint rSize = rdr.ReadUInt32();
                uint rLoc = rdr.ReadUInt32();
                if (h == 0x55555555)
                    datLoc = mapped ? vLoc : rLoc;
                if (!mapped && md > vLoc && md < vLoc + vSize)
                    md = md - vLoc + rLoc;

                if (mapped && vSize + vLoc > l) l = vSize + vLoc;
                else if (rSize + rLoc > l) l = rSize + rLoc;

                stream.Seek(0x10, SeekOrigin.Current);
            }

            stream.Seek(md, SeekOrigin.Begin);
            using (MemoryStream str = new MemoryStream())
            {
                stream.Position += 12;
                stream.Position += rdr.ReadUInt32() + 4;
                stream.Position += 2;

                ushort streams = rdr.ReadUInt16();

                for (int i = 0; i < streams; i++)
                {
                    uint pos = rdr.ReadUInt32() + md;
                    uint size = rdr.ReadUInt32();

                    int c = 0;
                    while (rdr.ReadByte() != 0) c++;
                    long ori = stream.Position += (((c + 1) + 3) & ~3) - (c + 1);

                    stream.Position = pos;
                    str.Write(rdr.ReadBytes((int)size), 0, (int)size);
                    stream.Position = ori;
                }

                buff = str.ToArray();
            }

            stream.Seek(datLoc, SeekOrigin.Begin);
            checkSum = rdr.ReadUInt64() ^ 0x2222222222222222;
            sn = rdr.ReadInt32();
            snLen = rdr.ReadInt32();
            iv = rdr.ReadBytes(rdr.ReadInt32() ^ 0x33333333);
            dats = rdr.ReadBytes(rdr.ReadInt32() ^ 0x44444444);
        }

        byte[] md5 = MD5.Create().ComputeHash(buff);
        ulong tCs = BitConverter.ToUInt64(md5, 0) ^ BitConverter.ToUInt64(md5, 8);
        if (tCs != checkSum)
            Environment.FailFast("Broken file");

        byte[] b = Decrypt(buff, iv, dats);
        Buffer.BlockCopy(new byte[buff.Length], 0, buff, 0, buff.Length);
        if (b[0] != 0xd6 || b[1] != 0x6f)
            Environment.FailFast("Broken file");
        byte[] dat = new byte[b.Length - 2];
        Buffer.BlockCopy(b, 2, dat, 0, dat.Length);

        data = dat;
        Hook();
        //AppDomain.CurrentDomain.ProcessExit += Dispose;
    }

    static byte[] Decrypt(byte[] buff, byte[] iv, byte[] dat)
    {
        Rijndael ri = Rijndael.Create();
        byte[] ret = new byte[dat.Length];
        MemoryStream ms = new MemoryStream(dat);
        using (CryptoStream cStr = new CryptoStream(ms, ri.CreateDecryptor(SHA256.Create().ComputeHash(buff), iv), CryptoStreamMode.Read))
        { cStr.Read(ret, 0, dat.Length); }

        SHA512 sha = SHA512.Create();
        byte[] c = sha.ComputeHash(buff);
        for (int i = 0; i < ret.Length; i += 64)
        {
            int len = ret.Length <= i + 64 ? ret.Length : i + 64;
            for (int j = i; j < len; j++)
                ret[j] ^= (byte)(c[j - i] ^ 0x11111111);
            c = sha.ComputeHash(ret, i, len - i);
        }
        return ret;
    }

    static bool hasLinkInfo;
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorJitInfo
    {
        public IntPtr* vfptr;
        public int* vbptr;

        public static ICorDynamicInfo* ICorDynamicInfo(ICorJitInfo* ptr)
        {
            hasLinkInfo = ptr->vbptr[10] > 0 && ptr->vbptr[10] >> 16 == 0;//!=0 and hiword byte ==0
            return (ICorDynamicInfo*)((byte*)&ptr->vbptr + ptr->vbptr[hasLinkInfo ? 10 : 9]);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorDynamicInfo
    {
        public IntPtr* vfptr;
        public int* vbptr;

        public static ICorStaticInfo* ICorStaticInfo(ICorDynamicInfo* ptr)
        {
            return (ICorStaticInfo*)((byte*)&ptr->vbptr + ptr->vbptr[hasLinkInfo ? 9 : 8]);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorStaticInfo
    {
        public IntPtr* vfptr;
        public int* vbptr;

        public static ICorMethodInfo* ICorMethodInfo(ICorStaticInfo* ptr)
        {
            return (ICorMethodInfo*)((byte*)&ptr->vbptr + ptr->vbptr[1]);
        }
        public static ICorModuleInfo* ICorModuleInfo(ICorStaticInfo* ptr)
        {
            return (ICorModuleInfo*)((byte*)&ptr->vbptr + ptr->vbptr[2]);
        }
        public static ICorClassInfo* ICorClassInfo(ICorStaticInfo* ptr)
        {
            return (ICorClassInfo*)((byte*)&ptr->vbptr + ptr->vbptr[3]);
        }
        public static ICorFieldInfo* ICorFieldInfo(ICorStaticInfo* ptr)
        {
            return (ICorFieldInfo*)((byte*)&ptr->vbptr + ptr->vbptr[4]);
        }
        public static ICorDebugInfo* ICorDebugInfo(ICorStaticInfo* ptr)
        {
            return (ICorDebugInfo*)((byte*)&ptr->vbptr + ptr->vbptr[5]);
        }
        public static ICorArgInfo* ICorArgInfo(ICorStaticInfo* ptr)
        {
            return (ICorArgInfo*)((byte*)&ptr->vbptr + ptr->vbptr[6]);
        }
        public static ICorLinkInfo* ICorLinkInfo(ICorStaticInfo* ptr)
        {
            return (ICorLinkInfo*)((byte*)&ptr->vbptr + ptr->vbptr[7]);
        }
        public static ICorErrorInfo* ICorErrorInfo(ICorStaticInfo* ptr)
        {
            return (ICorErrorInfo*)((byte*)&ptr->vbptr + ptr->vbptr[hasLinkInfo ? 8 : 7]);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorMethodInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorModuleInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorClassInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorFieldInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorDebugInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorArgInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorLinkInfo
    {
        public IntPtr* vfptr;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct ICorErrorInfo
    {
        public IntPtr* vfptr;
    }

    enum CorInfoOptions
    {
        OPT_INIT_LOCALS = 0x00000010,
        GENERICS_CTXT_FROM_THIS = 0x00000020,
        GENERICS_CTXT_FROM_PARAMTYPEARG = 0x00000040,
        GENERICS_CTXT_MASK = (GENERICS_CTXT_FROM_THIS | GENERICS_CTXT_FROM_PARAMTYPEARG),
        GENERICS_CTXT_KEEP_ALIVE = 0x00000080
    }
    enum CorInfoType : byte
    {
        UNDEF = 0x0,
        VOID = 0x1,
        BOOL = 0x2,
        CHAR = 0x3,
        BYTE = 0x4,
        UBYTE = 0x5,
        SHORT = 0x6,
        USHORT = 0x7,
        INT = 0x8,
        UINT = 0x9,
        LONG = 0xa,
        ULONG = 0xb,
        NATIVEINT = 0xc,
        NATIVEUINT = 0xd,
        FLOAT = 0xe,
        DOUBLE = 0xf,
        STRING = 0x10,
        PTR = 0x11,
        BYREF = 0x12,
        VALUECLASS = 0x13,
        CLASS = 0x14,
        REFANY = 0x15,
        VAR = 0x16,
        COUNT
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    unsafe struct CORINFO_METHOD_INFO
    {
        public IntPtr ftn;
        public IntPtr scope;
        public byte* ILCode;
        public uint ILCodeSize;
    }
    enum CorInfoCallConv
    {
        DEFAULT = 0x0,
        C = 0x1,
        STDCALL = 0x2,
        THISCALL = 0x3,
        FASTCALL = 0x4,
        VARARG = 0x5,
        FIELD = 0x6,
        LOCAL_SIG = 0x7,
        PROPERTY = 0x8,
        NATIVEVARARG = 0xb,
        MASK = 0x0f,
        GENERIC = 0x10,
        HASTHIS = 0x20,
        EXPLICITTHIS = 0x40,
        PARAMTYPE = 0x80,
    }


    [StructLayout(LayoutKind.Sequential)]
    unsafe struct CORINFO_SIG_INST_x86
    {
        public uint classInstCount;
        public IntPtr* classInst;
        public uint methInstCount;
        public IntPtr* methInst;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct CORINFO_SIG_INST_x64
    {
        public uint classInstCount;
        uint pad1;
        public IntPtr* classInst;
        public uint methInstCount;
        uint pad2;
        public IntPtr* methInst;
    }

    [StructLayout(LayoutKind.Sequential)]
    unsafe struct CORINFO_SIG_INFO_x86
    {
        public CorInfoCallConv callConv;
        public IntPtr retTypeClass;
        public IntPtr retTypeSigClass;
        public CorInfoType retType;
        public byte flags;
        public ushort numArgs;
        public CORINFO_SIG_INST_x86 sigInst;
        public IntPtr args;
        public IntPtr sig;
        public IntPtr scope;
        public IntPtr token;
    }
    [StructLayout(LayoutKind.Sequential)]
    unsafe struct CORINFO_SIG_INFO_x64
    {
        public CorInfoCallConv callConv;
        uint pad1;
        public IntPtr retTypeClass;
        public IntPtr retTypeSigClass;
        public CorInfoType retType;
        public byte flags;
        public ushort numArgs;
        uint pad2;
        public CORINFO_SIG_INST_x64 sigInst;
        public IntPtr args;
        public IntPtr sig;
        public IntPtr scope;
        public uint token;
        uint pad3;
    }

    enum CORINFO_EH_CLAUSE_FLAGS
    {
        NONE = 0,
        FILTER = 0x0001,
        FINALLY = 0x0002,
        FAULT = 0x0004,
    }
    struct CORINFO_EH_CLAUSE
    {
        public CORINFO_EH_CLAUSE_FLAGS Flags;
        public uint TryOffset;
        public uint TryLength;
        public uint HandlerOffset;
        public uint HandlerLength;
        public uint ClassTokenOrFilterOffset;
    }
    enum CorInfoTokenKind
    {
        Default,
        Ldtoken,
        Casting
    }
    enum CORINFO_RUNTIME_LOOKUP_KIND
    {
        THISOBJ,
        METHODPARAM,
        CLASSPARAM,
    }
    enum InfoAccessType
    {
        VALUE,
        PVALUE,
        PPVALUE
    }
    enum InfoAccessModule
    {
        UNKNOWN_MODULE,
        CURRENT_MODULE,
        EXTERNAL_MODULE,
    }
    struct CORINFO_GENERICHANDLE_RESULT
    {
        public CORINFO_LOOKUP lookup;
        public IntPtr compileTimeHandle;
    }
    struct CORINFO_LOOKUP
    {
        public CORINFO_LOOKUP_KIND lookupKind;
        public CORINFO_RUNTIME_LOOKUP runtimeLookup;
        public CORINFO_CONST_LOOKUP constLookup;
    }
    struct CORINFO_LOOKUP_KIND
    {
        public bool needsRuntimeLookup;
        public CORINFO_RUNTIME_LOOKUP_KIND runtimeLookupKind;
    }
    unsafe struct CORINFO_RUNTIME_LOOKUP
    {
        public uint token1;
        public uint token2;
        public IntPtr helper;
        public ushort indirections;
        public ushort testForNull;
        public fixed ushort offsets[4];
    }
    struct CORINFO_CONST_LOOKUP
    {
        public IntPtr handleOrAddr;
        public InfoAccessType accessType;
        public InfoAccessModule accessModule;
    }

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    unsafe delegate uint compileMethod(IntPtr self, ICorJitInfo* comp, CORINFO_METHOD_INFO* info, uint flags, byte** nativeEntry, uint* nativeSizeOfCode);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    unsafe delegate void findSig(IntPtr self, IntPtr module, uint sigTOK, IntPtr context, void* sig);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    unsafe delegate void getEHinfo(IntPtr self, IntPtr ftn, uint EHnumber, CORINFO_EH_CLAUSE* clause);


    [DllImport("kernel32.dll")]
    static extern IntPtr LoadLibrary(string lib);
    [DllImport("kernel32.dll")]
    static extern IntPtr GetProcAddress(IntPtr lib, string proc);
    [DllImport("kernel32.dll")]
    static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    delegate IntPtr getJit();
    static AntiTamper()
    {
        Init(RuntimeEnvironment.GetSystemVersion()[1] == '4');
    }

    static IntPtr hookPosition;
    static IntPtr original;
    static compileMethod originalDelegate;

    static bool ver;
    static unsafe void Init(bool ver)
    {
        AntiTamper.ver = ver;
        IntPtr jit = LoadLibrary(ver ? "clrjit.dll" : "mscorjit.dll");
        getJit get = (getJit)Marshal.GetDelegateForFunctionPointer(GetProcAddress(jit, "getJit"), typeof(getJit));
        hookPosition = Marshal.ReadIntPtr(get());
        original = Marshal.ReadIntPtr(hookPosition);

        IntPtr trampoline;
        if (IntPtr.Size == 8)
        {
            trampoline = Marshal.AllocHGlobal(12);
            byte[] dat = Convert.FromBase64String("SLj////////////g");

            uint oldPl;
            VirtualProtect(trampoline, 12, 0x40, out oldPl);
            Marshal.Copy(dat, 0, trampoline, 12);
            Marshal.WriteIntPtr(trampoline, 2, original);
        }
        else
        {
            trampoline = Marshal.AllocHGlobal(7);
            byte[] dat = Convert.FromBase64String("uP//////4A==");

            uint oldPl;
            VirtualProtect(trampoline, 7, 0x40, out oldPl);
            Marshal.Copy(dat, 0, trampoline, 7);
            Marshal.WriteIntPtr(trampoline, 1, original);
        }
        originalDelegate = (compileMethod)Marshal.GetDelegateForFunctionPointer(trampoline, typeof(compileMethod));
        RuntimeHelpers.PrepareDelegate(originalDelegate);
    }

    static byte[] data;

    static bool hooked;
    static compileMethod interop;
    static unsafe void Hook()
    {
        if (hooked) throw new InvalidOperationException();

        interop = new compileMethod(Interop);
        try
        {
            interop(IntPtr.Zero, null, null, 0, null, null);
        }
        catch { }

        uint oldPl;
        VirtualProtect(hookPosition, (uint)IntPtr.Size, 0x40, out oldPl);
        Marshal.WriteIntPtr(hookPosition, Marshal.GetFunctionPointerForDelegate(interop));
        VirtualProtect(hookPosition, (uint)IntPtr.Size, oldPl, out oldPl);

        hooked = true;
    }
    static unsafe void UnHook()
    {
        if (!hooked) throw new InvalidOperationException();

        uint oldPl;
        VirtualProtect(hookPosition, (uint)IntPtr.Size, 0x40, out oldPl);
        Marshal.WriteIntPtr(hookPosition, Marshal.GetFunctionPointerForDelegate(interop));
        VirtualProtect(hookPosition, (uint)IntPtr.Size, oldPl, out oldPl);

        hooked = false;
    }
    static void Dispose(object sender, EventArgs e)
    {
        if (hooked) UnHook();
    }

    unsafe class CorInfoHook
    {
        public IntPtr ftn;
        public CORINFO_EH_CLAUSE[] clauses;
        public getEHinfo od_getEHinfo;
        public getEHinfo h_getEHinfo;
        public ICorMethodInfo* info;
        public IntPtr* oriVfTbl;
        public IntPtr* newVfTbl;
        void hookEHInfo(IntPtr self, IntPtr ftn, uint EHnumber, CORINFO_EH_CLAUSE* clause)
        {
            if (ftn == this.ftn)
            {
                *clause = clauses[EHnumber];
            }
            else
                od_getEHinfo(self, ftn, EHnumber, clause);
        }
        public void Dispose()
        {
            Marshal.FreeHGlobal((IntPtr)newVfTbl);
            info->vfptr = oriVfTbl;
        }

        static int ehNum = -1;
        public static CorInfoHook Hook(ICorJitInfo* comp, IntPtr ftn, CORINFO_EH_CLAUSE[] clauses)
        {
            ICorMethodInfo* mtdInfo = ICorStaticInfo.ICorMethodInfo(ICorDynamicInfo.ICorStaticInfo(ICorJitInfo.ICorDynamicInfo(comp)));
            IntPtr* vfTbl = mtdInfo->vfptr;
            IntPtr* newVfTbl = (IntPtr*)Marshal.AllocHGlobal(0x19 * IntPtr.Size);
            for (int i = 0; i < 0x19; i++)
                newVfTbl[i] = vfTbl[i];
            if (ehNum == -1)
                for (int i = 0; i < 0x19; i++)
                {
                    bool isEh = true;
                    for (byte* func = (byte*)vfTbl[i]; *func != 0xe9; func++)
                        if (IntPtr.Size == 8 ?
                            (*func == 0x48 && *(func + 1) == 0x81 && *(func + 2) == 0xe9) :
                             (*func == 0x83 && *(func + 1) == 0xe9))
                        {
                            isEh = false;
                            break;
                        }
                    if (isEh)
                    {
                        ehNum = i;
                        break;
                    }
                }

            CorInfoHook ret = new CorInfoHook() { ftn = ftn, clauses = clauses, newVfTbl = newVfTbl, oriVfTbl = vfTbl, info = mtdInfo };
            ret.h_getEHinfo = new getEHinfo(ret.hookEHInfo);
            ret.od_getEHinfo = Marshal.GetDelegateForFunctionPointer(vfTbl[ehNum], typeof(getEHinfo)) as getEHinfo;
            newVfTbl[ehNum] = Marshal.GetFunctionPointerForDelegate(ret.h_getEHinfo);
            mtdInfo->vfptr = newVfTbl;
            return ret;
        }
    }

    static unsafe void ParseLocalVars(CORINFO_METHOD_INFO* info, ICorJitInfo* comp, uint localVarToken)
    {
        ICorModuleInfo* modInfo = ICorStaticInfo.ICorModuleInfo(ICorDynamicInfo.ICorStaticInfo(ICorJitInfo.ICorDynamicInfo(comp)));
        findSig findSig = Marshal.GetDelegateForFunctionPointer(modInfo->vfptr[4], typeof(findSig)) as findSig;

        void* sigInfo;

        if (ver)
        {
            if (IntPtr.Size == 8)
                sigInfo = (CORINFO_SIG_INFO_x64*)((uint*)(info + 1) + 5) + 1;
            else
                sigInfo = (CORINFO_SIG_INFO_x86*)((uint*)(info + 1) + 4) + 1;
        }
        else
        {
            if (IntPtr.Size == 8)
                sigInfo = (CORINFO_SIG_INFO_x64*)((uint*)(info + 1) + 3) + 1;
            else
                sigInfo = (CORINFO_SIG_INFO_x86*)((uint*)(info + 1) + 3) + 1;
        }
        findSig((IntPtr)modInfo, info->scope, localVarToken, info->ftn, sigInfo);

        byte* sig;
        if (IntPtr.Size == 8)
            sig = (byte*)((CORINFO_SIG_INFO_x64*)sigInfo)->sig;
        else
            sig = (byte*)((CORINFO_SIG_INFO_x86*)sigInfo)->sig;
        sig++;
        byte b = *sig;
        ushort numArgs;
        IntPtr args;
        if ((b & 0x80) == 0)
        {
            numArgs = b;
            args = (IntPtr)(sig + 1);
        }
        else
        {
            numArgs = (ushort)(((uint)(b & ~0x80) << 8) | *(sig + 1));
            args = (IntPtr)(sig + 2);
        }

        if (IntPtr.Size == 8)
        {
            CORINFO_SIG_INFO_x64* sigInfox64 = (CORINFO_SIG_INFO_x64*)sigInfo;
            sigInfox64->callConv = CorInfoCallConv.DEFAULT;
            sigInfox64->retType = CorInfoType.VOID;
            sigInfox64->flags = 1;
            sigInfox64->numArgs = numArgs;
            sigInfox64->args = args;
        }
        else
        {
            CORINFO_SIG_INFO_x86* sigInfox86 = (CORINFO_SIG_INFO_x86*)sigInfo;
            sigInfox86->callConv = CorInfoCallConv.DEFAULT;
            sigInfox86->retType = CorInfoType.VOID;
            sigInfox86->flags = 1;
            sigInfox86->numArgs = numArgs;
            sigInfox86->args = args;
        }
    }
    static unsafe void Parse(byte* body, CORINFO_METHOD_INFO* info, ICorJitInfo* comp, out CORINFO_EH_CLAUSE[] ehs)
    {
        //Refer to SSCLI
        if ((*body & 0x3) == 0x2)
        {
            if (ver)
            {
                *((uint*)(info + 1) + 0) = 8;   //maxstack
                *((uint*)(info + 1) + 1) = 0;   //ehcount
            }
            else
            {
                *((ushort*)(info + 1) + 0) = 8;
                *((ushort*)(info + 1) + 1) = 0;
            }
            info->ILCode = body + 1;
            info->ILCodeSize = (uint)(*body >> 2);
            ehs = null;
            return;
        }
        else
        {
            ushort flags = *(ushort*)body;
            if (ver)    //maxstack
                *((uint*)(info + 1) + 0) = *(ushort*)(body + 2);
            else
                *((ushort*)(info + 1) + 0) = *(ushort*)(body + 2);
            info->ILCodeSize = *(uint*)(body + 4);
            var localVarTok = *(uint*)(body + 8);
            if ((flags & 0x10) != 0)
            {
                if (ver)    //options
                    *((uint*)(info + 1) + 2) |= (uint)CorInfoOptions.OPT_INIT_LOCALS;
                else
                    *((uint*)(info + 1) + 1) |= (ushort)CorInfoOptions.OPT_INIT_LOCALS;
            }
            info->ILCode = body + 12;

            if (localVarTok != 0)
                ParseLocalVars(info, comp, localVarTok);

            if ((flags & 0x8) != 0)
            {
                body = body + 12 + info->ILCodeSize;
                var list = new ArrayList();
                byte f;
                do
                {
                    body = (byte*)(((uint)body + 3) & ~3);
                    f = *body;
                    uint count;
                    bool isSmall = (f & 0x40) == 0;
                    if (isSmall)
                        count = *(body + 1) / 12u;
                    else
                        count = (*(uint*)body >> 8) / 24;
                    body += 4;

                    for (int i = 0; i < count; i++)
                    {
                        var clause = new CORINFO_EH_CLAUSE();
                        clause.Flags = (CORINFO_EH_CLAUSE_FLAGS)(*body & 0x7);
                        body += isSmall ? 2 : 4;

                        clause.TryOffset = isSmall ? *(ushort*)body : *(uint*)body;
                        body += isSmall ? 2 : 4;
                        clause.TryLength = isSmall ? *(byte*)body : *(uint*)body;
                        body += isSmall ? 1 : 4;

                        clause.HandlerOffset = isSmall ? *(ushort*)body : *(uint*)body;
                        body += isSmall ? 2 : 4;
                        clause.HandlerLength = isSmall ? *(byte*)body : *(uint*)body;
                        body += isSmall ? 1 : 4;

                        clause.ClassTokenOrFilterOffset = *(uint*)body;
                        body += 4;

                        if ((clause.ClassTokenOrFilterOffset & 0xff000000) == 0x1b000000)
                        {
                            if (ver)    //options
                                *((uint*)(info + 1) + 2) |= (uint)CorInfoOptions.GENERICS_CTXT_KEEP_ALIVE;
                            else
                                *((uint*)(info + 1) + 1) |= (ushort)CorInfoOptions.GENERICS_CTXT_KEEP_ALIVE;
                        }

                        list.Add(clause);
                    }
                }
                while ((f & 0x80) != 0);
                ehs = new CORINFO_EH_CLAUSE[list.Count];
                for (int i = 0; i < ehs.Length; i++)
                    ehs[i] = (CORINFO_EH_CLAUSE)list[i];
                if (ver)    //ehcount
                    *((uint*)(info + 1) + 1) = (ushort)ehs.Length;
                else
                    *((ushort*)(info + 1) + 1) = (ushort)ehs.Length;
            }
            else
            {
                ehs = null;
                if (ver)    //ehcount
                    *((uint*)(info + 1) + 1) = 0;
                else
                    *((ushort*)(info + 1) + 1) = 0;
            }
        }
    }
    static unsafe uint Interop(IntPtr self, ICorJitInfo* comp, CORINFO_METHOD_INFO* info, uint flags, byte** nativeEntry, uint* nativeSizeOfCode)
    {
        if (self == IntPtr.Zero) return 0;

        CorInfoHook hook = null;

        if (info != null &&
            (ulong)info->ILCode > s &&
            (ulong)info->ILCode < s + l &&
            info->ILCodeSize == 0x11 &&
            info->ILCode[0] == 0x21 &&
            info->ILCode[9] == 0x20 &&
            info->ILCode[14] == 0x26)
        {
            ulong num = *(ulong*)(info->ILCode + 1);
            uint key = (uint)(num >> 32);
            uint ptr = (uint)(num & 0xFFFFFFFF) ^ key;
            uint len = ~*(uint*)(info->ILCode + 10) ^ key;
            byte* arr = stackalloc byte[(int)len];
            Marshal.Copy(data, (int)ptr, (IntPtr)arr, (int)len);

            byte* kBuff = stackalloc byte[4];
            *(uint*)kBuff = key;
            for (uint i = 0; i < len; i++)
            {
                arr[i] ^= kBuff[i % 4];
            }
            CORINFO_EH_CLAUSE[] ehs;
            Parse(arr, info, comp, out ehs);
            if (ehs != null)
                hook = CorInfoHook.Hook(comp, info->ftn, ehs);
        }

        uint ret = originalDelegate(self, comp, info, flags, nativeEntry, nativeSizeOfCode);

        if (hook != null)
        {
            hook.Dispose();
        }
        return ret;
    }
}