using Ex;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

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
			waiter.Wait();

			Console.WriteLine("All tests completed, Finished testing.");
			Log.Stop();

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
			public Func<Random, char> nextChar = (rand) => (char)rand.Next(' ', '~');
			public Func<Random, string> nextLine = null;

			private string _fullhost;
			public string fullhost { get { return _fullhost ?? (_fullhost = $"{host}:{port}"); } }
			public FuzzData(string host, short port, string payload, int? seed = null) {
				this.host = host;
				this.port = port;
				this.payload = payload;
				this.seed = seed ?? unchecked (Math.Abs((int)DateTime.UtcNow.Ticks));
			}
			public FuzzData(FuzzData src) {
				name = src.name;
				maxLineLength = src.maxLineLength;
				host = src.host;
				port = src.port;
				payload = src.payload;
				seed = src.seed;
				nextChar = src.nextChar;
				nextLine = src.nextLine;
				_fullhost = src.fullhost;
			}
			public void BindSocket() {
				sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				sock.Connect(host, port);
			}
			public void UnbindSocket() {
				sock.Disconnect(false);
				sock.Dispose();
			}
		}

		private static List<ResultData> outputs = new List<ResultData>();

		public class ResultData {
			public FuzzData test { get; private set; }
			public long bytesSent { get; private set; }
			public DateTime start { get; private set; }
			public DateTime end { get; private set; }
			public TimeSpan timeSpan { get { return end-start; } }
			public ResultData(FuzzData test, long bytesSent, DateTime start, DateTime end) {
				this.test = test;
				this.bytesSent = bytesSent;
				this.start = start;
				this.end = end;
			}
		}

		private static readonly string[] BYTE_FIXES = new string[] {
			"B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"
		};
		public static string FormatBytes(long bytes) {
			double totalBytes = bytes;
			for (int i = 0; i < BYTE_FIXES.Length; i++) {
				if (totalBytes < 1024) {
					return $"{totalBytes:F2}{BYTE_FIXES[i]}";
				}
				totalBytes /= 1024.0;
			}
			totalBytes *= 1024; // Correction for last loop through
			return $"{totalBytes:F3}{BYTE_FIXES[BYTE_FIXES.Length-1]}";
		}

		public static string FormatTime(TimeSpan time) {
			return time.ToString("c");
		}
		public static string FormatTimeFrom(DateTime start) {
			return (DateTime.UtcNow-start).ToString("c");
		}

		private static readonly byte[] NEWL = new byte[] { (byte)'\n' };
		private static string FullHttp(FuzzData job) { return $@"POST /where HTTP/2
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
		}


		private static void FuzzBasic(FuzzData job) {
			string fullhost = job.fullhost;
			string payload = job.payload;
			Random rand = new Random(job.seed);
			string fullHttp = FullHttp(job);
			string partialHttp = fullHttp.Substring(0, rand.Next(2, fullHttp.Length - payload.Length));

			StringBuilder allSent = new StringBuilder(partialHttp);
			StringBuilder allRead = new StringBuilder();
			byte[] garbage = Encoding.UTF8.GetBytes(partialHttp);
			long bytesSent = 0;
			long byteDebounce = 1024*1024;
			DateTime start = DateTime.UtcNow;
			void send(byte[] data) {
				job.sock.Send(data);
				bytesSent += data.Length;
				if (bytesSent >= byteDebounce) {
					byteDebounce *= 2;
					Log.Debug($"Still sending. So far, sent {FormatBytes(bytesSent)} over {FormatTimeFrom(start)}.");
				}
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
					if (job.nextLine != null) {
						nextLine.Append(job.nextLine(rand));
					} else {
						for (int i = 0; i < len; i++) {
							nextLine.Append(job.nextChar(rand));
						}
					}
					byte[] line = Encoding.UTF8.GetBytes(nextLine.ToString());
					send(line);

				} catch (Exception e) {
					DateTime now = DateTime.UtcNow;
					TimeSpan elapsed = now - start;

					Log.Warning($"Exception during test after sending {FormatBytes(bytesSent)} over {FormatTime(elapsed)}\nHTTP Message received:\n----------\n{allRead.ToString()}\n---------\n", e);
					break;
				}
			}
			DateTime end = DateTime.UtcNow;
			ResultData result = new ResultData(job, bytesSent, start, end);
			outputs.Add(result);
			
		}

		private static string RandomHeader(Random rand) {
			StringBuilder str = new StringBuilder();

			int nameLength = rand.Next(4, 16);
			int valueLength = rand.Next(4, 32);
			void addNchars(int n) {
				for (int i = 0; i < n; i++) {
					char c = (char)rand.Next('a', 'z');
					if (rand.NextDouble() < .5) {
						c = (char)(c+0x20);
					}
					str.Append(c);
				}
			}

			addNchars(nameLength);
			str.Append(": ");
			addNchars(valueLength);

			return str.ToString();
		}

		public static readonly List<List<string>> mimeTypes = LoadMimeTypes();
		public static List<List<string>> LoadMimeTypes() {
			try {
				return Json.To<List<List<string>>>(File.ReadAllText("mimeTypes.json"));
			} catch (Exception e) {
				Log.Error("Failed to load mimeTypes", e);
				return new List<List<string>>();
			}
		}
		public class AcceptHeaderStuffer {
			int pos = 0;
			public string Next(Random rand) {
				if (pos >= mimeTypes.Count) { return "*/*"; }
				return mimeTypes[pos++][1];
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

			List<FuzzData> atks = new List<FuzzData>();
			FuzzData basis = new FuzzData(host, port, "{\"ayyy\":\"lmao\"}");
			atks.Add(new FuzzData(basis));
			atks.Add(new FuzzData(basis) { name = "RandomHeaders", nextLine = RandomHeader } );
			atks.Add(new FuzzData(basis) { name = "AcceptHeaderStuffing", nextLine = new AcceptHeaderStuffer().Next } );

			foreach (FuzzData atk in atks) {
				try {
					Log.Info($"Test [{atk.name}] starting");
					atk.BindSocket();
					callback(atk);
					atk.UnbindSocket();


					Log.Info($"Test [{atk.name}] finished");
				} catch (Exception e) {
					Log.Warning($"Unhandled Exception occurred during test {atk.name}:", e);
				}
				await Request.Post($"{harnessHost}/restart", "{}");
			}
			
			return 0;
		}


		private static void SetupLogger() {
			Log.ignorePath = UncleanSourceFileDirectory();
			Log.fromPath = "Fuzzer";
			Log.defaultTag = "Ex";
			//Log.includeCallerInfo = false;
			LogLevel target = LogLevel.Verbose;

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
