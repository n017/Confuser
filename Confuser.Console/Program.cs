using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;
using Confuser.Core;

namespace Confuser.Console
{
    class Program
    {
        static int Main(string[] args)
        {
            ConsoleColor color = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.White;
            
            WriteLine("Confuser Version " + typeof(Core.Confuser).Assembly.GetName().Version);
            WriteLine();

            try
            {
                if (args.Length != 3 && (args.Length != 5 || args[3] != "-sn"))
                {
                    PrintUsage();
                    return 1;
                }

                if(!File.Exists(args[1]))
                {
                    WriteLineWithColor(ConsoleColor.Red, "ERROR: FILE NOT EXIST!");
                    return 2;
                }

                Marker marker;
                string source;
                if (args[0] == "-assembly")
                {
                    marker = new Marker();
                    source = args[1];
                }
                else if (args[0] == "-config")
                {
                    XDocument doc;
                    try
                    {
                        doc = XDocument.Load(args[1], LoadOptions.SetLineInfo | LoadOptions.SetBaseUri);
                    }
                    catch
                    {
                        WriteLineWithColor(ConsoleColor.Red, "ERROR: INVAILD XML!");
                        return 3;
                    }
                    XmlMarker mkr;
                    if (!XmlMarker.Create(doc, out mkr))
                    {
                        WriteLineWithColor(ConsoleColor.Red, "ERROR: INVAILD CONFIGURATION!");
                        return 4;
                    }
                    marker = mkr;
                    source = "";
                }
                else
                {
                    PrintUsage();
                    return 1;
                }

                Core.Confuser cr = new Confuser.Core.Confuser();
                ConfuserParameter param = new ConfuserParameter();

                List<IConfusion> cions = new List<IConfusion>();
                List<Packer> packs = new List<Packer>();
                foreach (Type type in typeof(IConfusion).Assembly.GetTypes())
                {
                    if (typeof(Core.IConfusion).IsAssignableFrom(type) && type != typeof(Core.IConfusion))
                        cions.Add(Activator.CreateInstance(type) as Core.IConfusion);
                    if (typeof(Core.Packer).IsAssignableFrom(type) && type != typeof(Core.Packer))
                        packs.Add(Activator.CreateInstance(type) as Core.Packer);
                }
                param.Confusions = cions.ToArray();
                param.Packers = packs.ToArray();
                param.DestinationPath = args[2];
                param.DefaultPreset = Preset.None;
                param.Marker = marker;
                param.ReferencesPath = string.IsNullOrEmpty(source) ? "" : Path.GetDirectoryName(source);
                param.SourceAssembly = source;
                param.StrongNameKeyPath = args.Length == 5 ? args[4] : null;
                ConsoleLogger.Initalize(param.Logger);
                WriteLine("START WORKING.");
                WriteLine(new string('*', 15));
                cr.Confuse(param);

                return 0;
            }
            finally
            {
                System.Console.ForegroundColor = color;
            }
        }

        static void PrintUsage()
        {
            WriteLine("Usage:");
            WriteLine("Confuser.Console.exe [-assembly <source assembly>|-config <configuration file>] <target path> [-sn <strong key pair path>]");
        }

        static void WriteLineWithColor(ConsoleColor color, string txt)
        {
            ConsoleColor clr = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
            System.Console.WriteLine(txt);
            System.Console.ForegroundColor = clr;
        }
        static void WriteLine(string txt)
        {
            System.Console.WriteLine(txt);
        }
        static void WriteLine()
        {
            System.Console.WriteLine();
        }
    }
}
