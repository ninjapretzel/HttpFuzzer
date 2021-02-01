using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Specialized;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Net.WebSockets;
using System.Collections.Generic;
using static MiniHttp.HttpServerHelpers;
using static MiniHttp.ProvidedMiddleware;
using Ex;
using System.Threading;

/////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////
// Basic almost-single-file Http-Server library that I can drop into projects for easy use
// Somewhat based on NodeJS's Koa/DenoJS's Oak. 
// https://koajs.com/
// Simple control with a stack-like middleware-oriented arrangement.
// Pass `MiddleWare` functions into `HttpServer.Watch` or `HttpServer.Serve`
// Also note the bit on the koajs `ctx.res` object:
//```
//	Bypassing Koa's response handling is not supported. Avoid using the following node properties:
//		res.statusCode
//		res.writeHead()
//		res.write()
//		res.end()
//```
// By consequence of how middleware works, the same applies to this framework.
//
// Note: There is one dependency, which is not noted in the above usings: My 'XtoJSON' library.
// Simply include the following file in parallel with this library: 
// https://github.com/ninjapretzel/XtoJSON/blob/master/XtoJSON.cs
/////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////
///////////////////////////////////////////////////////////////////////////////////////
namespace MiniHttp {

	/// <summary> Primary workhorse of this framework </summary>
	/// <param name="context"> <see cref="Ctx"/> object representing the request/response and everything else </param>
	/// <param name="next"> <see cref="NextFn"/> which calls the next middleware in the stack. </param>
	/// <returns> Async <see cref="Task"/> object </returns>
	public delegate Task Middleware(Ctx context, NextFn next);
	/// <summary> Sync version of middleware, converted to async with optimal precompleted tasks. </summary>
	/// <param name="context"> <see cref="Ctx"/> object representing the request/response and everything else </param>
	/// <param name="next"> <see cref="NextFn"/> which calls the next middleware in the stack. </param>
	public delegate void SyncMiddleware(Ctx context, NextFn next);

	/// <summary> Delegate of 'next' for clarity and convinience. Call one of these to call the next middleware in the stack. </summary>
	/// <returns> Async <see cref="Task"/> object </returns>
	public delegate Task NextFn();
	/// <summary> Primary context object, mostly wraps <see cref="HttpListenerContext"/>, and adds some things for convinience </summary>
	public class Ctx {
		/// <summary> Embedded <see cref="Req"/> object </summary>
		public Req req { get; private set; }
		/// <summary> Embedded <see cref="Res"/> object </summary>
		public Res res { get; private set; }
		/// <summary> Raw, wrapped <see cref="HttpListenerContext"/> object </summary>
		public HttpListenerContext raw { get; private set; }

		/// <summary> JsonObject holding query parameters </summary>
		public JsonObject query { get; private set; }
		/// <summary> JsonObject holding URL derived parameters </summary>
		public JsonObject param { get; private set; }
		/// <summary> JsonObject for custom middleware to use. </summary>
		public JsonObject midData { get; private set; }

		/// <summary> Requested path split on '/' chars </summary>
		public string[] pathSplit { get { return req.pathSplit; } }

		/// <summary> Default constructor, used when beginning handling of a request. </summary>
		/// <param name="context"> Raw <see cref="HttpListenerContext"/> to wrap </param>
		public Ctx(HttpListenerContext context) {
			raw = context;
			req = new Req(this, context.Request);
			res = new Res(this, context.Response);
			KeepAlive = true;

			query = req.QueryString.ToJsonObject();
			param = new JsonObject();
			midData = new JsonObject();

		}
		#region HttpListenerContext wrappers
		/// <summary> Gets the <see cref="HttpListenerContext.User"/>. </summary>
		public IPrincipal User { get { return raw.User; } }
		/// <summary> Wraps <see cref="HttpListenerContext.AcceptWebSocketAsync(string)"/></summary>
		public Task<HttpListenerWebSocketContext> AcceptWegbSocketAsync(string subProtocol) {
			return raw.AcceptWebSocketAsync(subProtocol);
		}
		/// <summary> Wraps <see cref="HttpListenerContext.AcceptWebSocketAsync(string, int, TimeSpan)"/></summary>
		public Task<HttpListenerWebSocketContext> AcceptWegbSocketAsync(string subProtocol, int receiveBufferSize, TimeSpan keepAliveInterval) {
			return raw.AcceptWebSocketAsync(subProtocol, receiveBufferSize, keepAliveInterval);
		}
		/// <summary> Wraps <see cref="HttpListenerContext.AcceptWebSocketAsync(string, int, TimeSpan, ArraySegment{byte})"/></summary>
		public Task<HttpListenerWebSocketContext> AcceptWebSocketAsync(string subProtocol, int receiveBufferSize, TimeSpan keepAliveInterval, ArraySegment<byte> internalBuffer) {
			return raw.AcceptWebSocketAsync(subProtocol, receiveBufferSize, keepAliveInterval, internalBuffer);
		}
		/// <summary> Wraps <see cref="HttpListenerContext.AcceptWebSocketAsync(string,  TimeSpan)"/></summary>
		public Task<HttpListenerWebSocketContext> AcceptWegbSocketAsync(string subProtocol, TimeSpan keepAliveInterval) {
			return raw.AcceptWebSocketAsync(subProtocol, keepAliveInterval);
		}
		#endregion
		#region HttpListenerRequest wrappers
		/// <summary> Was this request originally HTTPS? (SSL) Wraps into <see cref="HttpListenerRequest.IsSecureConnection"/> </summary>
		public bool IsSecureConnection { get { return raw.Request.IsSecureConnection; } }
		/// <summary> Was this a Websocket request? Wraps into <see cref="HttpListenerRequest.IsWebSocketRequest"/> </summary>
		public bool IsWebSocketRequest { get { return raw.Request.IsWebSocketRequest; } }
		/// <summary> Is the client authenticated? Wraps into <see cref="HttpListenerRequest.IsAuthenticated"/> </summary>
		public bool IsAuthenticated { get { return raw.Request.IsAuthenticated; } }
		/// <summary> Does this request contain data? Wraps into <see cref="HttpListenerRequest.HasEntityBody"/> </summary>
		public bool HasEntityBody { get { return raw.Request.HasEntityBody; } }
		/// <summary> Was this request sent by the same computer? (loopback) Wraps into <see cref="HttpListenerRequest.IsLocal"/> </summary>
		public bool IsLocal { get { return raw.Request.IsLocal; } }
		/// <summary> Gets the error code, if any, for problems with the client's certificate. Wraps into <see cref="HttpListenerRequest.ClientCertificateError"/> </summary>
		public int ClientCertificateError { get { return raw.Request.ClientCertificateError; } }
		/// <summary> Gets the string representation of the client's request Wraps into <see cref="HttpListenerRequest.UserHostAddress"/> </summary>
		public string UserHostAddress { get { return raw.Request.UserHostAddress; } }
		/// <summary> Gets the `User-Agent` header, if provided. Wraps into <see cref="HttpListenerRequest.UserAgent"/> </summary>
		public string UserAgent { get { return raw.Request.UserAgent; } }
		/// <summary> Gets the service provider name. Wraps into <see cref="HttpListenerRequest.ServiceName"/> </summary>
		public string ServiceName { get { return raw.Request.ServiceName; } }
		/// <summary> Gets the URL information (Without host and port). Wraps into <see cref="HttpListenerRequest.RawUrl"/> </summary>
		public string RawUrl { get { return raw.Request.RawUrl; } }
		/// <summary> Gets the host and port. Wraps into <see cref="HttpListenerRequest.UserHostName"/> </summary>
		public string UserHostName { get { return raw.Request.UserHostName; } }
		/// <summary> Gets the requested HTTP method. Wraps into <see cref="HttpListenerRequest.HttpMethod"/> </summary>
		public string HttpMethod { get { return raw.Request.HttpMethod; } }
		/// <summary> Gets the requested languages. Wraps into <see cref="HttpListenerRequest.UserLanguages"/> </summary>
		public string[] UserLanguages { get { return raw.Request.UserLanguages; } }
		/// <summary> Gets the acceptable response types. Wraps into <see cref="HttpListenerRequest.AcceptTypes"/> </summary>
		public string[] AcceptTypes { get { return raw.Request.AcceptTypes; } }
		/// <summary> Gets the `Referrer` header. Wraps into <see cref="HttpListenerRequest.UrlReferrer"/> </summary>
		public Uri UrlReferrer { get { return raw.Request.UrlReferrer; } }
		/// <summary> Gets the URI object requested by the client. Wraps into <see cref="HttpListenerRequest.Url"/> </summary>
		public Uri Url { get { return raw.Request.Url; } }
		/// <summary> Gets a stream containing the body data . Wraps into <see cref="HttpListenerRequest.InputStream"/> </summary>
		public Stream InputStream { get { return raw.Request.InputStream; } }
		/// <summary> Gets the TransportContext of the request. Wraps into <see cref="HttpListenerRequest.TransportContext"/> </summary>
		public TransportContext TransportContext { get { return raw.Request.TransportContext; } }
		/// <summary> Gets the Cookies in the request. Wraps into <see cref="HttpListenerRequest.Cookies"/> </summary>
		public CookieCollection Cookies { get { return raw.Request.Cookies; } }
		/// <summary> Gets the remote end point. Wraps into <see cref="HttpListenerRequest.RemoteEndPoint"/> </summary>
		public IPEndPoint RemoteEndPoint { get { return raw.Request.RemoteEndPoint; } }
		/// <summary> Gets the local end point. Wraps into <see cref="HttpListenerRequest.LocalEndPoint"/> </summary>
		public IPEndPoint LocalEndPoint { get { return raw.Request.LocalEndPoint; } }
		/// <summary> Gets the Request trace ID. Wraps <see cref="HttpListenerRequest.RequestTraceIdentifier"/> </summary>
		public Guid RequestTraceIdentifier { get { return raw.Request.RequestTraceIdentifier; } }
		#endregion
		#region HttpListenerResponse wrappers
		/// <summary> Gets/sets the HTTP status code. Wraps into <see cref="HttpListenerResponse.StatusCode"/>. </summary>
		public int StatusCode { get { return raw.Response.StatusCode; } set { raw.Response.StatusCode = value; } }
		/// <summary> Gets/sets the HTTP status description. Wraps into <see cref="HttpListenerResponse.StatusDescription"/>. </summary>
		public string StatusDescription { get { return raw.Response.StatusDescription; } set { raw.Response.StatusDescription = value; } }
		/// <summary> Gets/sets if chunked transmission should be used. Wraps into <see cref="HttpListenerResponse.SendChunked"/>. </summary>
		public bool SendChunked { get { return raw.Response.SendChunked; } set { raw.Response.SendChunked = value; } }
		/// <summary> Gets/sets if the connection should be kept open. Wraps into <see cref="HttpListenerResponse.KeepAlive"/>. </summary>
		public bool KeepAlive { get { return raw.Response.KeepAlive; } set { raw.Response.KeepAlive = value; } }
		/// <summary> Gets/sets the redirect location. Wraps into <see cref="HttpListenerResponse.RedirectLocation"/>. </summary>
		public string RedirectLocation { get { return raw.Response.RedirectLocation; } set { raw.Response.RedirectLocation = value; } }
		/// <summary> Gets/sets the content type. Wraps into <see cref="HttpListenerResponse.ContentType"/>. </summary>
		public string ContentType { get { return raw.Response.ContentType; } set { raw.Response.ContentType = value; } }
		/// <summary> Gets/sets the protocol version. Wraps into <see cref="HttpListenerResponse.ProtocolVersion"/>. </summary>
		public Version ProtocolVersion { get { return raw.Response.ProtocolVersion; } set { raw.Response.ProtocolVersion = value; } }
		/// <summary> Gets/sets the encoding to use. Wraps into <see cref="HttpListenerResponse.ContentEncoding"/>. </summary>
		public Encoding ContentEncoding { get { return raw.Response.ContentEncoding; } set { raw.Response.ContentEncoding = value; } }
		/// <summary> Gets the output stream. Wraps into <see cref="HttpListenerResponse.OutputStream"/>. </summary>
		public Stream OutputStream { get { return raw.Response.OutputStream; } }
		#endregion


