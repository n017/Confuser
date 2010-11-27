using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Confuser.Core;

namespace Confuser.Console
{
    static class ConsoleLogger
    {
        public static int ReturnValue = -1;
        public static void Initalize(Logger logger)
        {
            logger.BeginPhase += BeginPhase;
            logger.Logging += Logging;
            logger.Progressing += Progressing;
            logger.Fault += Fault;
            logger.End += End;
        }

        static void BeginPhase(object sender, PhaseEventArgs e)
        {
            switch (e.Phase)
            {
                case 1:
                    WriteLineWithColor(ConsoleColor.Yellow, "Initalizing..."); break;
                case 2:
                    WriteLineWithColor(ConsoleColor.Yellow, "Obfuscating Structure..."); break;
                case 3:
                    WriteLineWithColor(ConsoleColor.Yellow, "Obfuscating Metadata..."); break;
                case 4:
                    WriteLineWithColor(ConsoleColor.Yellow, "Processing PE..."); break;
                default:
                    WriteLineWithColor(ConsoleColor.Yellow, "Running Phase " + e.Phase + "..."); break;
            }
        }
        static void Logging(object sender, LogEventArgs e)
        {
            WriteLine(e.Message);
        }
        static void Progressing(object sender, ProgressEventArgs e)
        {
            //
        }
        static void Fault(object sender, ExceptionEventArgs e)
        {
            WriteLineWithColor(ConsoleColor.Red, new string('*', 15));
            WriteLineWithColor(ConsoleColor.Red, "ERROR!!");
            WriteLineWithColor(ConsoleColor.Red, e.Exception.Message);
            WriteLineWithColor(ConsoleColor.Red, e.Exception.StackTrace);
            WriteLineWithColor(ConsoleColor.Red, new string('*', 15));
            ReturnValue = System.Runtime.InteropServices.Marshal.GetHRForException(e.Exception);
        }
        static void End(object sender, LogEventArgs e)
        {
            WriteLineWithColor(ConsoleColor.Green, new string('*', 15));
            WriteLineWithColor(ConsoleColor.Green, "SUCCESSED!!");
            WriteLineWithColor(ConsoleColor.Green, e.Message);
            WriteLineWithColor(ConsoleColor.Green, new string('*', 15));
            ReturnValue = 0;
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
