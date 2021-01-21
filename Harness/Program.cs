using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Harness {
	public class Program {

		public static string SourceFileDirectory([CallerFilePath] string callerPath = "[NO PATH]") {
			callerPath = ForwardSlashPath(callerPath);
			return callerPath.Substring(0, callerPath.LastIndexOf('/'));
		}

		public static string TopSourceFileDirectory() { return SourceFileDirectory(); }

		/// <summary> Convert a file or folder path to only contain forward slashes '/' instead of backslashes '\'. </summary>
		/// <param name="path"> Path to convert </param>
		/// <returns> <paramref name="path"/> with all '\' characters replaced with '/' </returns>
		private static string ForwardSlashPath(string path) {
			string s = path.Replace('\\', '/');
			return s;
		}


		static void Main(string[] args) {
			if (args.Length < 1) { Console.WriteLine("Please provide app name to run."); return; }

			string dirName = args[0];
			string targetDir = $"../Apps/{dirName}";
			
			if (Directory.Exists(targetDir)) {
				Directory.SetCurrentDirectory(targetDir);
			} else {
				Console.WriteLine($"No directory {targetDir} exists.");
				return;
			}

			if (!File.Exists("spec.json")) {
				Console.WriteLine($"No file 'spec.json' found in {targetDir}.");
				Console.WriteLine($"See 'spec-example.json' in {TopSourceFileDirectory()}.");
				return;
			}

			string json = File.ReadAllText("spec.json");
			JsonObject settings = Json.Parse<JsonObject>(json);
			if (settings == null) {
				Console.WriteLine($"'spec.json' must contain a Json Object.");
				Console.WriteLine($"See 'spec-example.json' in {TopSourceFileDirectory()}.");
				return;
			}

			Task<int> waiter = AsyncMain(settings);
			Console.WriteLine($"Final exit code: {waiter.Result}");
		}

		public static async Task<int> AsyncMain(JsonObject settings) {
			string wdir = ForwardSlashPath(Directory.GetCurrentDirectory());
			string build = settings.Get<string>("build");
			string buildArgs = settings.Get<string>("buildArgs");
			string run = settings.Get<string>("run");
			string runArgs = settings.Get<string>("runArgs");

			if (build != null && build.Trim().Length > 0) {
				Process built = await Run(build, buildArgs, wdir);
			}

			while (true) {
				try {
					Process finished = await Run(run, runArgs, wdir);
					if (finished.ExitCode == 31337) {
						return 31337;
					}
					Console.WriteLine($"[{run}] exited with code {finished.ExitCode}");
				} catch (Exception e) {
					return -1;
				}
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


		static async Task<Process> Run(string cmd, string args = null, string folder = null) {
			if (args == null) { args = ""; }
			Console.WriteLine($"{folder} $ {cmd} {args}");
			//ProcessStartInfo info = new ProcessStartInfo(shell, $"{prefix} '{cmd}'") {
			ProcessStartInfo info = new ProcessStartInfo(cmd, args) {
				UseShellExecute = false,
				//RedirectStandardInput = true,
				//RedirectStandardOutput  = true,
				//RedirectStandardError = true,
				
			};
			if (folder != null) {
				info.WorkingDirectory = folder;
			}

			Process p = new Process() { StartInfo = info, };
			p.Start();
			while (!p.HasExited) { await Task.Delay(1);  }

			return p;
		}

	}
}