		public override string ToString() {
			return $"Ctx: {RemoteEndPoint} => {LocalEndPoint} | {HttpMethod} @ {UserHostName}{RawUrl} -> {StatusCode} ";
		}

		/// <summary> Convinient accessor for <see cref="Res.body"/>. </summary>
		public object body { get { return res.body; } set { res.body = value; } }

	}

	/// <summary> Request object, mostly wraps <see cref="HttpListenerRequest"/>, and adds some things for convinience </summary>
	public class Req {
		/// <summary> Parent <see cref="Ctx"/> object. </summary>
		public Ctx ctx { get; private set; }
		/// <summary> Raw, wrapped <see cref="HttpListenerRequest"/> object </summary>
		public HttpListenerRequest raw { get; private set; }
		/// <summary> Constructor, used by <see cref="Ctx.Ctx(HttpListenerContext)"/></summary>
		/// <param name="owner"> Owner <see cref="Ctx"/> object </param>
		/// <param name="context"> Raw <see cref="HttpListenerRequest"/> to wrap </param>
		public Req(Ctx owner, HttpListenerRequest request) {
			ctx = owner;
			raw = request;

			pathSplit = UpToFirst(request.RawUrl, "?").Split("/").Where(it => it != "").ToArray();

		}
		#region HttpListenerRequest wrappers
		/// <summary> Was this request originally HTTPS? (SSL) Wraps into <see cref="HttpListenerRequest.IsSecureConnection"/> </summary>
		public bool IsSecureConnection { get { return raw.IsSecureConnection; } }
		/// <summary> Should this connection be kept alive? Wraps into <see cref="HttpListenerRequest.KeepAlive"/> </summary>
		public bool KeepAlive { get { return raw.KeepAlive; } }
		/// <summary> Was this a Websocket request? Wraps into <see cref="HttpListenerRequest.IsWebSocketRequest"/> </summary>
		public bool IsWebSocketRequest { get { return raw.IsWebSocketRequest; } }
		/// <summary> Is the client authenticated? Wraps into <see cref="HttpListenerRequest.IsAuthenticated"/> </summary>
		public bool IsAuthenticated { get { return raw.IsAuthenticated; } }
		/// <summary> Does this request contain data? Wraps into <see cref="HttpListenerRequest.HasEntityBody"/> </summary>
		public bool HasEntityBody { get { return raw.HasEntityBody; } }
		/// <summary> Was this request sent by the same computer? (loopback) Wraps into <see cref="HttpListenerRequest.IsLocal"/> </summary>
		public bool IsLocal { get { return raw.IsLocal; } }
		/// <summary> Gets the error code, if any, for problems with the client's certificate. Wraps into <see cref="HttpListenerRequest.ClientCertificateError"/> </summary>
		public int ClientCertificateError { get { return raw.ClientCertificateError; } }
		/// <summary> Get the length of data included Wraps into <see cref="HttpListenerRequest.ContentLength64"/> </summary>
		public long ContentLength64 { get { return raw.ContentLength64; } }
		/// <summary> Gets the string representation of the client's request Wraps into <see cref="HttpListenerRequest.UserHostAddress"/> </summary>
		public string UserHostAddress { get { return raw.UserHostAddress; } }
		/// <summary> Gets the `User-Agent` header, if provided. Wraps into <see cref="HttpListenerRequest.UserAgent"/> </summary>
		public string UserAgent { get { return raw.UserAgent; } }
		/// <summary> Gets the service provider name. Wraps into <see cref="HttpListenerRequest.ServiceName"/> </summary>
		public string ServiceName { get { return raw.ServiceName; } }
		/// <summary> Gets the URL information (Without host and port). Wraps into <see cref="HttpListenerRequest.RawUrl"/> </summary>
		public string RawUrl { get { return raw.RawUrl; } }
		/// <summary> Gets the host and port. Wraps into <see cref="HttpListenerRequest.UserHostName"/> </summary>
		public string UserHostName { get { return raw.UserHostName; } }
		/// <summary> Gets the requested HTTP method. Wraps into <see cref="HttpListenerRequest.HttpMethod"/> </summary>
		public string HttpMethod { get { return raw.HttpMethod; } }
		/// <summary> Gets the request's MIME type. Wraps into <see cref="HttpListenerRequest.ContentType"/> </summary>
		public string ContentType { get { return raw.ContentType; } }
		/// <summary> Gets the requested languages. Wraps into <see cref="HttpListenerRequest.UserLanguages"/> </summary>
		public string[] UserLanguages { get { return raw.UserLanguages; } }
		/// <summary> Gets the acceptable response types. Wraps into <see cref="HttpListenerRequest.AcceptTypes"/> </summary>
		public string[] AcceptTypes { get { return raw.AcceptTypes; } }
		/// <summary> Gets the `Referrer` header. Wraps into <see cref="HttpListenerRequest.UrlReferrer"/> </summary>
		public Uri UrlReferrer { get { return raw.UrlReferrer; } }
		/// <summary> Gets the URI object requested by the client. Wraps into <see cref="HttpListenerRequest.Url"/> </summary>
		public Uri Url { get { return raw.Url; } }
		/// <summary> Gets the HTTP version of the request Wraps into <see cref="HttpListenerRequest.ProtocolVersion"/> </summary>
		public Version ProtocolVersion { get { return raw.ProtocolVersion; } }
		/// <summary> Gets a stream containing the body data . Wraps into <see cref="HttpListenerRequest.InputStream"/> </summary>
		public Stream InputStream { get { return raw.InputStream; } }
		/// <summary> Gets the Encoding object of the request, if available. Wraps into <see cref="HttpListenerRequest.ContentEncoding"/> </summary>
		public Encoding ContentEncoding { get { return raw.ContentEncoding; } }
		/// <summary> Gets the TransportContext of the request. Wraps into <see cref="HttpListenerRequest.TransportContext"/> </summary>
		public TransportContext TransportContext { get { return raw.TransportContext; } }
		/// <summary> Gets the headers of the request. Wraps into <see cref="HttpListenerRequest.Headers"/> </summary>
		public NameValueCollection Headers { get { return raw.Headers; } }
		/// <summary> Gets the QueryString of the request. Wraps into <see cref="HttpListenerRequest.QueryString"/> </summary>
		public NameValueCollection QueryString { get { return raw.QueryString; } }
		/// <summary> Gets the Cookies in the request. Wraps into <see cref="HttpListenerRequest.Cookies"/> </summary>
		public CookieCollection Cookies { get { return raw.Cookies; } }
		/// <summary> Gets the remote end point. Wraps into <see cref="HttpListenerRequest.RemoteEndPoint"/> </summary>
		public IPEndPoint RemoteEndPoint { get { return raw.RemoteEndPoint; } }
		/// <summary> Gets the local end point. Wraps into <see cref="HttpListenerRequest.LocalEndPoint"/> </summary>
		public IPEndPoint LocalEndPoint { get { return raw.LocalEndPoint; } }
		/// <summary> Gets the Request trace ID. Wraps <see cref="HttpListenerRequest.RequestTraceIdentifier"/> </summary>
		public Guid RequestTraceIdentifier { get { return raw.RequestTraceIdentifier; } }

