using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;
using Mono.Cecil.PE;

namespace Confuser.Core
{
    public class PackerParameter
    {
        ModuleDefinition[] mods = null;
        byte[][] pes = null;
        NameValueCollection parameters = new NameValueCollection();

        public ModuleDefinition[] Modules { get { return mods; } internal set { mods = value; } }
        public byte[][] PEs { get { return pes; } internal set { pes = value; } }
        public NameValueCollection Parameters { get { return parameters; } internal set { parameters = value; } }
    }

    public abstract class Packer
    {
        class PackerMarker : Marker
        {
            AssemblySetting origin;
            public PackerMarker(AssemblySetting origin) { this.origin = origin; }

            protected override AssemblySetting MarkAssembly(AssemblyDefinition asm, Marking mark, out bool exclude)
            {
                mark.CurrentConfusions.Clear();
                AssemblySetting ret = new AssemblySetting(asm);
                foreach (var i in origin.GlobalParameters)
                    mark.CurrentConfusions.Add(i.Key, i.Value);
                //ret.Packer = origin.Packer;
                //ret.PackerParameters = origin.PackerParameters;
                exclude = false;
                return ret;
            }
            protected override ModuleSetting MarkModule(ModuleDefinition mod, Marking mark, out bool exclude)
            {
                mark.CurrentConfusions.Clear();
                ModuleSetting ret = new ModuleSetting(mod);
                foreach (var i in origin.Modules[0].Parameters)
                    mark.CurrentConfusions.Add(i.Key, i.Value);
                exclude = false;
                return ret;
            }
        }

        public abstract string ID { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract bool StandardCompatible { get; }
        Confuser cr;
        internal Confuser Confuser { get { return cr; } set { cr = value; } }
        protected void Log(string message) { cr.Log(message); }

        public string[] Pack(ConfuserParameter crParam, PackerParameter param)
        {
            AssemblyDefinition asm;
            PackCore(out asm, param);

            string tmp = Path.GetTempPath() + "\\" + Path.GetRandomFileName() + "\\";
            Directory.CreateDirectory(tmp);
            MetadataProcessor psr = new MetadataProcessor();
            Section oldRsrc = null;
            foreach (Section s in param.Modules[0].GetSections())
                if (s.Name == ".rsrc") { oldRsrc = s; break; }
            if (oldRsrc != null)
            {
                psr.ProcessImage += accessor =>
                {
                    Section sect = null;
                    foreach (Section s in accessor.Sections)
                        if (s.Name == ".rsrc") { sect = s; break; }
                    if (sect == null)
                    {
                        sect = new Section()
                        {
                            Name = ".rsrc",
                            Characteristics = 0x40000040
                        };
                        foreach (Section s in accessor.Sections)
                            if (s.Name == ".text") { accessor.Sections.Insert(accessor.Sections.IndexOf(s) + 1, sect); break; }
                    }
                    sect.VirtualSize = oldRsrc.VirtualSize;
                    sect.SizeOfRawData = oldRsrc.PointerToRawData;
                    int idx = accessor.Sections.IndexOf(sect);
                    sect.VirtualAddress = accessor.Sections[idx - 1].VirtualAddress + ((accessor.Sections[idx - 1].VirtualSize + 0x2000U - 1) & ~(0x2000U - 1));
                    sect.PointerToRawData = accessor.Sections[idx - 1].PointerToRawData + accessor.Sections[idx - 1].SizeOfRawData;
                    for (int i = idx + 1; i < accessor.Sections.Count; i++)
                    {
                        accessor.Sections[i].VirtualAddress = accessor.Sections[i - 1].VirtualAddress + ((accessor.Sections[i - 1].VirtualSize + 0x2000U - 1) & ~(0x2000U - 1));
                        accessor.Sections[i].PointerToRawData = accessor.Sections[i - 1].PointerToRawData + accessor.Sections[i - 1].SizeOfRawData;
                    }
                    ByteBuffer buff = new ByteBuffer(oldRsrc.Data);
                    PatchResourceDirectoryTable(buff, oldRsrc, sect);
                    sect.Data = buff.GetBuffer();
                };
            }
            psr.Process(asm.MainModule, tmp + asm.MainModule.Name);

            Confuser cr = new Confuser();
            ConfuserParameter par = new ConfuserParameter();
            par.SourceAssemblies = new string[] { tmp + asm.MainModule.Name };
            tmp = Path.GetTempPath() + "\\" + Path.GetRandomFileName() + "\\";
            par.DestinationPath = tmp;
            par.Confusions = crParam.Confusions;
            par.DefaultPreset = crParam.DefaultPreset;
            par.StrongNameKeyPath = crParam.StrongNameKeyPath;
            par.Marker = new PackerMarker(cr.settings[0]);
            cr.Confuse(par);

            return Directory.GetFiles(tmp);
        }
        protected abstract void PackCore(out AssemblyDefinition asm, PackerParameter parameter);

        static void PatchResourceDirectoryTable(ByteBuffer resources, Section old, Section @new)
        {
            resources.Advance(12);
            int num = resources.ReadUInt16() + resources.ReadUInt16();
            for (int i = 0; i < num; i++)
            {
                PatchResourceDirectoryEntry(resources, old, @new);
            }
        }
        static void PatchResourceDirectoryEntry(ByteBuffer resources, Section old, Section @new)
        {
            resources.Advance(4);
            uint num = resources.ReadUInt32();
            int position = resources.Position;
            resources.Position = ((int)num) & 0x7fffffff;
            if ((num & 0x80000000) != 0)
            {
                PatchResourceDirectoryTable(resources, old, @new);
            }
            else
            {
                PatchResourceDataEntry(resources, old, @new);
            }
            resources.Position = position;
        }
        static void PatchResourceDataEntry(ByteBuffer resources, Section old, Section @new)
        {
            uint num = resources.ReadUInt32();
            resources.Position -= 4;
            resources.WriteUInt32(num - old.VirtualAddress + @new.VirtualAddress);
        }
    }
}