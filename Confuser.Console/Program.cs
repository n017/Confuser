using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Confuser.Core;
using Confuser.Core.Project;
using System.Xml;

namespace Confuser.Console
{
    class Program
    {
        static int Main(string[] args)
        {
            ConsoleColor color = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.White;

            WriteLine("Confuser Version v" + typeof(Core.Confuser).Assembly.GetName().Version);
            WriteLine();

            try
            {
                if (args.Length != 1)
                {
                    PrintUsage();
                    return 1;
                }

                if (!File.Exists(args[0]))
                {
                    WriteLineWithColor(ConsoleColor.Red, "ERROR: FILE NOT EXIST!");
                    return 2;
                }

                var proj = new ConfuserProject();
                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(args[0]);
                proj.Load(xmlDoc);

                Core.Confuser cr = new Core.Confuser();
                ConfuserParameter param = new ConfuserParameter();
                param.Project = proj;
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
            WriteLine("Confuser.Console.exe [configuration file]");
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