		/// <summary> Wraps <see cref="HttpListenerRequest.BeginGetClientCertificate(AsyncCallback, object)"/> </summary>
		public IAsyncResult BeginGetClientCertificate(AsyncCallback requestCallback, object state) { return raw.BeginGetClientCertificate(requestCallback, state); }
		/// <summary> Wraps <see cref="HttpListenerRequest.EndGetClientCertificate(IAsyncResult)"/> </summary>
		public X509Certificate2 EndGetClientCertificate(IAsyncResult asyncResult) { return raw.EndGetClientCertificate(asyncResult); }
		/// <summary> Wraps <see cref="HttpListenerRequest.GetClientCertificate"/> </summary>
		public X509Certificate2 GetClientCertificate() { return raw.GetClientCertificate(); }
		/// <summary> Wraps <see cref="HttpListenerRequest.GetClientCertificateAsync"/> </summary>
		public Task<X509Certificate2> GetClientCertificateAsync() { return raw.GetClientCertificateAsync(); }
		#endregion
		/// <summary> Accesses the entire request body as a <see cref="byte[]"/>. Populated by the <see cref="BodyParser"/> </summary>
		public byte[] data { get; set; }
		/// <summary> Available to hold the request's string version, if present. Populated by the <see cref="BodyParser"/> </summary>
		public string body { get; set; }
		/// <summary> <see cref="JsonObject"/> parsed from body, if successful. Populated by the <see cref="BodyParser"/> </summary>
		public JsonObject bodyObj { get; set; }
		/// <summary> <see cref="JsonArray"/> parsed from body, if successful. Populated by the <see cref="BodyParser"/> </summary>
		public JsonArray bodyArr { get; set; }

		/// <summary> Requested path split on '/' chars </summary>
		public string[] pathSplit { get; private set; }
	}
	/// <summary> Response object, mostly wraps <see cref="HttpListenerResponse"/>, and adds some things for convinience </summary>
	public class Res {
		private static readonly Version VERSION = new Version("1.1");
		/// <summary> Parent <see cref="Ctx"/> object. </summary>
		public Ctx ctx { get; private set; }
		/// <summary> Raw, wrapped <see cref="HttpListenerResponse"/> object </summary>
		public HttpListenerResponse raw { get; private set; }
		/// <summary> Constructor, used by <see cref="Ctx.Ctx(HttpListenerContext)"/></summary>
		/// <param name="owner"> Owner <see cref="Ctx"/> object </param>
		/// <param name="context"> Raw <see cref="HttpListenerResponse"/> to wrap </param>
		public Res(Ctx owner, HttpListenerResponse response) {
			ctx = owner;
			raw = response;
			ContentEncoding ??= Encoding.UTF8;
			StatusCode = 200;
			StatusDescription = "ok";
			ProtocolVersion = VERSION;
			SendChunked = false;

			body = null;
		}
		#region HttpListenerResponse wrappers
		/// <summary> Gets/sets the Content-Length header. Wraps into <see cref="HttpListenerResponse.ContentLength64"/>. </summary>
		public long ContentLength64 { get { return raw.ContentLength64; } set { raw.ContentLength64 = value; } }
		/// <summary> Gets/sets the HTTP status code. Wraps into <see cref="HttpListenerResponse.StatusCode"/>. </summary>
		public int StatusCode { get { return raw.StatusCode; } set { raw.StatusCode = value; } }
		/// <summary> Gets/sets the HTTP status description. Wraps into <see cref="HttpListenerResponse.StatusDescription"/>. </summary>
		public string StatusDescription { get { return raw.StatusDescription; } set { raw.StatusDescription = value; } }
		/// <summary> Gets/sets if chunked transmission should be used. Wraps into <see cref="HttpListenerResponse.SendChunked"/>. </summary>
		public bool SendChunked { get { return raw.SendChunked; } set { raw.SendChunked = value; } }
		/// <summary> Gets/sets if the connection should be kept open. Wraps into <see cref="HttpListenerResponse.KeepAlive"/>. </summary>
		public bool KeepAlive { get { return raw.KeepAlive; } set { raw.KeepAlive = value; } }
		/// <summary> Gets/sets the content-type. Wraps into <see cref="HttpListenerResponse.ContentType"/>. </summary>
		public string ContentType { get { return raw.ContentType; } set { raw.ContentType = value; } }
		/// <summary> Gets/sets the redirect location. Wraps into <see cref="HttpListenerResponse.RedirectLocation"/>. </summary>
		public string RedirectLocation { get { return raw.RedirectLocation; } set { raw.RedirectLocation = value; } }
		/// <summary> Gets/sets the protocol version. Wraps into <see cref="HttpListenerResponse.ProtocolVersion"/>. </summary>
		public Version ProtocolVersion { get { return raw.ProtocolVersion; } set { raw.ProtocolVersion = value; } }
		/// <summary> Gets/sets the response headers. Wraps into <see cref="HttpListenerResponse.Headers"/>. </summary>
		public WebHeaderCollection Headers { get { return raw.Headers; } set { raw.Headers = value; } }
		/// <summary> Gets/sets the response cookies. Wraps into <see cref="HttpListenerResponse.Cookies"/>. </summary>
		public CookieCollection Cookies { get { return raw.Cookies; } set { raw.Cookies = value; } }
		/// <summary> Gets/sets the encoding to use. Wraps into <see cref="HttpListenerResponse.ContentEncoding"/>. </summary>
		public Encoding ContentEncoding { get { return raw.ContentEncoding; } set { raw.ContentEncoding = value; } }
		/// <summary> Gets the output stream. Wraps into <see cref="HttpListenerResponse.OutputStream"/>. </summary>
		public Stream OutputStream { get { return raw.OutputStream; } }

