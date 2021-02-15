using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace CSharp {
	class Program {
		static void Main(string[] args) {
			using (HttpListener listener = new HttpListener()) {
				listener.Prefixes.Add("http://localhost:3000/");

				try {
					listener.Start();
					while (true) {
						var ctx = listener.GetContext();

						string msg = "{\"success\":1}";
						byte[] data = Encoding.UTF8.GetBytes(msg);
						ctx.Response.StatusCode = 200;
						ctx.Response.StatusDescription = "Ok";
						ctx.Response.ContentType = "application/json;charset=utf-8";
						ctx.Response.ContentEncoding = Encoding.UTF8;
						ctx.Response.OutputStream.Write(data, 0, data.Length);

						if (!ctx.Request.KeepAlive) {
							ctx.Response.OutputStream.Close();
						}
					}
				} catch (Exception e) {
					if (e is HttpListenerException && (e as HttpListenerException).ErrorCode == 995) { return; }
					Console.WriteLine("Listen: Internal Error - " + e.ToString() + "\n" + e.StackTrace);
					return;
				}
			}
		}

	}
}
