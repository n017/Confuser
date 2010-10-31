using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Security;
using System.Security.Permissions;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace Confuser.Core
{
    class Compressor:Packer
    {
        public static void Compress(Confuser cr, byte[] asm, ModuleDefinition mod, Stream dst)
        {
            if (mod.Kind == ModuleKind.Dll || mod.Kind == ModuleKind.NetModule)
            {
                dst.Write(asm, 0, asm.Length);
                return;
            }
            AssemblyDefinition ldr = AssemblyDefinition.CreateAssembly(mod.Assembly.Name, mod.Name, ModuleKind.Windows);

            EmbeddedResource res = new EmbeddedResource(Encoding.UTF8.GetString(Guid.NewGuid().ToByteArray()), ManifestResourceAttributes.Public, Encrypt(asm));
            ldr.MainModule.Resources.Add(res);

            AssemblyDefinition ldrC = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
            TypeDefinition t = CecilHelper.Inject(ldr.MainModule, ldrC.MainModule.GetType("CompressShell"));
            foreach (Instruction inst in t.GetStaticConstructor().Body.Instructions)
                if (inst.Operand is string)
                    inst.Operand = res.Name;
            t.Namespace = "";
            t.DeclaringType = null;
            t.IsNestedPrivate = false;
            t.IsNotPublic = true;
            ldr.MainModule.Types.Add(t);
            ldr.EntryPoint = t.Methods.FirstOrDefault(mtd => mtd.Name == "Main");

            ldr.Write(dst);
        }

        static byte[] Encrypt(byte[] asm)
        {
            MemoryStream str = new MemoryStream();
            using (BinaryWriter wtr = new BinaryWriter(new DeflateStream(str, CompressionMode.Compress)))
            {
                wtr.Write(asm);
            }
            byte[] ret = str.ToArray();
            for (int i = 0; i < ret.Length; i++)
            {
                ret[i] = (byte)(ret[i] ^ i);
            }
            return ret;
        }

        public override string ID
        {
            get { return "compressor"; }
        }

        public override string Name
        {
            get { return "Compressing Packer"; }
        }

        public override string Description
        {
            get { return "Reduce the size of output"; }
        }

        public override bool StandardCompatible
        {
            get { return true; ; }
        }

        protected override void PackCore(out ModuleDefinition mod, PackerParameter parameter)
        {
            if (parameter.Module.Kind == ModuleKind.Dll || parameter.Module.Kind == ModuleKind.NetModule)
            {
                mod = parameter.Module;
                return;
            }
            mod = ModuleDefinition.CreateModule(parameter.Module.Name, new ModuleParameters() { Architecture = parameter.Module.Architecture, Kind = parameter.Module.Kind, Runtime = parameter.Module.Runtime });

            EmbeddedResource res = new EmbeddedResource(Encoding.UTF8.GetString(Guid.NewGuid().ToByteArray()), ManifestResourceAttributes.Public, Encrypt(parameter.PE));
            mod.Resources.Add(res);

            AssemblyDefinition ldrC = AssemblyDefinition.ReadAssembly(typeof(Iid).Assembly.Location);
            TypeDefinition t = CecilHelper.Inject(mod, ldrC.MainModule.GetType("CompressShell"));
            foreach (Instruction inst in t.GetStaticConstructor().Body.Instructions)
                if (inst.Operand is string)
                    inst.Operand = res.Name;
            t.Namespace = "";
            t.DeclaringType = null;
            t.IsNestedPrivate = false;
            t.IsNotPublic = true;
            mod.Types.Add(t);
            mod.EntryPoint = t.Methods.FirstOrDefault(mtd => mtd.Name == "Main");
        }
    }
}
