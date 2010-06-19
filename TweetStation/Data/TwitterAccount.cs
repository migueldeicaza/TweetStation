// Copyright 2010 Miguel de Icaza
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Text;
using System.Collections.Specialized;
using System.Json;
using System.Xml.Linq;

namespace TweetStation
{
	public enum TweetKind {
		Home,
		Replies,
		Direct,
		Transient,
	}
	
	public partial class TwitterAccount
	{
		// The OAuth configuration for TweetStation
		public static OAuthConfig OAuthConfig = new OAuthConfig () {
			ConsumerKey = "VSemGxR6ZNpo5IWY3dS8uQ",
			TwitPicKey = "e66f585ed2c8be83be12a8f2be9a5981",
			BitlyKey = "R_45898eef7a5772943c2ca54eea9877fd",
			Callback = "http://tirania.org/tweetstation/oauth",
			ConsumerSecret = "MEONRf8QqJDotJWioW1v1sSZVhXlOsTI85xu9eZfJf8",
			RequestTokenUrl = "https://api.twitter.com/oauth/request_token", 
			AccessTokenUrl = "https://twitter.com/oauth/access_token", 
			AuthorizeUrl = "https://twitter.com/oauth/authorize"
		};
		
		const string timelineUri = "http://api.twitter.com/1/statuses/home_timeline.json";
		const string mentionsUri = "http://api.twitter.com/1/statuses/mentions.json";
		const string directUri = "http://api.twitter.com/1/direct_messages.json";
			
		const string DEFAULT_ACCOUNT = "defaultAccount";
		
		public long AccountId { get; set; }
		public long LastLoaded { get; set; }
		public string Username { get; set; }
		public string OAuthToken { get; set; }
		public string OAuthTokenSecret { get; set; }
		
		static NSString invoker = new NSString ("");
		
		static Dictionary<long, TwitterAccount> accounts = new Dictionary<long, TwitterAccount> ();
		
		public static TwitterAccount CurrentAccount { get; set; }
		
		public static TwitterAccount GetDefaultAccount ()
		{		
			var account = FromId (Util.Defaults.IntForKey (DEFAULT_ACCOUNT));
			if (account == null || string.IsNullOrEmpty (account.OAuthToken))
				return null;
			
			CurrentAccount = account;
			
			return account;
		}
		
		public static void SetDefault (TwitterAccount account)
		{
			Util.Defaults.SetInt (account.LocalAccountId, DEFAULT_ACCOUNT);
			CurrentAccount = account;
		}

		public void ReloadTimeline (TweetKind kind, long? since, long? max_id, Action<int> done)
		{
			string uri = null;
			switch (kind){
			case TweetKind.Home:
				uri = timelineUri; break;
			case TweetKind.Replies:
				uri = mentionsUri; break;
			case TweetKind.Direct:
				uri = directUri; break;
			}
			var req = uri + "?count=200" + 
				(since.HasValue ? "&since_id=" + since.Value : "") +
				(max_id.HasValue ? "&max_id=" + max_id.Value : "");
				
			Download (req, false, result => {
				int count = -1;
				
				if (result != null){
					try {
						count = Tweet.LoadJson (new MemoryStream (result), LocalAccountId, kind);
					} catch (Exception e) { 
						Console.WriteLine (e);
					}
				}

				invoker.BeginInvokeOnMainThread (delegate { done (count); });
			});
		}
		
		internal struct Request {
			public string Url;
			public Action<byte []> Callback;
			public bool CallbackOnMainThread;
			
			public Request (string url, bool callbackOnMainThread, Action<byte []> callback)
			{
				Url = url;
				Callback = callback;
				CallbackOnMainThread = callbackOnMainThread;
			}
		}
		
		const int MaxPending = 200;
		static Queue<Request> queue = new Queue<Request> ();
		static int pending;
		
		/// <summary>
		///   Throttled data download from the specified url and invokes the callback with
		///   the resulting data on the main UIKit thread.
		/// </summary>
		/// 
		/// 
		public void Download (string url, Action<byte []> callback)		
		{
			Download (url, true, callback);
		}
		
		public void Download (string url, bool callbackOnMainThread, Action<byte []> callback)
		{
			lock (queue){				
				pending++;
				if (pending++ < MaxPending)
					Launch (url, callbackOnMainThread, callback);
				else {
					queue.Enqueue (new Request (url, callbackOnMainThread, callback));
					//Console.WriteLine ("Queued: {0}", url);
				}
			}
		}

		// This is required because by default WebClient wont authenticate
		// until challenged to.   Twitter does not do that, so we need to force
		// the pre-authentication
		class AuthenticatedWebClient : WebClient {
			protected override WebRequest GetWebRequest (Uri address)
			{
				var req = (HttpWebRequest) WebRequest.Create (address);
				req.PreAuthenticate = true;
				
				return req;
			}
		}
		
