using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using Mono.Cecil;
using Confuser.Core;
using System.Reflection;
using System.Windows;

namespace Confuser
{
    public class ConfuserDatas
    {
        static ConfuserDatas()
        {
            LoadAssembly(typeof(IConfusion).Assembly, false);
        }

        public static readonly ObservableCollection<IConfusion> Confusions = new ObservableCollection<IConfusion>();
        public static readonly ObservableCollection<Packer> Packers = new ObservableCollection<Packer>();
        public static void LoadAssembly(Assembly asm, bool interact)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Loaded type :");
            bool h = false;
            foreach (Type type in asm.GetTypes())
            {
                if (typeof(Core.IConfusion).IsAssignableFrom(type) && type != typeof(Core.IConfusion))
                {
                    Confusions.Add(Activator.CreateInstance(type) as Core.IConfusion);
                    sb.AppendLine(type.FullName);
                    h = true;
                }
                if (typeof(Core.Packer).IsAssignableFrom(type) && type != typeof(Core.Packer))
                {
                    Packers.Add(Activator.CreateInstance(type) as Core.Packer);
                    sb.AppendLine(type.FullName);
                    h = true;
                }
            }
            if (!h) sb.AppendLine("NONE!");
            if (interact)
                MessageBox.Show(sb.ToString(), "Confuser", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        public AssemblyDefinition[] Assemblies { get; set; }
        public string StrongNameKey { get; set; }
        public string OutputPath { get; set; }
        public ConfuserParameter Parameter { get; set; }
        public string Summary { get; set; }
    }
}