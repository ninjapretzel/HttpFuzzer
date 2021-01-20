using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Harness {
    class Program {
        static void Main(string[] args) {
            if (args.Length < 1) { Console.WriteLine("Please provide app name to run."); return; }
            string dirName = args[0];
            string targetDir = $"../Apps/{dirName}";
            if (Directory.Exists(targetDir)) {
                Directory.SetCurrentDirectory(targetDir);
			}

            
        }

        public static string platform { get; private set; } = Init();
        public static string shell { get; private set; }
        public static string prefix { get; private set; }
        static string Init() {
            string platform = System.Environment.OSVersion.Platform.ToString();
            if (platform == "Win32NT") {
                shell = "C:/Windows/System32/cmd.exe";
                prefix = "/C";
            } else if (platform == "Unix") {
                shell = "/bin/bash";
                prefix = "-c";
            }
            return platform;
        }


        static void Run(string cmd) {
            ProcessStartInfo info = new ProcessStartInfo(shell, $"{prefix} '{cmd}'") {
                UseShellExecute = false
            };
            Process p = new Process() { StartInfo = info };
            p.Start();
            p.WaitForExit();
        }

    }
}