		/// <summary> Wraps <see cref="HttpListenerResponse.Abort"/>. </summary>
		public void Abort() { raw.Abort(); }
		/// <summary> Wraps <see cref="HttpListenerResponse.AddHeader(string, string)"/>. </summary>
		public void AddHeader(string name, string value) { raw.AddHeader(name, value); }
		/// <summary> Wraps <see cref="HttpListenerResponse.AppendCookie(Cookie)"/>. </summary>
		public void AppendCookie(Cookie cookie) { raw.AppendCookie(cookie); }
		/// <summary> Wraps <see cref="HttpListenerResponse.AppendHeader(string, string)"/>. </summary>
		public void AppendHeader(string name, string value) { raw.AppendHeader(name, value); }
		/// <summary> Wraps <see cref="HttpListenerResponse.Close(byte[], bool)"/>. </summary>
		public void Close(byte[] responseEntity, bool willBlock) { raw.Close(responseEntity, willBlock); }
		/// <summary> Wraps <see cref="HttpListenerResponse.Close"/>. </summary>
		public void Close() { raw.Close(); }
		/// <summary> Wraps <see cref="HttpListenerResponse.CopyFrom(HttpListenerResponse)"/>. </summary>
		public void CopyFrom(HttpListenerResponse templateResponse) { raw.CopyFrom(templateResponse); }
		/// <summary> Wraps <see cref="HttpListenerResponse.Redirect(string)"/>. </summary>
		public void Redirect(string url) { raw.Redirect(url); }
		/// <summary> Wraps <see cref="HttpListenerResponse.SetCookie(Cookie)"/>. </summary>
		public void SetCookie(Cookie cookie) { raw.SetCookie(cookie); }
		#endregion

		/// <summary> Currently assigned object representing the body. </summary>
		public object body { get; set; }
	}

	/// <summary> Contains entry points standing up http servers. </summary>
	public class HttpServer {
		/// <summary> Default restart timer of 1 second. </summary>
		public static readonly int DEFAULT_RESTART_DELAY = 1000;

		/// <summary> Array of prefixes for the <see cref="HttpListener"/> to accept </summary>
		public string[] prefixes { get; private set; }
		/// <summary> Time in milliseconds between attempted restarts </summary>
		public int restartDelay { get; private set; }
		/// <summary> <see cref="Middleware"/> to use when handling requests </summary>
		private Middleware[] middleware { get; set; }
		/// <summary> Is this listener currently running? </summary>
		public bool running { get; private set; }
		/// <summary> <see cref="Task{TResult}"/> running the listener </summary>
		public Task<int> serverTask { get; private set; }
		/// <summary> <see cref="HttpListener"/>. </summary>
		private HttpListener listener { get; set; }

		public HttpServer(string prefix, params Middleware[] middleware) {
			this.prefixes = new string[] { prefix };
			this.restartDelay = DEFAULT_RESTART_DELAY;
			this.middleware = middleware;
			Initialize();
		}
		public HttpServer(string[] prefixes, params Middleware[] middleware) {
			this.prefixes = prefixes;
			this.restartDelay = DEFAULT_RESTART_DELAY;
			this.middleware = middleware;
			Initialize();
		}
		public HttpServer(string prefix, int restartDelay, params Middleware[] middleware) {
			this.prefixes = new string[] { prefix };
			this.restartDelay = restartDelay;
			this.middleware = middleware;
			Initialize();
		}
		public HttpServer(string[] prefixes, int restartDelay, params Middleware[] middleware) {
			this.prefixes = prefixes;
			this.restartDelay = restartDelay;
			this.middleware = middleware;
			Initialize();
		}

		private void Initialize() {
			prefixes = prefixes.Select(it => (it.EndsWith("/") ? it : it + "/")).ToArray();
			running = true;
			listener = new HttpListener();
			foreach (var prefix in prefixes) { listener.Prefixes.Add(prefix); }

			serverTask = Listen(listener, () => running, middleware);
		}

		public void Stop() {
			listener.Abort();
			running = false;
		}

		/// <summary> Continuously retries hosting an HTTP server </summary>
		/// <param name="prefixes"> List of hostnames/ports to accept. </param>
		/// <param name="stayOpen"> Delegate that provides a <see cref="bool"/> value. True while the server should stay open. </param>
		/// <param name="restartDelay"> Millisecond delay between restarts, should the program crash.</param>
		/// <param name="middleware"> Array of <see cref="Middleware"/> to use </param>
		/// <returns> Task that completes when the server stops listening. </returns>
		public static async Task<int> Watch(string[] prefixes, Func<bool> stayOpen, int restartDelay, params Middleware[] middleware) {
			prefixes = prefixes.Select(it => (it.EndsWith("/") ? it : it + "/")).ToArray();

			int ret = 1;
			while (ret != 0) {
				ret = await Serve(prefixes, stayOpen, middleware);
				await Task.Delay(restartDelay);
			}

			return 0;
		}
		/// <summary> Continuously retries hosting an HTTP server with default settings </summary>
		/// <param name="prefix"> hostname/port to accept </param>
		/// <param name="middleware"> Array of <see cref="Middleware"/> to use </param>
		/// <returns> Task that completes when the server stops listening. </returns>
		public static Task<int> Watch(string prefix, params Middleware[] middleware) {
			return Watch(new string[] { prefix }, () => true, DEFAULT_RESTART_DELAY, middleware);
		}
		/// <summary> Continuously retries hosting an HTTP server with default settings </summary>
		/// <param name="prefix"> hostname/port to accept </param>
		/// <param name="stayOpen"> Delegate that provides a <see cref="bool"/> value. True while the server should stay open. </param>
		/// <param name="middleware"> Array of <see cref="Middleware"/> to use </param>
		/// <returns> Task that completes when the server stops listening. </returns>
		public static Task<int> Watch(string prefix, Func<bool> stayOpen, params Middleware[] middleware) {
			return Watch(new string[] { prefix }, stayOpen, DEFAULT_RESTART_DELAY, middleware);
		}

		/// <summary> Begins hosting an HTTP server</summary>
		/// <param name="prefixes"> List of hostnames/ports to accept. </param>
		/// <param name="stayOpen"> Delegate that provides a <see cref="bool"/> value. True while the server should stay open. </param>
		/// <param name="middleware"> Array of middleware to use </param>
		/// <returns> Task that completes when the server stops listening. </returns>
		public static async Task<int> Serve(string[] prefixes, Func<bool> stayOpen, params Middleware[] middleware) {
			if (!HttpListener.IsSupported) {
				Console.WriteLine("Cannot start HTTP server, unsupported on this platform.");
				return -2;
			}

			if (prefixes == null || prefixes.Length == 0) { return -1; }
			using (HttpListener listener = new HttpListener()) {
				foreach (var prefix in prefixes) { listener.Prefixes.Add(prefix); }
				return await Listen(listener, stayOpen, middleware);
			}
		}

