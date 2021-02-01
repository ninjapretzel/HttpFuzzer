using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Ex {

	/// <summary> Tiny request library </summary>
	public static class Request {
		/// <summary> Instance of <see cref="HttpClient"/> singleton. </summary>
		public static readonly HttpClient http = new HttpClient();

		/// <summary> Callback to fire on any errors (eg for logging) </summary>
		public static Action<string, Exception> onError;

		/// <summary> Attempt to GET the given URL and handle the response as a <see cref="string"/></summary>
		/// <param name="url"> URL to GET from </param>
		/// <param name="callback"> Callback to fire on success </param>
		/// <returns> Task that can be awaited for the completion. </returns>
		public static Task<string> Get(string url, Action<string> callback = null) {
			return Task.Run(() => GetAsync(url, callback));
		}
		/// <summary> Attempt to GET the given URL and handle the response as a <see cref="string"/></summary>
		/// <param name="url"> URL to GET from </param>
		/// <param name="callback"> Callback to fire on success </param>
		/// <returns> Awaitable <see cref="Task"/> </returns>
		public static async Task<string> GetAsync(string url, Action<string> callback = null) {
			try {
				HttpResponseMessage response = await http.GetAsync(url);

				return await Finish(response, callback);
			} catch (Exception e) {
				onError?.Invoke($"Exception during GET", e);
				return null;
			}
		}
		/// <summary> Attempt to GET the given URL and handle the response as a <see cref="byte[]"/></summary>
		/// <param name="url"> URL to GET from </param>
		/// <param name="callback"> Callback to fire on success </param>
		/// <returns> Task that can be awaited for the completion. </returns>
		public static Task<byte[]> GetRaw(string url, Action<byte[]> callback = null) {
			return Task.Run(() => GetRawAsync(url, callback));
		}
		/// <summary> Attempt to GET the given URL and handle the response as a <see cref="byte[]"/></summary>
		/// <param name="url"> URL to GET from </param>
		/// <param name="callback"> Callback to fire on success </param>
		/// <returns> Awaitable <see cref="Task"/> </returns>
		public static async Task<byte[]> GetRawAsync(string url, Action<byte[]> callback = null) {
			try {
				HttpResponseMessage response = await http.GetAsync(url);

				return await Finish(response, callback);
			} catch (Exception e) {
				onError?.Invoke($"Exception during GET raw", e);
				return null;
			}
		}

		/// <summary> Attempt to POST to the given URL and handle the response as a <see cref="string"/> </summary>
		/// <param name="url"> URL to POST to </param>
		/// <param name="content"> string to POST </param>
		/// <param name="callback"> Callback to fire on success </param>
		/// <param name="prep"> Custom generator of C#'s retarded <see cref="HttpContent"/>, if you want a different encoding or "ContentType"</param>
		/// <returns> Awaitable Task </returns>
		/// <remarks> Invokes <see cref="onError"/> during any failures. </remarks>
		public static Task<string> Post(string url, string content, Action<string> callback = null, Func<string, HttpContent> prep = null) {
			return Task.Run(() => PostAsync(url, content, callback, prep));
		}

		/// <summary> Attempt to POST to the given URL and handle the response as a <see cref="string"/> </summary>
		/// <param name="url"> URL to POST to </param>
		/// <param name="content"> string to POST </param>
		/// <param name="callback"> Callback to fire on success </param>
		/// <param name="prep"> Custom generator of C#'s retarded <see cref="HttpContent"/>, if you want a different encoding or "ContentType", or any custom headers. </param>
		/// <returns> Awaitable Task </returns>
		/// <remarks> Invokes <see cref="onError"/> during any failures. </remarks>
		public static async Task<string> PostAsync(string url, string content, Action<string> callback = null, Func<string, HttpContent> prep = null) {
			try {
				HttpContent request;

				if (prep == null) {
					request = new StringContent(content, Encoding.UTF8, "application/json");
				} else {
					request = prep(content);
				}

				HttpResponseMessage response = await http.PostAsync(url, request);
				return await Finish(response, callback);
			} catch (Exception e) {
				onError?.Invoke($"Exception during POST", e);
				return null;
			}
		}


		private static async Task<string> Finish(HttpResponseMessage response, Action<string> callback = null) {
			if (response.IsSuccessStatusCode) {
				string result = await response.Content.ReadAsStringAsync();
				callback?.Invoke(result);
				return result;
			} else {
				onError?.Invoke($"Bad status code from {response.RequestMessage.Method}", null);
				return null;
			}
		}

		private static async Task<byte[]> Finish(HttpResponseMessage response, Action<byte[]> callback = null) {
			if (response.IsSuccessStatusCode) {
				byte[] result = await response.Content.ReadAsByteArrayAsync();
				callback?.Invoke(result);
				return result;
			} else {
				onError?.Invoke($"Bad status code from {response.RequestMessage.Method}", null);
				return null;
			}
		}
	}
}
