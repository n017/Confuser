using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.IO;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using Mono.Cecil.Cil;

namespace Confuser.Core
{
    public class PackerParameter
    {
        ModuleDefinition mod = null;
        byte[] pe = null;
        NameValueCollection parameters = new NameValueCollection();

        public ModuleDefinition Module { get { return mod; } internal set { mod = value; } }
        public byte[] PE { get { return pe; } internal set { pe = value; } }
        public NameValueCollection Parameters { get { return parameters; } internal set { parameters = value; } }
    }

    public abstract class Packer
    {
        class PackerMarker : Marker
        {
            ModuleDefinition origin;
            public PackerMarker(ModuleDefinition mod) { origin = mod; }

            public override void MarkAssembly(AssemblyDefinition asm, Preset preset)
            {
                base.MarkAssembly(asm, preset);

                IAnnotationProvider m = asm;
                m.Annotations.Clear();
                IAnnotationProvider src = (IAnnotationProvider)origin.Assembly;
                foreach (object key in src.Annotations.Keys)
                    m.Annotations[key] = src.Annotations[key];
            }
            protected override void MarkModule(ModuleDefinition mod, IDictionary<IConfusion, NameValueCollection> current)
            {
                IAnnotationProvider m = mod;
                m.Annotations.Clear();
                IAnnotationProvider src = (IAnnotationProvider)origin;
                foreach (object key in src.Annotations.Keys)
                    m.Annotations.Add(key, src.Annotations[src]);

                var dict = (IDictionary<IConfusion, NameValueCollection>)src.Annotations["ConfusionSets"];
                current.Clear();
                foreach (var i in dict)
                    current.Add(i.Key, i.Value);
            }
        }

        public abstract string ID { get; }
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract bool StandardCompatible { get; }
        Confuser cr;
        internal Confuser Confuser { get { return cr; } set { cr = value; } }
        protected void Log(string message) { cr.Log(message); }

        public byte[] Pack(ConfuserParameter crParam, PackerParameter param)
        {
            ModuleDefinition mod;
            PackCore(out mod, param);

            string tmp = Path.GetTempPath() + "\\" + Path.GetRandomFileName() + "\\";
            Directory.CreateDirectory(tmp);
            mod.Write(tmp + mod.Name);

            Confuser cr = new Confuser();
            ConfuserParameter par = new ConfuserParameter();
            par.SourceAssembly = tmp + mod.Name;
            par.ReferencesPath = tmp;
            tmp = Path.GetTempPath() + "\\" + Path.GetRandomFileName() + "\\";
            par.DestinationPath = tmp;
            par.Confusions = crParam.Confusions;
            par.DefaultPreset = crParam.DefaultPreset;
            par.StrongNameKeyPath = crParam.StrongNameKeyPath;
            par.Marker = new PackerMarker(param.Module);
            cr.Confuse(par);

            return File.ReadAllBytes(tmp + mod.Name);
        }
        protected abstract void PackCore(out ModuleDefinition mod, PackerParameter parameter);
    }
}