		/// <summary> Listen for requests </summary>
		/// <param name="listener"> <see cref="HttpListener"/> to use to listen with </param>
		/// <param name="stayOpen"> <see cref="Func{TResult}"/> to use to stay open </param>
		/// <param name="middleware"> <see cref="Middleware[]"/> to use when handling requests </param>
		/// <returns> Task that completes once the server stops listening (via stayOpen becoming false or otherwise aborting the listener) </returns>
		private static async Task<int> Listen(HttpListener listener, Func<bool> stayOpen, Middleware[] middleware) {
			try {
				listener.Start();
				while (stayOpen()) {
					Ctx ctx = new Ctx(await listener.GetContextAsync());
					// Toss into other task and resume listening.
					var _ = Task.Run(async () => {
						await Handle(ctx, middleware);
						await Finish(ctx);
					});
				}
			} catch (Exception e) {
				if (e is HttpListenerException && (e as HttpListenerException).ErrorCode == 995) { return 0; }
				Console.WriteLine("HttpServer.Listen: Internal Error - " + e);
				return -1;
			}
			return 0;
		}

		/// <summary> Begins hosting an HTTP server</summary>
		/// <param name="prefix"> hostname/port to accept. </param>
		/// <param name="middleware"> Array of middleware to use </param>
		/// <returns> Task that completes when the server stops listening. </returns>
		public static Task<int> Serve(string prefix, params Middleware[] middleware) {
			return Serve(new string[] { prefix }, () => true, middleware);
		}

		/// <summary> Inner function used to handle HTTP contexts. </summary>
		/// <param name="ctx"> Context to handle </param>
		/// <param name="middleware"> Middleware stack to use </param>
		/// <returns> Task that completes when request has been handled. </returns>
		public static async Task Handle(Ctx ctx, Middleware[] middleware) {
			NextFn next(int i) {
				return async () => {
					if (i + 1 >= middleware.Length) { return; }
					await middleware[i + 1](ctx, next(i + 1));
				};
			}

			try {
				// Kick everything off
				await next(-1)();
			} catch (Exception e) {
				// Top level exception, srs issue.
				Console.WriteLine($"HttpServer.Handle: Internal error in context: {ctx}\n{e}");
				ctx.StatusCode = 500;
				ctx.StatusDescription = $"Internal Server Error";
				ctx.ContentType = "text/plain; charset=utf-8";
				ctx.body = $"500 Internal Server Error {e.GetType()} / {e.Message} \nStack Trace:\n{e.StackTrace}";
			}

		}

		/// <summary> Logic to finish handling a <see cref="Ctx"/> object. </summary>
		/// <param name="ctx"> Request/Response <see cref="Ctx"/> object </param>
		/// <returns> <see cref="Task"/> that completes when the <paramref name="ctx"/> has been finished</returns>
		private static async Task Finish(Ctx ctx) {
			ctx.res.Headers["Server"] = "MiniHttp/0.0.1 +";

			if (ctx.body == null) {
				ctx.StatusCode = 404;
				ctx.StatusDescription = "Not Found";
				ctx.body = "404 Not Found";
			}
			byte[] data = ctx.body as byte[];
			if (ctx.body is JsonObject || ctx.body is JsonArray) {
				ctx.ContentType = "application/json;charset=utf-8";
				ctx.ContentEncoding = Encoding.UTF8;
				ctx.body = ctx.body.ToString();
			}
			if (ctx.body is string) {
				if (ctx.ContentType == null) {
					ctx.ContentEncoding = Encoding.UTF8;
					ctx.ContentType = "text/plain;charset=utf-8";
				}
				data = ctx.ContentEncoding.GetBytes(ctx.body as string);
			}
			if (data == null) {
				ctx.ContentType = "text/plain;charset=utf-8";
				data = ctx.ContentEncoding.GetBytes("");
			}

			var res = ctx.res.raw;
			res.ContentLength64 = data.Length;
			await res.OutputStream.WriteAsync(data, 0, data.Length);

			if (!ctx.KeepAlive) {
				res.OutputStream.Close();
			}
		}

	}

	/// <summary> Class for handling route matching </summary>
	public class Router {
		/// <summary> Class for holding information about a single Route. </summary>
		public class Route {
			/// <summary> HTTP Method to match, or "*" to match any </summary>
			public string method { get; private set; }
			/// <summary> Route pattern to match. </summary>
			public string pattern { get; private set; }
			/// <summary> Pre-split pattern </summary>
			public string[] splitPattern { get; private set; }
			/// <summary> Middleware to use when matching, in order. </summary>
			public Middleware[] handlers { get; private set; }
			/// <summary> Constructor </summary>
			/// <param name="method"> HTTP Method to match (GET, POST, PUT, etc, or "*" for any) </param>
			/// <param name="pattern"> Path pattern to match </param>
			/// <param name="handlers"> <see cref="Middleware"/> set to use when matching this route </param>
			public Route(string method, string pattern, params Middleware[] handlers) {
				if (!pattern.StartsWith('/')) { pattern = '/' + pattern; }
				this.method = method;
				this.pattern = pattern;
				splitPattern = pattern.Split("/").Where(it => it != "").ToArray();
				this.handlers = handlers;
			}
			/// <inheritdoc/>
			public override string ToString() { return FmtPath(splitPattern); }
		}
		/// <summary> Empty list of middleware. </summary>
		private static readonly List<Middleware> EMPTY_MIDDLEWARE = new List<Middleware>();

		/// <summary> List of <see cref="Route"/>s to match </summary>
		private List<Route> routes = new List<Route>();
		/// <summary> List of <see cref="Middleware"/> to always use, given a route has been matched. </summary>
		private List<Middleware> always = new List<Middleware>();

		/// <summary> Bake a <see cref="Middleware[]"/> from the given <paramref name="handlers"/> 
		/// and the current set of <see cref="Middleware"/> registered to <see cref="always"/>. </summary>
		/// <param name="handlers"> <see cref="Middleware"/> set to augment </param>
		/// <returns> Baked <see cref="Middleware[]"/> containing all of the content of <see cref="always"/> followed by <paramref name="handlers"/>. </returns>
		private Middleware[] Augment(IEnumerable<Middleware> handlers) {
			List<Middleware> copy = new List<Middleware>(always);
			copy.AddRange(handlers);
			return copy.ToArray();
		}

		/// <summary> <see cref="Middleware"/> to use before any matching routes. </summary>
		/// <param name="handlers"> <see cref="Middleware"/> objects to use, in order </param>
		public void Use(params Middleware[] handlers) {
			if (routes.Count > 0) { throw new Exception("Router.Use: Please register all Middleware with .Use() _before_ registering any routes with Get/Post/Put/etc."); }
			always.AddRange(handlers);
		}
		/// <summary> Configure to match any HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="Middleware"/> handlers to use when pattern is matched </param>
		public void Any(string pattern, params Middleware[] handlers) { routes.Add(new Route("*", pattern, Augment(handlers))); }
		/// <summary> Configure to match any HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="SyncMiddleware"/> handlers to use when pattern is matched </param>
		public void Any(string pattern, params SyncMiddleware[] handlers) { routes.Add(new Route("*", pattern, Augment(handlers.Select(it => SyncWrap(it))))); }

		/// <summary> Configure to match only the GET HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="Middleware"/> handlers to use when method and pattern is matched </param>
		public void Get(string pattern, params Middleware[] handlers) { routes.Add(new Route("GET", pattern, Augment(handlers))); }
		/// <summary> Configure to match only the GET HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="SyncMiddleware"/> handlers to use when pattern is matched </param>
		public void Get(string pattern, params SyncMiddleware[] handlers) { routes.Add(new Route("GET", pattern, Augment(handlers.Select(it => SyncWrap(it))))); }

