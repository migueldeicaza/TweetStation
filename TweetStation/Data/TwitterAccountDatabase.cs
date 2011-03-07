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
using System.Linq;
using System.Text;
using System.Threading;
using SQLite;
using MonoTouch.Foundation;
using System.IO;
using System.Net;

namespace TweetStation
{
	public partial class TwitterAccount
	{
		[PrimaryKey, AutoIncrement]
		public int LocalAccountId { get; set; }
		
		public static TwitterAccount FromId (int id)
		{
			if (accounts.ContainsKey (id)){
				return accounts [id];
			}
			
			lock (Database.Main){
				var account = Database.Main.Query<TwitterAccount> ("select * from TwitterAccount where LocalAccountId = ?", id).FirstOrDefault ();
				if (account != null)
					accounts [account.LocalAccountId] = account;
				
				return account;
			}
		}
		
		public static TwitterAccount Create (OAuthAuthorizer oauth)
		{
			var account = new TwitterAccount () {
				Username = oauth.AccessScreenname,
				AccountId = oauth.AccessId,
				OAuthToken = oauth.AccessToken,
				OAuthTokenSecret = oauth.AccessTokenSecret
			};
			lock (Database.Main)
				Database.Main.Insert (account);
			accounts [account.LocalAccountId] = account;
			
			return account;
		}

		public static void Remove (TwitterAccount account)
		{
			var id = account.LocalAccountId;
			bool pickNewDefault = id == Util.Defaults.IntForKey (DEFAULT_ACCOUNT);
			
			if (accounts.ContainsKey (id))
				accounts.Remove (id);
			
			lock (Database.Main){
				Database.Main.Execute ("DELETE FROM Tweet where LocalAccountId = ?", account.LocalAccountId);
				Database.Main.Delete<TwitterAccount> (account);
			
				if (pickNewDefault){
					var newDefault = Database.Main.Query<TwitterAccount> ("SELECT LocalAccountId FROM TwitterAccount WHERE OAuthToken != \"\"").FirstOrDefault ();
					if (newDefault != null)
						Util.Defaults.SetInt (newDefault.LocalAccountId, DEFAULT_ACCOUNT);
				}
			}
		}
		
		// 
		// Posts the @contents to the @url.   The post is done in a queue
		// system that is flushed regularly, so it is safe to call Post to
		// fire and forget
		//
		public void Post (string url, string content)
		{
			Post (url, "POST", content);
		}
		
		public void Post (string url, string verb, string content)
		{
			var qtask = new QueuedTask2 () {
				AccountId = LocalAccountId, 
				Url = url, 
				PostData = content,
				Verb = verb
			};
			lock (Database.Main)
				Database.Main.Insert (qtask);
			
			FlushTasks ();
		}
		
		void FlushTasks ()
		{
			lock (Database.Main){
				var tasks = Database.Main.Query<QueuedTask2> ("SELECT * FROM QueuedTask2 where AccountId = ? ORDER BY TaskId DESC", LocalAccountId).ToArray ();
				ThreadPool.QueueUserWorkItem (delegate { PostTask (tasks); });
			}
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
		void PostTask (QueuedTask2 [] tasks)
		{
			var client = GetClient ();
			try {
				Util.PushNetworkActive ();
				foreach (var task in tasks){
					Uri taskUri = new Uri (task.Url);
					client.Headers [HttpRequestHeader.Authorization] = OAuthAuthorizer.AuthorizeRequest (OAuthConfig, OAuthToken, OAuthTokenSecret, "POST", taskUri, task.PostData);
					try {
						var res = client.UploadData (taskUri, task.Verb, Encoding.UTF8.GetBytes (task.PostData));
						//var r = new StreamReader (new MemoryStream (res));
						//Console.WriteLine (r.ReadToEnd ());
					} catch (Exception e){
						// Can happen if we had already favorited this status
						Console.WriteLine ("Can happen if we already favorited: {0}", e);
					}
					
					lock (Database.Main)
						Database.Main.Execute ("DELETE FROM QueuedTask2 WHERE TaskId = ?", task.TaskId);
				}
			} catch (Exception e) {
				Console.WriteLine (e);
			} finally {
				Util.PopNetworkActive ();
			}
		}
		
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
		
		public void SetDefaultAccount ()
		{
			NSUserDefaults.StandardUserDefaults.SetInt (LocalAccountId, DEFAULT_ACCOUNT); 
		}
		
		public string lasterror;
		public void ReloadTimeline (TweetKind kind, long? since, long? max_id, Action<int> done)
		{				
			var req = MakeTimelineRequest (kind, since, max_id);
			Download (req, false, result => {
				int count = -1;
				
				if (result != null){
					try {
						count = Tweet.LoadJson (result, LocalAccountId, kind);
						lasterror = null;
					} catch (Exception e) { 
						Console.WriteLine (e);
						lasterror = e.ToString ();
					}
				}

				invoker.BeginInvokeOnMainThread (delegate { done (count); });
			});
		}
		
		public class QueuedTask2 {
			[PrimaryKey, AutoIncrement]
			public int TaskId { get; set; }
			public long AccountId { get; set; }
			public string Url { get; set; }

			public string PostData { get; set; }
			public string Verb { get; set; }
		}
	}
}

