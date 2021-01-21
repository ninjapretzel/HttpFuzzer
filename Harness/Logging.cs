using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace Ex {

	/// <summary> Enum of log levels for filtering. </summary>
	public enum LogLevel {
		/// <summary> Disables all logging messages, except errors. </summary>
		Error = 0,
		/// <summary> Logs messages marked as warnings. </summary>
		Warning = 1,
		/// <summary> Log messages marked as normal information.  </summary>
		Info = 2,
		/// <summary> Logs messages marked as debug information. </summary>
		Debug = 3,
		/// <summary> Does not filter any logging messages. </summary>
		Verbose = 4,
	}


	public delegate void Logger(LogInfo info);

	/// <summary> Log message info passed on to <see cref="Logger"/> callbacks. </summary>
	public struct LogInfo {
		/// <summary> Severity of logging </summary>
		public LogLevel level { get; private set; }
		/// <summary> Log Message </summary>
		public string message { get; private set; }
		/// <summary> Log Tag </summary>
		public string tag { get; private set; }
		public LogInfo(LogLevel level, string message, string tag) {
			this.level = level;
			this.message = message;
			this.tag = tag;
		}
	}

	/// <summary> Class handling statically accessible logging </summary>
	public static class Log {

		public static readonly string[] LEVEL_CODES = { "\\r", "\\y", "\\w", "\\h", "\\d" };
		public static string defaultTag = "Baka";

		public static string ColorCode(LogLevel level) { return (LEVEL_CODES[(int)level]); }
		/// <summary> Path to use to filter file paths </summary>
		public static string ignorePath = null;
		/// <summary> Path to insert infront of filtered paths </summary>
		public static string fromPath = null;

		/// <summary> True to insert backslash color codes. </summary>
		public static bool colorCodes = true;

		/// <summary> Log handler to use to print logs </summary>
		public static Logger logHandler;
		/// <summary> Queue of unhandled <see cref="LogInfo"/>s </summary>
		public static readonly ConcurrentQueue<LogInfo> logs = new ConcurrentQueue<LogInfo>();

		/// <summary> If logging is currently running </summary>
		private static bool go = false;
		/// <summary> Thread handling logging  </summary>
		private static Thread logThread = InitializeLoggingThread();
		/// <summary> Initializes thread that handles logging </summary>
		private static Thread InitializeLoggingThread() {
			go = true;
			Thread t = new Thread(() => {
				LogInfo info;
				while (go) {
					while (logs.TryDequeue(out info)) {
						try {
							logHandler.Invoke(info);
						} catch (Exception) { /* Can't really log when there's an exception */ }
					}
					Thread.Sleep(1);
				}
			});
			t.Start();
			return t;
		}
		/// <summary> Stops the logging thread (after a delay) </summary>
		public static void Stop() {
			go = false;
		}
		/// <summary> Restarts the logging thread </summary>
		public static void Restart() {
			go = false;
			logThread.Join();
			logThread = InitializeLoggingThread();
		}

		/// <summary> Logs a message using the Verbose LogLevel. </summary>
		/// <param name="obj"> Message to log. </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Verbose(object obj, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			log(null, obj, LogLevel.Verbose, tag, callerName, callerPath, callerLine);
		}
		/// <summary> Logs an excpetion and message using the Verbose LogLevel. </summary>
		/// <param name="ex"> Exception to log. </param>
		/// <param name="obj"> Message to log. </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Verbose(object obj, Exception ex, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			log(ex, obj, LogLevel.Verbose, tag, callerName, callerPath, callerLine);
		}

		/// <summary> Logs a message using the Debug LogLevel. </summary>
		/// <param name="obj"> Message to log. </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Debug(object obj, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			log(null, obj, LogLevel.Debug, tag, callerName, callerPath, callerLine);
		}
		/// <summary> Logs an excpetion and message using the Debug LogLevel. </summary>
		/// <param name="ex"> Exception to log. </param>
		/// <param name="obj"> Message to log. </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Debug(object obj, Exception ex, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			log(ex, obj, LogLevel.Debug, tag, callerName, callerPath, callerLine);
		}

		/// <summary> Logs a message using the Info LogLevel. </summary>
		/// <param name="obj"> Message to log. </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Info(object obj, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			log(null, obj, LogLevel.Info, tag, callerName, callerPath, callerLine);
		}
		/// <summary> Logs an excpetion and message using the Info LogLevel. </summary>
		/// <param name="ex"> Exception to log. </param>
		/// <param name="obj"> Message to log. </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Info(object obj, Exception ex, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			log(ex, obj, LogLevel.Info, tag, callerName, callerPath, callerLine);
		}

		/// <summary> Logs a message using the Warning LogLevel. </summary>
		/// <param name="obj"> Message to log. </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Warning(object obj, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			log(null, obj, LogLevel.Warning, tag, callerName, callerPath, callerLine);
		}
		/// <summary> Logs an excpetion and message using the Warning LogLevel. </summary>
		/// <param name="ex"> Exception to log. </param>
		/// <param name="obj"> Message to log. </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Warning(object obj, Exception ex, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			log(ex, obj, LogLevel.Warning, tag, callerName, callerPath, callerLine);
		}

		/// <summary> Logs a message using the Error LogLevel. </summary>
		/// <param name="obj"> Message to log. </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Error(object obj, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			log(null, obj, LogLevel.Error, tag, callerName, callerPath, callerLine);
		}
		/// <summary> Logs an excpetion and message using the Error LogLevel. </summary>
		/// <param name="ex"> Exception to log. </param>
		/// <param name="obj"> Message to log. </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void Error(object obj, Exception ex, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			log(ex, obj, LogLevel.Error, tag, callerName, callerPath, callerLine);
		}

		/// <summary> Primary workhorse logging method with all options. </summary>
		/// <param name="ex"> Exception to log </param>
		/// <param name="obj"> Message to log </param>
		/// <param name="level"> Minimum log level to use </param>
		/// <param name="tag"> Tag to log with </param>
		/// <param name="callerName">Name of calling method </param>
		/// <param name="callerPath">File of calling method </param>
		/// <param name="callerLine">Line number of calling method </param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void log(Exception ex, object obj, LogLevel level = LogLevel.Info, string tag = null,
				[CallerMemberName] string callerName = "[NO METHOD]",
				[CallerFilePath] string callerPath = "[NO PATH]",
				[CallerLineNumber] int callerLine = -1) {
			if (obj == null) { obj = "[null]"; }
			if (tag == null) { tag = defaultTag; }
			string callerInfo = CallerInfo(callerName, callerPath, callerLine, level);
			string message = (colorCodes ? ColorCode(level) : "") + obj.ToString()
				+ (ex != null ? $"\n{ex.InfoString()}" : "")
				+ callerInfo;

			logs.Enqueue(new LogInfo(level, message, tag));

		}


		/// <summary> Little helper method to consistantly format caller information </summary>
		/// <param name="callerName"> Name of method </param>
		/// <param name="callerPath"> Path of file method is contained in </param>
		/// <param name="callerLine"> Line in file where log is called. </param>
		/// <returns>Formatted caller info</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)] // Gotta go fast. 
		public static string CallerInfo(string callerName, string callerPath, int callerLine, LogLevel level) {
			string path = (fromPath != null ? fromPath : "")
				+ (ignorePath != null && callerPath.Contains(ignorePath)
					? callerPath.Substring(callerPath.IndexOf(ignorePath) + ignorePath.Length)
					: callerPath);
			return (colorCodes ? "\\d" : "")
				+ $"\n{level.ToString()[0]}: [{DateTime.UtcNow.UnixTimestamp()}] by "
				+ ForwardSlashPath(path)
				+ $" at {callerLine} in {callerName}()";
		}
		private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long UnixTimestamp(this DateTime date) {
			TimeSpan diff = date.ToUniversalTime().Subtract(epoch);
			return (long)diff.TotalMilliseconds;
		}
		/// <summary> Convert a file or folder path to only contain forward slashes '/' instead of backslashes '\'. </summary>
		/// <param name="path"> Path to convert </param>
		/// <returns> <paramref name="path"/> with all '\' characters replaced with '/' </returns>
		private static string ForwardSlashPath(string path) {
			string s = path.Replace('\\', '/');
			return s;
		}

		/// <summary> Constructs a string with information about an exception, and all of its inner exceptions. </summary>
		/// <param name="e"> Exception to print. </param>
		/// <returns> String containing info about an exception, and all of its inner exceptions. </returns>
		private static string InfoString(this Exception e) {
			StringBuilder str = new StringBuilder("\nException Info: " + e.MiniInfoString());
			str.Append("\n\tMessage: " + ForwardSlashPath(e.Message));
			Exception ex = e.InnerException;

			while (ex != null) {
				str.Append("\n\tInner Exception: " + ex.MiniInfoString());
				ex = ex.InnerException;
			}


			return ForwardSlashPath(str.ToString());
		}

		/// <summary> Constructs a string with information about an exception. </summary>
		/// <param name="e"> Exception to print </param>
		/// <returns> String containing exception type, message, and stack trace. </returns>
		private static string MiniInfoString(this Exception e) {
			StringBuilder str = new StringBuilder(e.GetType().ToString());
			str.Append("\n\tMessage: " + ForwardSlashPath(e.Message));
			str.Append("\nStack Trace: " + e.StackTrace);
			return str.ToString();
		}

	}


	public static class Pretty {
		/// <summary> Dictionary of hex codes mapped to their console colors </summary>
		private static Dictionary<char, ConsoleColor> colors = new Dictionary<char, ConsoleColor>() {
			{ '0', ConsoleColor.Black },
			{ '1', ConsoleColor.Red },
			{ '2', ConsoleColor.Green },
			{ '3', ConsoleColor.Yellow },
			{ '4', ConsoleColor.Blue },
			{ '5', ConsoleColor.Cyan },
			{ '6', ConsoleColor.Magenta },
			{ '7', ConsoleColor.White },

			{ '8', ConsoleColor.DarkGray },
			{ '9', ConsoleColor.DarkRed },
			{ 'A', ConsoleColor.DarkGreen },
			{ 'B', ConsoleColor.DarkYellow },
			{ 'C', ConsoleColor.DarkBlue },
			{ 'D', ConsoleColor.DarkCyan },
			{ 'E', ConsoleColor.DarkMagenta },
			{ 'F', ConsoleColor.Gray },
		};

		private static Dictionary<string, string> markdownConversion = new Dictionary<string, string>() {
			{ "\\r", $"{INVIS_FG}1"},
			{ "\\o", $"{INVIS_FG}9"},
			{ "\\y", $"{INVIS_FG}3"},
			{ "\\g", $"{INVIS_FG}2"},
			{ "\\b", $"{INVIS_FG}4"},
			{ "\\i", $"{INVIS_FG}D"},
			{ "\\v", $"{INVIS_FG}6"},

			{ "\\c", $"{INVIS_FG}5"},

			{ "\\w", $"{INVIS_FG}7"},
			{ "\\k", $"{INVIS_FG}0"},
			{ "\\u", $"{INVIS_FG}A"},
			{ "\\h", $"{INVIS_FG}F"},
			{ "\\d", $"{INVIS_FG}8"},

			{ "\\1", $"{INVIS_FG}F"},
			{ "\\2", $"{INVIS_FG}9"},
			{ "\\3", $"{INVIS_FG}2"},
			{ "\\4", $"{INVIS_FG}4"},
			{ "\\e", $"{INVIS_FG}B"},
			{ "\\t", $"{INVIS_FG}2"},
			{ "\\p", $"{INVIS_FG}A"},
			{ "\\j", $"{INVIS_FG}A"},
		};



		/// <summary> Hex codes packed in an array </summary>
		public static readonly char[] hex = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

		public const char INVIS_BG = (char)0x0E;
		public const char INVIS_FG = (char)0x0F;
		/// <summary> Get a %#^# code for the given bg/fg colors. </summary>
		public static string Code(int bg, int fg) {
			return "" + INVIS_BG + hex[bg % 16] + INVIS_FG + hex[fg % 16];
		}

		public static string ConvertMD(string src) {
			foreach (var pair in markdownConversion) {
				src = src.Replace(pair.Key, pair.Value);
			}
			return src;
		}

		public static void Print(string str) {
			PrintDirect(ConvertMD(str));
		}
		public static void PrintDirect(string str) {
			StringBuilder buffer = new StringBuilder();

			for (int i = 0; i < str.Length; i++) {
				// Special character for changing foreground colors
				if (str[i] == INVIS_FG && i + 1 < str.Length) {
					char next = str[i + 1];
					if (colors.ContainsKey(next)) {
						// Write text in previous color 
						Console.Write(buffer.ToString());
						// Clear buffer for text in next color
						buffer.Clear();
						// Set next color
						Console.ForegroundColor = colors[next];
						i++; // consume an extra character
						continue; // Restart loop
					}
				}
				// Special character for changing background colors
				if (str[i] == INVIS_BG && i + 1 < str.Length) {
					char next = str[i + 1];
					if (colors.ContainsKey(next)) {
						// Write text in previous color
						Console.Write(buffer.ToString());
						// Clear buffer for text in next color
						buffer.Clear();
						// Set next color 
						Console.BackgroundColor = colors[next];
						i++; // consume an extra character
						continue; // Restart loop
					}
				}
				buffer.Append(str[i]);
			}

			Console.Write(buffer);

			Console.ResetColor();


		}


	}

}