		/// <summary> Configure to match only the HEAD HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="Middleware"/> handlers to use when method and pattern is matched </param>
		public void Head(string pattern, params Middleware[] handlers) { routes.Add(new Route("HEAD", pattern, Augment(handlers))); }
		/// <summary> Configure to match only the HEAD HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="SyncMiddleware"/> handlers to use when pattern is matched </param>
		public void Head(string pattern, params SyncMiddleware[] handlers) { routes.Add(new Route("HEAD", pattern, Augment(handlers.Select(it => SyncWrap(it))))); }

		/// <summary> Configure to match only the POST HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="Middleware"/> handlers to use when method and pattern is matched </param>
		public void Post(string pattern, params Middleware[] handlers) { routes.Add(new Route("POST", pattern, Augment(handlers))); }
		/// <summary> Configure to match only the POST HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="SyncMiddleware"/> handlers to use when pattern is matched </param>
		public void Post(string pattern, params SyncMiddleware[] handlers) { routes.Add(new Route("POST", pattern, Augment(handlers.Select(it => SyncWrap(it))))); }

		/// <summary> Configure to match only the PUT HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="Middleware"/> handlers to use when method and pattern is matched </param>
		public void Put(string pattern, params Middleware[] handlers) { routes.Add(new Route("PUT", pattern, Augment(handlers))); }
		/// <summary> Configure to match only the PUT HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="SyncMiddleware"/> handlers to use when pattern is matched </param>
		public void Put(string pattern, params SyncMiddleware[] handlers) { routes.Add(new Route("PUT", pattern, Augment(handlers.Select(it => SyncWrap(it))))); }

		/// <summary> Configure to match only the DELETE HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="Middleware"/> handlers to use when method and pattern is matched </param>
		public void Delete(string pattern, params Middleware[] handlers) { routes.Add(new Route("DELETE", pattern, Augment(handlers))); }
		/// <summary> Configure to match only the DELETE HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="SyncMiddleware"/> handlers to use when pattern is matched </param>
		public void Delete(string pattern, params SyncMiddleware[] handlers) { routes.Add(new Route("DELETE", pattern, Augment(handlers.Select(it => SyncWrap(it))))); }

		/// <summary> Configure to match only the OPTIONS HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="Middleware"/> handlers to use when method and pattern is matched </param>
		public void Options(string pattern, params Middleware[] handlers) { routes.Add(new Route("OPTIONS", pattern, Augment(handlers))); }
		/// <summary> Configure to match only the OPTIONS HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="SyncMiddleware"/> handlers to use when pattern is matched </param>
		public void Options(string pattern, params SyncMiddleware[] handlers) { routes.Add(new Route("OPTIONS", pattern, Augment(handlers.Select(it => SyncWrap(it))))); }

		/// <summary> Configure to match only the CONNECT HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="Middleware"/> handlers to use when method and pattern is matched </param>
		public void Connect(string pattern, params Middleware[] handlers) { routes.Add(new Route("CONNECT", pattern, Augment(handlers))); }
		/// <summary> Configure to match only the CONNECT HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="SyncMiddleware"/> handlers to use when pattern is matched </param>
		public void Connect(string pattern, params SyncMiddleware[] handlers) { routes.Add(new Route("CONNECT", pattern, Augment(handlers.Select(it => SyncWrap(it))))); }

		/// <summary> Configure to match only the TRACE HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="Middleware"/> handlers to use when method and pattern is matched </param>
		public void Trace(string pattern, params Middleware[] handlers) { routes.Add(new Route("TRACE", pattern, Augment(handlers))); }
		/// <summary> Configure to match only the TRACE HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="SyncMiddleware"/> handlers to use when pattern is matched </param>
		public void Trace(string pattern, params SyncMiddleware[] handlers) { routes.Add(new Route("TRACE", pattern, Augment(handlers.Select(it => SyncWrap(it))))); }

		/// <summary> Configure to match only the PATCH HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="Middleware"/> handlers to use when method and pattern is matched </param>
		public void Patch(string pattern, params Middleware[] handlers) { routes.Add(new Route("PATCH", pattern, Augment(handlers))); }
		/// <summary> Configure to match only the PATCH HTTP Method, and the given <paramref name="pattern"/>, to run the given <paramref name="handlers"/>. </summary>
		/// <param name="pattern"> Path pattern to match </param>
		/// <param name="handlers"> <see cref="SyncMiddleware"/> handlers to use when pattern is matched </param>
		public void Patch(string pattern, params SyncMiddleware[] handlers) { routes.Add(new Route("PATCH", pattern, Augment(handlers.Select(it => SyncWrap(it))))); }



		/// <summary> Implicit conversion to <see cref="Middleware"/> via <see cref="Routes"/> </summary>
		/// <param name="rt"> Router to convert </param>
		public static implicit operator Middleware(Router rt) { return rt.Routes; }
		/// <summary> Returns the <see cref="Handle(Ctx, NextFn)"/> as a <see cref="Middleware"/> delegate </summary>
		public Middleware Routes { get { return Handle; } }

		/// <summary> Function that handles matching and handling a request </summary>
		/// <param name="ctx"> Request context to handle </param>
		/// <param name="next"> Next middleware function to call </param>
		/// <returns> Async <see cref="Task"/> representing work. </returns>
		private async Task Handle(Ctx ctx, NextFn next) {
			// TODO: Maybe a match scoring system?
			//Console.WriteLine($"Matching request {ctx}");

			Route bestMatch = null;
			foreach (var route in routes) {
				//Console.WriteLine($"To Route {route}");
				if (PathMatches(ctx, route)) {
					bestMatch = route;
					if (route.method == "*" || route.method == ctx.HttpMethod) {
						//Console.WriteLine($"Matched route {route} and method {ctx.HttpMethod}");
						break;
					} else {
						//Console.WriteLine($"Matched route {route} but not method {ctx.HttpMethod}");
					}
				}
			}

			if (bestMatch != null && (bestMatch.method == ctx.HttpMethod || bestMatch.method == "*")) {
				await HttpServer.Handle(ctx, bestMatch.handlers);
			} else if (bestMatch != null) {
				ctx.body = $"Cannot {ctx.HttpMethod} /{string.Join('/', ctx.pathSplit)}";
				ctx.StatusCode = 405;
				ctx.StatusDescription = "Method Not Allowed";
				await next();
			} else {
				await next();
			}

		}

		/// <summary> Function to use to test that a Request's <see cref="Ctx"/> matches a given <see cref="Route"/>, 
		/// and extracts any <see cref="Ctx.param"/>s from the URL </summary>
		/// <param name="ctx"> Request <see cref="Ctx"/> to process </param>
		/// <param name="route"> <see cref="Route"/> to attempt to match </param>
		/// <returns> true if match, false otherwise. </returns>
		public static bool PathMatches(Ctx ctx, Route route) {
			string[] routePath = route.splitPattern;
			string[] requestPath = ctx.pathSplit;

			JsonObject vars = new JsonObject();
			int start = ctx.midData.Pull("pathMatchedTo", 0);
			int matchedTo = start;
			bool maybeMatch(bool match) {
				if (match) {
					//Console.WriteLine($"Router matched {matchedTo} sections of path");
					ctx.midData["pathMatchedTo"] = matchedTo;
					ctx.param.Set(vars);
				}
				return match;
			}

			string lastMatchedPart = "";
			int n = routePath.Length;
			int remainingParts = requestPath.Length - start;
			//Console.WriteLine($"Matching {n} steps starting at {start}");
			for (int i = 0; i < n; i++) {
				string routePart = routePath[i];
				if (start + i >= requestPath.Length) {
					//Console.WriteLine($"Matched up to {routePart}");
					matchedTo = i;

					return maybeMatch(i == n - 1 && routePart == "*");
				}
				string requestPart = requestPath[start + i];
				//Console.WriteLine($"Matching part {routePart} to {requestPart}");

				if (routePart.StartsWith(":")) {
					vars[routePart.Substring(1)] = requestPart;
					matchedTo = i + 1;
					lastMatchedPart = routePart;
				} else if (routePart == "*") {
					matchedTo = i;
					lastMatchedPart = "*";
					//Console.WriteLine($"Router matched wildcard route: {route}");
					return maybeMatch(true);
				} else if (routePart != requestPart) {
					return false;
				} else {
					matchedTo = i + 1;
				}
			}

			if (lastMatchedPart != "*" && routePath.Length < remainingParts) {
				//Console.WriteLine($"Not a match, non-wild card on route of length {routePath.Length} vs request with remaining {remainingParts} parts");
				return false;
			}

			//Console.WriteLine($"Router matched {route}");
			return maybeMatch(true);
		}
	}

