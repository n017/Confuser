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
        static int ParseCommandLine(string[] args, out ConfuserProject proj)
        {
            proj = new ConfuserProject();
            for (int i = 0; i < args.Length; i++)
            {
                string action = args[i].ToLower();
                if (!action.StartsWith("-") || i + 1 >= args.Length)
                {
                    WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Invalid argument {0}!", action));
                    return 3;
                }
                action = action.Substring(1).ToLower();
                switch (action)
                {
                    case "project":
                        {
                            if (!File.Exists(args[i + 1]))
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: File '{0}' not exist!", args[i + 1]));
                                return 2;
                            }
                            XmlDocument xmlDoc = new XmlDocument();
                            xmlDoc.Load(args[i + 1]);
                            proj.Load(xmlDoc);
                            i += 1;
                        } break;
                    case "preset":
                        {
                            try
                            {
                                proj.DefaultPreset = (Preset)Enum.Parse(typeof(Preset), args[i + 1], true);
                                i += 1;
                            }
                            catch
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Invalid preset '{0}'!", args[i + 1]));
                                return 3;
                            }
                        } break;
                    case "input":
                        {
                            int parameterCounter = i + 1;

                            for (int j = i + 1; j < args.Length && !args[j].StartsWith("-"); j++)
                            {
                                parameterCounter = j;
                                string inputParameter = args[j];

                                int lastBackslashPosition = inputParameter.LastIndexOf('\\') + 1;
                                string filename = inputParameter.Substring(lastBackslashPosition, inputParameter.Length - lastBackslashPosition);
                                string path = inputParameter.Substring(0, lastBackslashPosition);

                                try
                                {
                                    string[] fileList = Directory.GetFiles(path, filename);
                                    if (fileList.Length == 0)
                                    {
                                        WriteLineWithColor(ConsoleColor.Red, string.Format("Error: No files matching '{0}' in directory '{1}'!", filename));
                                        return 2;
                                    }
                                    else if (fileList.Length == 1)
                                    {
                                        proj.Add(new ProjectAssembly() { Path = fileList[0],
                                                                         IsMain = j == i + 1 && filename.Contains('?') == false && filename.Contains('*') == false});
                                    }
                                    else
                                    {
                                        foreach (string expandedFilename in fileList)
                                        {
                                            proj.Add(new ProjectAssembly() { Path = expandedFilename, IsMain = false });
                                        }
                                    }
                                }
                                catch (DirectoryNotFoundException)
                                {
                                    WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Directory '{0}' does not exist!", path));
                                    return 2;
                                }
                            }
                            i = parameterCounter;
                        } break;
                    case "output":
                        {
                            if (!Directory.Exists(args[i + 1]))
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: Directory '{0}' not exist!", args[i + 1]));
                                return 2;
                            }
                            proj.OutputPath = args[i + 1];
                            i += 1;
                        } break;
                    case "snkey":
                        {
                            if (!File.Exists(args[i + 1]))
                            {
                                WriteLineWithColor(ConsoleColor.Red, string.Format("Error: File '{0}' not exist!", args[i + 1]));
                                return 2;
                            }
                            proj.SNKeyPath = args[i + 1];
                            i += 1;
                        } break;
                }
            }

            if (proj.Count == 0 || string.IsNullOrEmpty(proj.OutputPath))
            {
                WriteLineWithColor(ConsoleColor.Red, "Error: Missing required arguments!");
                return 4;
            }


            return 0;
        }

        static int Main(string[] args)
        {
            ConsoleColor color = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.White;

            WriteLine("Confuser Version v" + typeof(Core.Confuser).Assembly.GetName().Version);
            WriteLine();

#if DEBUG
            for (int i = 0; i < 3; i++)
            {
                System.Console.Write('.');
                System.Threading.Thread.Sleep(1000);
            }
            WriteLine();
#endif


            try
            {
                if (args.Length < 2 || args[0] == "-help")
                {
                    PrintUsage();
                    return 0;
                }

                ConfuserProject proj;
                int error = ParseCommandLine(args, out proj);
                if (error != 0)
                {
                    return error;
                }

                Core.Confuser cr = new Core.Confuser();
                ConfuserParameter param = new ConfuserParameter();
                param.Project = proj;
                ConsoleLogger.Initalize(param.Logger);
                WriteLine("Start working.");
                WriteLine(new string('*', 15));
                cr.Confuse(param);

                return ConsoleLogger.ReturnValue;
            }
            finally
            {
                System.Console.ForegroundColor = color;
            }
        }

        static void PrintUsage()
        {
            WriteLine("Usage:");
            WriteLine("Confuser.Console.exe [-project <configuration file> | -preset <preset> -snkey <strong name key> -output <output directory> -input <input files>]");
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