		WebClient GetClient ()
		{
			if (OAuthTokenSecret != null)
				return new WebClient (); 
			return null;
#if false
			// In the future, for connecting to non-OAuth twitter sites
			else 
				return new AuthenticatedWebClient () {
					Credentials = new NetworkCredentials (login, pass);
			}
#endif
		}
		
		public void AddOAuthHeader (string operation, string url, string data)
		{
		}

		static void InvokeCallback (Action<byte []> callback, DownloadDataCompletedEventArgs e)
		{
			try {
				if (e == null)
					callback (null);
				callback (e.Result);
			} catch  (Exception ex){
				Console.WriteLine (ex);
			}
		}
		
		void Launch (string url, bool callbackOnMainThread, Action<byte []> callback)
		{
			var client = GetClient ();
	
			client.DownloadDataCompleted += delegate(object sender, DownloadDataCompletedEventArgs e) {
				lock (queue)
					pending--;
				
				Util.PopNetworkActive ();
				
				if (callbackOnMainThread)
					invoker.BeginInvokeOnMainThread (delegate { InvokeCallback (callback, e);});
				else
					InvokeCallback (callback, e);
				
				lock (queue){
					if (queue.Count > 0){
						var request = queue.Dequeue ();
						Launch (request.Url, request.CallbackOnMainThread, request.Callback);
					}
				}
			};
			Util.PushNetworkActive ();
			Uri uri = new Uri (url);
			OAuthAuthorizer.AuthorizeRequest (OAuthConfig, client, OAuthToken, OAuthTokenSecret, "GET", uri, null);
			client.DownloadDataAsync (uri);
		}
		
		public void SetDefaultAccount ()
		{
			NSUserDefaults.StandardUserDefaults.SetInt (LocalAccountId, DEFAULT_ACCOUNT); 
		}

		static void Copy (Stream source, Stream dest)
		{
			var buffer = new byte [4096];
			int n = 0;

			source.Position = 0;
			while ((n = source.Read (buffer, 0, buffer.Length)) != 0){
				dest.Write (buffer, 0, n);
			}
		}

		static void AddPart (Stream target, string boundary, bool newline, string header, string value)
		{
			if (newline)
				target.Write (new byte [] { 13, 10 }, 0, 2);
			
			var enc = Encoding.UTF8.GetBytes (String.Format ("--{0}\r\n{1}\r\n\r\n", boundary, header));
			target.Write (enc, 0, enc.Length);
			if (value != null){
				enc = Encoding.UTF8.GetBytes (value);
				target.Write (enc, 0, enc.Length);
			}
		}
		
		//
		// Creates the YFrog form to upload the image
		//
		static Stream GenerateYFrogFrom (string boundary, Stream source, string username)
		{
			var dest = new MemoryStream ();
			AddPart (dest, boundary, false, "Content-Disposition: form-data; name=\"media\"; filename=\"none.png\"\r\nContent-Type: application/octet-stream", null);
			Copy (source, dest);
			AddPart (dest, boundary, true, "Content-Disposition: form-data; name=\"username\"", username);
			var bbytes = Encoding.ASCII.GetBytes (String.Format ("\r\n--{0}--", boundary));
			dest.Write (bbytes, 0, bbytes.Length);

			return dest;
		}
		
		public void UploadPicture (Stream source, Action<string> completed)
		{
			var boundary = "###" + Guid.NewGuid ().ToString () + "###";
						
			//var url = new Uri ("http://api.twitpic.com/2/upload.json");
			var url = new Uri ("http://yfrog.com/api/xauth_upload");
			var req = (HttpWebRequest) WebRequest.Create (url);
			req.Method = "POST";
			req.ContentType = "multipart/form-data; boundary=" + boundary;
			OAuthAuthorizer.AuthorizeTwitPic (OAuthConfig, req, OAuthToken, OAuthTokenSecret);

			Stream upload = GenerateYFrogFrom (boundary, source, Username);
			req.ContentLength = upload.Length;
			using (var rs = req.GetRequestStream ()){
				Copy (upload, rs);
				rs.Close ();
			}
			string urlToPic = null;
			try {
				var response = (HttpWebResponse) req.GetResponse  ();
				var stream = response.GetResponseStream ();
				var doc = XDocument.Load (stream);
				if (doc.Element ("rsp").Attribute ("stat").Value == "ok"){
					urlToPic = doc.Element ("rsp").Element ("mediaurl").Value;
				}
				stream.Close ();
			} catch (Exception e){
				Console.WriteLine (e);
			}
			invoker.BeginInvokeOnMainThread (delegate { completed (urlToPic); });
		}
	}
	
	public interface IAccountContainer {
		TwitterAccount Account { get; set; }
	}
}
