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
	public static class Program {

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

		public static volatile bool continueRunning = false;
		public static volatile string NextTest = "Unnamed";
		public static volatile Process currentProcess = null;
		
		public static readonly JsonObject settings = new JsonObject();


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
			JsonObject sets = Json.Parse<JsonObject>(json);
			if (sets == null) {
				Console.WriteLine($"'spec.json' must contain a Json Object.");
				Console.WriteLine($"See 'spec-example.json' in {TopSourceFileDirectory()}.");
				return;
			}
			settings.Set(sets);
			SetupLogger();

			HttpServer server = StartServer();
			Log.Info("Test Harness listening on http://localhost:31337");

			
			Task<int> waiter = AsyncMain();
			Log.Info($"Final exit code: {waiter.Result}");

			Log.Stop();

		}

		public static async Task<Process> Build() {
			if (buildCmd != null && buildCmd.Trim().Length > 0) {
				Process built = await StartProcess(buildCmd, wdir);
				return built;
			}
			return null;
		}
		public static async Task<Process> Run() {
			Process process = await StartProcess(runCmd, wdir);
			return process;
		}
		public static async Task<int> AsyncMain() {
			Process built = await Build();
			await built.WaitForExitAsync();
			/*if (built == null || !built.HasExited) {
				Log.Warning($"Build command [{buildCmd}] failed.");
				return -1;
			}*/

			while (true) {
				try {
					Process process = await Run();
					while (!process.HasExited && continueRunning) { await Task.Delay(1); }

					if (process.HasExited) {
						Log.Warning($"Potential Crash. [{runCmd}] exited with code {process.ExitCode}");

					} else {
						Log.Info($"Restart probably requested. [{runCmd}] was pre-empted by fuzzer.");
						process.Kill(true);
						await process.WaitForExitAsync();
					}
					
				} catch (Exception) {
					return -1;
				}
				// await Task.Delay(100);
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
			router.Post("/restart", async (ctx, next) => {
				Process cur = currentProcess;
				continueRunning = false;
				// Wait for main to swap out to a new process...
				while (cur == currentProcess) {
					await Task.Delay(1);
				}
				// Todo: Adjust for startup time
				await Task.Delay(1); 
				ctx.body = "{\"success\":true}";
			});
			middleware.Add(router);



			HttpServer server = new HttpServer("http://localhost:31337", middleware.ToArray());
			return server;
		}


		public static PlatformID platformId { get; private set; }
		public static string platform { get; private set; } = InitPlatform();
		public static string shell { get; private set; }
		public static string prefix { get; private set; }

		public static string buildCmd {
			get {
				if (platformId == PlatformID.Win32NT && settings.Has("buildWindows")) { return settings.Get<string>("buildWindows"); }
				if (platformId == PlatformID.Unix && settings.Has("buildLinux")) { return settings.Get<string>("buildLinux"); }
				return settings.Get<string>("build");
			}
		}
		public static string runCmd { 
			get {
				if (platformId == PlatformID.Win32NT && settings.Has("runWindows")) { return settings.Get<string>("runWindows"); }
				if (platformId == PlatformID.Unix && settings.Has("runLinux")) { return settings.Get<string>("runLinux"); }
				return settings.Get<string>("run"); 
			}
		}
		public static string wdir { get { return ForwardSlashPath(Directory.GetCurrentDirectory()); } }

		static string InitPlatform() {
			platformId = Environment.OSVersion.Platform;
			if (platformId == PlatformID.Win32NT) {
				shell = @"C:\Windows\System32\cmd.exe";
				prefix = "/C";
			} else if (platformId == PlatformID.Unix) {
				shell = "/bin/bash";
				prefix = "-c";
			}
			return platformId.ToString();
		}

		static async Task<Process> StartProcess(string cmd, string folder = null) {
			Log.Info($"{folder} $> {cmd}");
			continueRunning = true;
			ProcessStartInfo info = new ProcessStartInfo(shell, $"{prefix} \"{cmd}\"") {
				UseShellExecute = false,
			};
			if (folder != null) {
				info.WorkingDirectory = folder;
			}

			Process p = new Process() { StartInfo = info, };
			Interlocked.Exchange(ref currentProcess, p);
			p.Start();

			return p;
		}
		public static void Forget(this Task task) {
			async Task Fire(Task task) {
				try { await task; } 
				catch (Exception e) {
					Log.Error("Faulted Fire-And-Forget Task:", e);
				}
			}
			_ = Fire(task);
		}
		public static void Forget<T>(this Task<T> task) {
			async Task Fire(Task<T> task) {
				try { await task; } catch (Exception e) {
					Log.Error("Faulted Fire-And-Forget Task:", e);
					
				}
			}
			_ = Fire(task);
		}
	}

}
