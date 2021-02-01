using Ex;
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Fuzzer {

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
			if (args.Length == 0) {
				Console.WriteLine("Please provide target endpoint or name of a 'spec' file to load.");
				return;
			}
			SetupRequest();
			SetupLogger();
			Task<int> waiter = AsyncMain(args[0]);

		}
		
		static async Task<int> AsyncMain(string arg) {
			short port = 0;
			string host = "";
			string[] splits = arg.Split(":");
			host = splits[0];
			if (splits.Length == 1 || !short.TryParse(splits[1], out port)) {
				Log.Warning("Couldn't parse port, or no port provided. Be advised, defaulting to port 3000.");
				port = 3000;
			}

			await Pwn(host, port, FuzzBasic);

			return 0;
		}

		public class FuzzData {
			public string name { get; set; } = "Default";
			public int maxLineLength { get; set; } = 24;
			public string host { get; private set; }
			public short port { get; private set; } = 3000;
			public Socket sock { get; private set; }
			public string payload { get; private set; } = "";
			public int seed;
			public Func<Random, char> nextChar = (rand) => (char) rand.Next(' ', '~');

			private string _fullhost;
			public string fullhost { get { return _fullhost ?? (_fullhost = $"{host}:{port}"); } }
			public FuzzData(string host, short port, Socket sock, string payload, int? seed = null) {
				this.host = host;
				this.port = port;
				this.sock = sock;
				this.payload = payload;
				this.seed = seed ?? unchecked (Math.Abs((int)DateTime.UtcNow.Ticks));
			}

		}

		private static readonly byte[] NEWL = new byte[] { (byte)'\n' };
		private static void FuzzBasic(FuzzData job) {
			string fullhost = job.fullhost;
			string payload = job.payload;
			Random rand = new Random(job.seed);

			string fullHttp = $@"POST /where HTTP/2
Host: {job.fullhost}
User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:84.0) Gecko/20100101 Firefox/84.0
Accept: */*
Accept-Language: en-US,en;q=0.5
Accept-Encoding: gzip, deflate, br
Referer: https://{job.fullhost}/wherever
Content-Length: {job.payload.Length}
content-type: text/plain
Origin: https://{job.fullhost}
Connection: keep-alive
Cookie: veryFreshIndeed
Sec-GPC: 1
TE: Trailers

{job.payload}";

			string partialHttp = fullHttp.Substring(0, rand.Next(2, fullHttp.Length - payload.Length));

			StringBuilder allSent = new StringBuilder(partialHttp);
			StringBuilder allRead = new StringBuilder();
			byte[] garbage = Encoding.UTF8.GetBytes(partialHttp);
			long bytesSent = 0;
			void send(byte[] data) {
				job.sock.Send(data);
				bytesSent += data.Length;
			}
			byte[] readBuff = new byte[4096];
			void read() {
				try {
					if (job.sock.Available > 0) {
						int bytesRead = job.sock.Receive(readBuff, 0, 4096, SocketFlags.None);
						string str = Encoding.UTF8.GetString(readBuff, 0, bytesRead);
						allRead.Append(str);
					}

				} catch (Exception e) { }

			}
			send(garbage);
			StringBuilder nextLine = new StringBuilder();

			while (true) {
				try {
					nextLine.Clear();
					allSent.Append("\n");
					send(NEWL);
					read();
					
					int len = rand.Next(2, job.maxLineLength);
					for (int i = 0; i < len; i++) {
						nextLine.Append(job.nextChar(rand));
					}
					byte[] line = Encoding.UTF8.GetBytes(nextLine.ToString());
					send(line);

				} catch (Exception e) {
					Log.Warning($"Exception during test after sending {bytesSent} bytes\nHTTP Message received:\n----------\n{allRead.ToString()}\n---------\n", e);
					break;
				}
			}
			
		}

		private static readonly string harnessHost = "http://localhost:31337";
		private static async Task<int> Pwn(string host, short port, Action<FuzzData> callback) {
			string name = callback.Method.Name;
			Log.Info($"Doing test [{name}] on {host}:{port}...");
			string result = await Request.Post($"{harnessHost}/test", $"{{\"name\":\"{name}\"}}");
			
			// Wait for a new instance of the test app to be up and running
			await Request.Post($"{harnessHost}/restart", "{}");
			// await Task.Delay(100);
			// Log.Info(result);

			Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			sock.Connect(host, port);
			FuzzData atk = new FuzzData(host, port, sock, "{\"ayyy\":\"lmao\"}");
			try {
				callback(atk);

				Log.Info($"Test [{name}] finished");
			} catch (Exception e) {
				Log.Warning($"Unhandled Exception occurred during callback {name}:", e);
				await Request.Post($"{harnessHost}/restart", "{}");
			}
			
			
			return 0;
		}


		private static void SetupLogger() {
			Log.ignorePath = UncleanSourceFileDirectory();
			Log.fromPath = "Fuzzer";
			Log.defaultTag = "Ex";
			//Log.includeCallerInfo = false;
			LogLevel target = LogLevel.Info;

			Log.logHandler += (info) => {
				// Console.WriteLine($"{info.tag}: {info.message}");
				if (info.level <= target) {
					//Console.WriteLine($"\n{info.tag}: {info.message}\n");
					Pretty.Print($"\n{info.tag}: {info.message}");
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

		private static void SetupRequest() {
			Request.onError = (str, err) => {
				Log.Error($"Request Error: {str}", err);
			};
		}
	}
}
