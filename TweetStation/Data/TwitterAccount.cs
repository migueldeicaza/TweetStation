using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using SQLite;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Text;
using System.Collections.Specialized;
using System.Json;

namespace TweetStation
{
	public enum TweetKind {
		Home,
		Replies,
		Direct,
		Transient,
	}
	
	public class TwitterAccount
	{
		// The OAuth configuration for TweetStation
		public static OAuthConfig OAuthConfig = new OAuthConfig () {
			ConsumerKey = "VSemGxR6ZNpo5IWY3dS8uQ",
			TwitPicKey = "e66f585ed2c8be83be12a8f2be9a5981",
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
		
		[PrimaryKey, AutoIncrement]
		public int LocalAccountId { get; set; }
		
		public long AccountId { get; set; }
		public long LastLoaded { get; set; }
		public string Username { get; set; }
		public string OAuthToken { get; set; }
		public string OAuthTokenSecret { get; set; }
		
		static NSString invoker = new NSString ("");
		
		static Dictionary<long, TwitterAccount> accounts = new Dictionary<long, TwitterAccount> ();
		
		public static TwitterAccount FromId (int id)
		{
			if (accounts.ContainsKey (id)){
				return accounts [id];
			}
			
			var account = Database.Main.Query<TwitterAccount> ("select * from TwitterAccount where LocalAccountId = ?", id).FirstOrDefault ();
			if (account != null)
				accounts [account.LocalAccountId] = account;
			
			return account;
		}
		
		public static TwitterAccount Create (OAuthAuthorizer oauth)
		{
			var account = new TwitterAccount () {
				Username = oauth.AccessScreenname,
				AccountId = oauth.AccessId,
				OAuthToken = oauth.AccessToken,
				OAuthTokenSecret = oauth.AccessTokenSecret
			};
			Database.Main.Insert (account);
			accounts [account.LocalAccountId] = account;
			
			return account;
		}
		
		public static TwitterAccount CurrentAccount { get; set; }
		
		public static TwitterAccount GetDefaultAccount ()
		{		
#if false
			if (File.Exists ("/Users/miguel/tpass")){
				using (var f = System.IO.File.OpenText ("/Users/miguel/tpass")){
					var ta = new TwitterAccount () { 
						Username = f.ReadLine (),
						Password = f.ReadLine ()
					};
					Database.Main.Insert (ta, "OR IGNORE");
					using (var f2 = File.OpenRead ("home_timeline.json")){
						Tweet.LoadJson (f2, ta.LocalAccountId, TweetKind.Home);
					}
					accounts [ta.LocalAccountId] = ta;
					CurrentAccount = ta;
					return ta;
				}
			}
#endif	
			var account = FromId (Util.Defaults.IntForKey (DEFAULT_ACCOUNT));
			if (account == null || string.IsNullOrEmpty (account.OAuthToken))
				return null;
			
			CurrentAccount = account;
			
			return account;
		}
		
		public static void SetDefault (TwitterAccount account)
		{
			Util.Defaults.SetInt (account.LocalAccountId, DEFAULT_ACCOUNT);
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
				
			Download (req, result => {
				if (result == null)
					done (-1);
				else {
					int count = -1;
					try {
						count = Tweet.LoadJson (new MemoryStream (result), LocalAccountId, kind);
					} catch (Exception e) { 
						Console.WriteLine (e);
					}
					done (count);
				}
			});
		}
		
		internal struct Request {
			public string Url;
			public Action<byte []> Callback;
			
			public Request (string url, Action<byte []> callback)
			{
				Url = url;
				Callback = callback;
			}
		}
		
		const int MaxPending = 200;
		static Queue<Request> queue = new Queue<Request> ();
		static int pending;
		
		/// <summary>
		///   Throttled data download from the specified url and invokes the callback with
		///   the resulting data on the main UIKit thread.
		/// </summary>
		public void Download (string url, Action<byte []> callback)
		{
			lock (queue){				
				pending++;
				if (pending++ < MaxPending)
					Launch (url, callback);
				else {
					queue.Enqueue (new Request (url, callback));
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
		
		void Launch (string url, Action<byte []> callback)
		{
			var client = GetClient ();
	
			client.DownloadDataCompleted += delegate(object sender, DownloadDataCompletedEventArgs e) {
				lock (queue)
					pending--;
				
				Util.PopNetworkActive ();
				
				invoker.BeginInvokeOnMainThread (delegate {
					try {
						if (e == null)
							callback (null);
						callback (e.Result);
					} catch  (Exception ex){
						Console.WriteLine (ex);
					}
				});
				
				lock (queue){
					if (queue.Count > 0){
						var request = queue.Dequeue ();
						Launch (request.Url, request.Callback);
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
			while ((n = source.Read (buffer, 0, buffer.Length)) != 0)
				dest.Write (buffer, 0, n);
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
			var url = new Uri ("http://yfrog.com/api/upload");
			var req = (HttpWebRequest) WebRequest.Create (url);
			req.Method = "POST";
			req.ContentType = "multipart/form-data; boundary=" + boundary;
			OAuthAuthorizer.AuthorizeTwitPic (OAuthConfig, req, OAuthToken, OAuthTokenSecret);

			Stream upload = GenerateYFrogFrom (boundary, source, Username);
			req.ContentLength = upload.Length;
			using (var rs = req.GetRequestStream ())
				Copy (upload, rs);

			ThreadPool.QueueUserWorkItem (delegate {
				string urlToPic = null;
				try {
					var response = (HttpWebResponse) req.GetResponse  ();
					
					var stream = response.GetResponseStream ();
					var jresponse = JsonValue.Load (stream);
					Console.WriteLine (jresponse.ToString ());
					stream.Close ();
				} catch (Exception e){
					Console.WriteLine (e);
				}
				invoker.BeginInvokeOnMainThread (delegate { completed (urlToPic); });
			});
		}
		
		// 
		// Posts the @contents to the @url.   The post is done in a queue
		// system that is flushed regularly, so it is safe to call Post to
		// fire and forget
		//
		public void Post (string url, string content)
		{
			var qtask = new QueuedTask () {
				AccountId = LocalAccountId, 
				Url = url, 
				PostData = content,
			};
			Database.Main.Insert (qtask);
			
			FlushTasks ();
		}
		
		void FlushTasks ()
		{
			var tasks = Database.Main.Query<QueuedTask> ("SELECT * FROM QueuedTask ORDER BY TaskId DESC").ToArray ();	
			ThreadPool.QueueUserWorkItem (delegate { PostTask (tasks); });
		}
		
		// 
		// TODO ITEMS:
		//   * Need to change this to use HttpWebRequest, since I need to erad
		//     the result back and create a tweet out of it, and insert in DB.
		//
		//   * Report error to the user?   Perhaps have a priority flag
		//     (posts show dialog, btu starring does not?
		//
		// Runs on a thread from the threadpool.
		void PostTask (QueuedTask [] tasks)
		{
			var client = GetClient ();
			try {
				Util.PushNetworkActive ();
				foreach (var task in tasks){
					Uri taskUri = new Uri (task.Url);
					OAuthAuthorizer.AuthorizeRequest (OAuthConfig, client, OAuthToken, OAuthTokenSecret, "POST", taskUri, task.PostData);
					client.UploadData (taskUri, "POST", Encoding.UTF8.GetBytes (task.PostData));
					invoker.BeginInvokeOnMainThread (delegate {
						try {
							Database.Main.Execute ("DELETE FROM QueuedTask WHERE TaskId = ?", task.TaskId);
						} catch (Exception e){
							Console.WriteLine (e);
						}
					});	
				}
			} catch (Exception e) {
				Console.WriteLine (e);
			} finally {
				Util.PopNetworkActive ();
			}
		}
		
		public class QueuedTask {
			[PrimaryKey, AutoIncrement]
			public int TaskId { get; set; }
			public long AccountId { get; set; }
			public string Url { get; set; }

			public string PostData { get; set; }
		}
	}
	
	public interface IAccountContainer {
		TwitterAccount Account { get; set; }
	}
}
