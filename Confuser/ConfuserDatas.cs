using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using Mono.Cecil;
using Confuser.Core;
using System.Reflection;

namespace Confuser
{
    public class ConfuserDatas
    {
        static ConfuserDatas()
        {
            LoadAssembly(typeof(IConfusion).Assembly);
        }

        public static readonly ObservableCollection<IConfusion> Confusions = new ObservableCollection<IConfusion>();
        public static readonly ObservableCollection<Packer> Packers = new ObservableCollection<Packer>();
        public static void LoadAssembly(Assembly asm)
        {
            foreach (Type type in asm.GetTypes())
            {
                if (typeof(Core.IConfusion).IsAssignableFrom(type) && type != typeof(Core.IConfusion))
                    Confusions.Add(Activator.CreateInstance(type) as Core.IConfusion);
                if (typeof(Core.Packer).IsAssignableFrom(type) && type != typeof(Core.Packer))
                    Packers.Add(Activator.CreateInstance(type) as Core.Packer);
            }
        }


        public AssemblyDefinition[] Assemblies { get; set; }
        public string StrongNameKey { get; set; }
        public string OutputPath { get; set; }
        public ConfuserParameter Parameter { get; set; }
        public string Summary { get; set; }
    }
}