	/// <summary> Class holding some default middleware that can be useful. </summary>
	public static class ProvidedMiddleware {

		/// <summary> Returns a middleware function that prints a trace message 
		/// with the given <paramref name="traceNum"/>, both before and after the rest of the stack</summary>
		/// <param name="traceNum"> Number to print in trace message </param>
		/// <returns> <see cref="Middleware"/> that prints trace message and <paramref name="traceNum"/>. </returns>
		public static Middleware MakeTrace(int traceNum) {
			return async (ctx, next) => {
				Console.WriteLine($"Trace Before {traceNum}");
				await next();
				Console.WriteLine($"Trace After {traceNum}");
			};
		}
		/// <summary> General "BodyParser" middleware, similar to ExpressJS. </summary>
		public static readonly Middleware BodyParser = async (ctx, next) => {
			byte[] buffer = new byte[2048];
			using (MemoryStream stream = new MemoryStream()) {
				int read = 0;
				int size = 0;
				do {
					read = await ctx.InputStream.ReadAsync(buffer, 0, 2048);
					if (read > 0) {
						size += read;
						await stream.WriteAsync(buffer, 0, read);
					}

				} while (read > 0);

				byte[] final = stream.ToArray();
				if (ctx.req.ContentEncoding != null) {
					ctx.req.body = ctx.req.ContentEncoding.GetString(final).Replace("\r\n", "\n");
					try {
						JsonValue result = Json.ParseStrict(ctx.req.body);
						if (result is JsonObject) { ctx.req.bodyObj = result as JsonObject; }
						if (result is JsonArray) { ctx.req.bodyArr = result as JsonArray; }
					} catch (Exception) { }
				} else {
					ctx.req.data = final;
				}
			}
			await next();
		};

