using MiniHttp;
using static MiniHttp.ProvidedMiddleware;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using Ex;

namespace Harness {
	public class Program {

		public static string SourceFileDirectory([CallerFilePath] string callerPath = "[NO PATH]") {
			callerPath = ForwardSlashPath(callerPath);
			return callerPath.Substring(0, callerPath.LastIndexOf('/'));
		}

		public static string UncleanSourceFileDirectory([CallerFilePath] string callerPath = "[NO PATH]") {
			return callerPath.Substring(0, callerPath.Replace('\\', '/').LastIndexOf('/'));
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

			SetupLogger();

			HttpServer server = StartServer();
			Log.Info("Test Harness listening on http://localhost:31337");

			Task<int> waiter = AsyncMain(settings);
			Log.Info($"Final exit code: {waiter.Result}");

			Log.Stop();

		}

		public static async Task<int> AsyncMain(JsonObject settings) {
			string wdir = ForwardSlashPath(Directory.GetCurrentDirectory());
			string build = settings.Get<string>("build");
			string run = settings.Get<string>("run");

			if (build != null && build.Trim().Length > 0) {
				Process built = await Run(build, wdir);
			}

			while (true) {
				try {
					Process finished = await Run(run, wdir);
					
					Log.Warning($"Potential Crash. [{run}] exited with code {finished.ExitCode}");
				} catch (Exception) {
					return -1;
				}
			}
		}

		private static void SetupLogger() {
			Log.ignorePath = UncleanSourceFileDirectory();
			Log.fromPath = "Harness";
			Log.defaultTag = "Ex";
			LogLevel target = LogLevel.Info;

			Log.logHandler += (info) => {
				// Console.WriteLine($"{info.tag}: {info.message}");
				if (info.level <= target) {
					//Console.WriteLine($"\n{info.tag}: {info.message}\n");
					Pretty.Print($"\n{info.tag}: {info.message}\n");
				}
			};

			// Todo: Change logfile location when deployed
			// Log ALL messages to file.
			string logfolder = $"{SourceFileDirectory()}/logs";
			if (!Directory.Exists(logfolder)) { Directory.CreateDirectory(logfolder); }
			string logfile = $"{logfolder}/{DateTime.UtcNow.UnixTimestamp()}.log";
			Log.logHandler += (info) => {
				File.AppendAllText(logfile, $"\n{info.tag}: {info.message}\n");
			};

		}

		public static string NextTest;
		public static HttpServer StartServer() {
			List<Middleware> middleware = new List<Middleware>();

			middleware.Add(BodyParser);
			Router router = new Router();
			router.Post("/test", async (ctx,next) => { 
				JsonObject obj = ctx.req.bodyObj;
				if (obj != null && obj.Has<JsonString>("name")) {
					Interlocked.Exchange(ref NextTest, obj.Get<string>("name"));
					ctx.body = "{\"success\":true}";
					Log.Info($"Beginning test \"{NextTest}\"");
				} else {
					ctx.body = "{\"success\":false}";
				}
			});
			middleware.Add(router);



			HttpServer server = new HttpServer("http://localhost:31337", middleware.ToArray());
			return server;
		}


		public static string platform { get; private set; } = Init();
		public static string shell { get; private set; }
		public static string prefix { get; private set; }
		static string Init() {
			string platform = System.Environment.OSVersion.Platform.ToString();
			if (platform == "Win32NT") {
				shell = @"C:\Windows\System32\cmd.exe";
				prefix = "/C";
			} else if (platform == "Unix") {
				shell = "/bin/bash";
				prefix = "-c";
			}
			return platform;
		}

		static async Task<Process> Run(string cmd, string folder = null) {
			Log.Info($"{folder} $> {cmd}");
			ProcessStartInfo info = new ProcessStartInfo(shell, $"{prefix} \"{cmd}\"") {
				UseShellExecute = false,
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