		/// <summary> Default map of extension -> MIME type</summary>
		/// <remarks> From https://developer.mozilla.org/en-US/docs/Web/HTTP/Basics_of_HTTP/MIME_types/Common_types </remarks>
		public static readonly Dictionary<string, string> CONTENT_TYPES = new Dictionary<string, string>() {
			{ "aac", "audio/aac" },
			{ "abw", "application/x-abiword" },
			{ "arc", "application/x-freearc" },
			{ "avi", "video/x-msvideo" },
			{ "azw", "application/vnd.amazon.ebook" },
			{ "bin", "application/octet-stream" },
			{ "bmp", "image/bmp" },
			{ "bz", "application/x-bzip" },
			{ "bz2", "application/x-bzip2" },
			{ "csh", "application/x-csh" },
			{ "css", "text/css" },
			{ "csv", "text/csv" },
			{ "doc", "application/msword" },
			{ "docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
			{ "eot", "application/vnd.ms-fontobject" },
			{ "epub", "application/epub+zip" },
			{ "gz", "application/gzip" },
			{ "gif", "image/gif" },
			{ "html", "text/html" },
			{ "htm", "text/html" },
			{ "ico", "image/vnd.microsoft.icon" },
			{ "ics", "text/calendar" },
			{ "jar", "application/java-archive" },
			{ "jpeg", "image/jpeg" },
			{ "jpg", "image/jpeg" },
			{ "js", "text/javascript" },
			{ "json", "application/json" },
			{ "jsonld", "application/ld+json" },
			{ "midi", "audio/midi audio/x-midi" },
			{ "mid", "audio/midi audio/x-midi" },
			{ "mjs", "text/javascript" },
			{ "mp3", "audio/mpeg" },
			{ "mpeg", "video/mpeg" },
			{ "mp4", "video/mp4" },
			{ "mpkg", "application/vnd.apple.installer+xml" },
			{ "odp", "application/vnd.oasis.opendocument.presentation" },
			{ "ods", "application/vnd.oasis.opendocument.spreadsheet" },
			{ "odt", "application/vnd.oasis.opendocument.text" },
			{ "oga", "audio/ogg" },
			{ "ogv", "video/ogg" },
			{ "ogx", "application/ogg" },
			{ "opus", "audio/opus" },
			{ "otf", "font/otf" },
			{ "png", "image/png" },
			{ "pdf", "application/pdf" },
			{ "php", "application/x-httpd-php" },
			{ "ppt", "application/vnd.ms-powerpoint" },
			{ "pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
			{ "rar", "application/vnd.rar" },
			{ "rtf", "application/rtf" },
			{ "sh", "application/x-sh" },
			{ "svg", "image/svg+xml" },
			{ "swf", "application/x-shockwave-flash" },
			{ "tar", "application/x-tar" },
			{ "tif", "image/tiff" },
			{ "tiff", "image/tiff" },
			{ "ts", "video/mp2t" },
			{ "ttf", "font/ttf" },
			{ "txt", "text/plain" },
			{ "vsd", "application/vnd.visio" },
			{ "wav", "audio/wav" },
			{ "weba", "audio/webm" },
			{ "webm", "video/webm" },
			{ "webp", "image/webp" },
			{ "woff", "font/woff" },
			{ "woff2", "font/woff2" },
			{ "xhtml", "application/xhtml+xml" },
			{ "xls", "application/vnd.ms-excel" },
			{ "xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
			{ "xml", "application/xml" },
			{ "xul", "application/vnd.mozilla.xul+xml" },
			{ "zip", "application/zip" },
			{ "3gp", "video/3gpp" },
			{ "3g2", "video/3gpp2" },
			{ "7z", "application/x-7z-compressed" },
		};
		/// <summary> Default list of default document filenames to match at roots. </summary>
		public static readonly List<string> DEFAULT_DOCUMENTS = new List<string>() {
			"index.html",
			"index.htm",
			"default.html",
			"default.htm",
		};

		/// <summary> General static file hosting, works with the <see cref="Router"/>'s path matching when mounted. </summary>
		/// <param name="path"> Local folder path to host statically </param>
		/// <param name="defaultDocuments"> <see cref="List{string}"/> of filenames to automatically try to match before failing. Defaults to <see cref="DEFAULT_DOCUMENTS"/> if not provided. </param>
		/// <param name="contentTypes"> <see cref="Dictionary{string, string}"/> to use to match extensions to MIME types. Defaults to <see cref="CONTENT_TYPES"/> if not provided. </param>
		/// <returns> <see cref="Middleware"/> which statically hosts files in the given folder. </returns>
		public static Middleware Static(string path = ".", List<string> defaultDocuments = null, Dictionary<string, string> contentTypes = null) {
			if (path == "") { path = "./"; }
			if (!path.EndsWith("/")) { path += "/"; }
			if (contentTypes == null) { contentTypes = CONTENT_TYPES; }
			if (defaultDocuments == null) { defaultDocuments = DEFAULT_DOCUMENTS; }

			return async (ctx, next) => {
				int start = ctx.midData.Pull("pathMatchedTo", 0);
				// Console.WriteLine($"Starting static file check at {start}");
				string[] requestPath = ctx.pathSplit;
				List<string> remaining = new List<string>();
				for (int i = start; i < requestPath.Length; i++) {
					remaining.Add(requestPath[i]);
				}
				string remainingPath = string.Join('/', remaining);
				string localPath = path + remainingPath;
				// Console.WriteLine($"Looking for static file matching {{{remainingPath}}} at {{{localPath}}}");
				var res = ctx.res;

				if (File.Exists(localPath)) {
					//Console.WriteLine("Found file.");

					string ext = HttpServerHelpers.FromLast(ctx.pathSplit[ctx.pathSplit.Length - 1], ".");
					res.body = File.ReadAllBytes(localPath);
					res.StatusCode = 200;
					res.StatusDescription = "Ok, Found";
					res.ContentType = contentTypes.ContainsKey(ext) ? contentTypes[ext] : "?/?";

				} else {
					foreach (var doc in defaultDocuments) {
						string checkPath = localPath + "/" + doc;
						if (File.Exists(checkPath)) {
							//Console.WriteLine($"Found default document matching {{{doc}}}");
							string ext = HttpServerHelpers.FromLast(doc, ".");
							res.body = File.ReadAllBytes(checkPath);
							res.StatusCode = 200;
							res.StatusDescription = "Ok, Found";
							res.ContentType = contentTypes.ContainsKey(ext) ? contentTypes[ext] : "?/?";
							return;
						}
					}


					//Console.WriteLine("Not found, using next middleware.");
					await next();
				}



			};
		}

		/// <summary> General debugging "Inspect" middleware. 
		/// Prints out various properties of a request before and after the rest of the Middleware stack. </summary>
		public static readonly Middleware Inspect = async (ctx, next) => {
			void print(string prefix) {
				Console.WriteLine($"{prefix}: {ctx}");
				Console.WriteLine($"Headers:\n{ctx.req.Headers}");
				Console.WriteLine($"Query: {ctx.query}");
				Console.WriteLine($"Params: {ctx.param}");
				Console.WriteLine($"Raw body: {ctx.req.body}");
				Console.WriteLine($"Body Object: {ctx.req.bodyObj?.ToString()}");
				Console.WriteLine($"Body Array: {ctx.req.bodyArr?.ToString()}");
			}
			print("\nInspect Before");
			await next();
			print("\nInspect After");
		};

	}

	/// <summary> Contains some helper methods that may need to be used globally. </summary>
	public static class HttpServerHelpers {
		/// <summary> Convert the <see cref="NameValueCollection"/> type into a <see cref="JsonObject"/> </summary>
		/// <param name="coll"> <see cref="NameValueCollection"/> to convert </param>
		/// <returns> <see cref="JsonObject"/> containing the same information as <paramref name="coll"/> </returns>
		public static JsonObject ToJsonObject(this NameValueCollection coll) {
			JsonObject obj = new JsonObject();

			string[] keys = coll.AllKeys;
			for (int i = 0; i < keys.Length; i++) {
				string key = keys[i];
				string value = coll[key];
				// Console.WriteLine($"Pair {i} - \"{key}\"=\"{value}\"");

				if (key == null) {
					// Invert this and set them as flags as true...
					// Why would you use null as the KEY?
					string[] values = value.Split(',');
					foreach (var val in values) { obj[val] = true; }
				} else {
					// Otherwise treat singletons as a value, lists as an array.
					if (value.Contains(',')) {
						string[] values = value.Split(',');
						obj[key] = new JsonArray().AddAll(values);
					} else {
						obj[key] = value;
					}
				}
			}
			return obj;
		}

		/// <summary> Get the content of the <paramref name="str"/> <see cref="string"/> up until the first occurence of <paramref name="search"/> </summary>
		/// <param name="str"> <see cref="string"/> to scan </param>
		/// <param name="search"> <see cref="string"/> to search for </param>
		/// <returns> <see cref="string.Substring(int, int)"/> from 0 to the index where <paramref name="search"/> was found </returns>
		public static string UpToFirst(string str, string search) {
			if (str.Contains(search)) {
				int ind = str.IndexOf(search);
				return str.Substring(0, ind);
			}
			return str;
		}
		/// <summary> Get the content of the <paramref name="str"/> <see cref="string" /> from after the last occurence of <paramref name="search"/> </summary>
		/// <param name="str"> <see cref="string"/> to scan </param>
		/// <param name="search"> <see cref="string"/> to search for </param>
		/// <returns> <see cref="string.Substring(int, int)"/> from the index just after where <paramref name="search"/> was found to the end of the string </returns>
		public static string FromLast(this string str, string search) {
			if (str.Contains(search) && !str.EndsWith(search)) {
				int ind = str.LastIndexOf(search);

				return str.Substring(ind + 1);
			}
			return "";
		}

		/// <summary> Helper to format paths for printing </summary>
		/// <param name="paths"> </param>
		/// <returns></returns>
		public static string FmtPath(string[] paths) {
			return "/" + string.Join(" -> ", paths.Select(it => (it == null || it == "") ? "/" : it));
		}

		/// <summary> Helper to wrap <see cref="SyncMiddleware"/> in an optimal non-async <see cref="Task"/> returning function. </summary>
		/// <param name="middleware"> <see cref="SyncMiddleware"/> to call </param>
		/// <returns> Precompleted <see cref="Task"/> </returns>
		public static Middleware SyncWrap(SyncMiddleware middleware) {
			return (ctx, next) => { middleware(ctx, next); return Task.CompletedTask; };
		}
	}
	public static class MiniHttp_Tests {

		public static void TestBasic() {
			List<Middleware> middleware = new List<Middleware>();
			//middleware.Add(Inspect);
			middleware.Add(BodyParser);

			Router router = new Router();
			router.Get("/", (ctx, next) => { ctx.body = "Aww yeet"; });
			router.Get("/what", (ctx, next) => { ctx.body = "lolwhat"; });
			router.Get("/what/:id", (ctx, next) => { ctx.body = "lolwhat #" + ctx.param["id"].stringVal; });
			router.Post("/", (ctx, next) => { ctx.body = $"omg you sent \"{ctx.req.body}\" "; });

			Router lower = new Router();
			lower.Get("/ayy", (ctx, next) => { ctx.body = $"{ctx.param["id"].stringVal}'s Ayy"; });
			lower.Get("/bee", (ctx, next) => { ctx.body = $"{ctx.param["id"].stringVal}'s Bee"; });
			lower.Get("/cee", (ctx, next) => { ctx.body = new JsonObject("id", ctx.param["id"], "test", 5); });
			lower.Get("/", (ctx, next) => { ctx.body = $"{ctx.param["id"].stringVal}'s Homepage"; });

			router.Any("/lower/:id/*", lower);

			middleware.Add(router);
			middleware.Add(Static("./public"));
			string baseUrl = "http://localhost:3001";

			var server = new HttpServer(baseUrl, middleware.ToArray());

			List<Task<string>> tasks = new List<Task<string>>();
			void testGet(string path, string expected) {
				tasks.Add(Request.Get(baseUrl + path, (result) => {
					if (result != expected) { throw new Exception($"Did not recieve expected result \"{result}\"."); }
				}));
			}
			void testPost(string path, string payload, string expected) {
				tasks.Add(Request.Post(baseUrl + path, payload, (result) => {
					if (result != expected) { throw new Exception($"Did not recieve expected result \"{result}\"."); }
				}));
			}
			Action<string, Exception> onError = (s, e) => {
				Console.WriteLine($"{s}: {e.InfoString()}");
			};
			Request.onError += onError;

			testGet("/", "Aww yeet");
			testGet("/what", "lolwhat");
			testGet("/what/12345", "lolwhat #12345");

			testPost("/", "boat", "omg you sent \"boat\" ");

			testGet("/lower/123", "123's Homepage");
			testGet("/lower/123/ayy", "123's Ayy");
			testGet("/lower/123/bee", "123's Bee");
			testGet("/lower/123/cee", "{\"id\":\"123\",\"test\":5}");

			foreach (var t in tasks) {
				t.Wait();
				if (t.Exception != null) { throw t.Exception; }
			}

			Request.onError -= onError;

			testGet(baseUrl, null);
			server.Stop();
			server.serverTask.Wait();

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
}
