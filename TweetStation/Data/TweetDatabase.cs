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
using System.Json;
using SQLite;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TweetStation
{
	public partial class Tweet 
	{
		[PrimaryKey]
		public long Id { get; set; }

		[Indexed]
		public long CreatedAt { get; set; }
		

		/// <summary>
		///    Saves the tweet into the database
		/// </summary>
		void Insert (Database db)
		{
			db.Insert (this, "OR IGNORE");
		}
		
		public void Replace (Database db)
		{
			db.Insert (this, "OR REPLACE");
		}
		
		static bool ParseUser (JsonObject juser, User user, HashSet<long> usersSeen)
		{
			try {
				user.UpdateFromJson ((JsonObject) juser);
				if (!usersSeen.Contains (user.Id)){
					usersSeen.Add (user.Id);
					Database.Main.Insert (user, "OR REPLACE");
				}
			} catch {
				return false;
			}
			return true;
		}
		
		/// <summary>
		///   Loads the tweets encoded in the JSon response from the server
		///   into the database.   Users that are detected in the stream
		///   are entered in the user database as well.
		/// </summary>
		static public int LoadJson (Stream stream, int localAccount, TweetKind kind)
		{
			Database db = Database.Main;
			int count = 0;
			JsonValue root;
			string userKey;
			
			try {
				root = JsonValue.Load (stream);
				if (kind == TweetKind.Direct)
					userKey = "sender";
				else 
					userKey = "user";
			} catch (Exception e) {
				Console.WriteLine (e);
				return -1;
			}
			
			// These are reusable instances that we used during population
			var tweet = new Tweet () { Kind = kind, LocalAccountId = localAccount };
			var user = new User ();
			
			var start = DateTime.UtcNow;
			
			var usersSeen = new HashSet<long> ();
			
			lock (db){
				db.Execute ("BEGIN");
				foreach (JsonObject jentry in root){
					var juser = jentry [userKey];
					bool result;
					
					if (!ParseUser ((JsonObject) juser, user, usersSeen))
						continue;
					
					if (kind == TweetKind.Direct)
						result = tweet.TryPopulateDirect (jentry);
					else
						result = tweet.TryPopulate (jentry);
					
					if (result){
						PopulateUser (tweet, user);
						tweet.Insert (db);
						count++;
					}	
					
					// Repeat user loading for the retweet info
					if (tweet.Retweeter != null)
						ParseUser ((JsonObject)(jentry ["retweeted_status"]["user"]), user, usersSeen);
				}
				db.Execute ("COMMIT");
			}
			var end = DateTime.UtcNow;
			Util.Log ("With transactions: Spent {0} ticks in inserting {1} elements", (end-start).Ticks, count);
			return count;
		}
		
		static Tweet ParseTweet (Stream stream)
		{
			JsonObject jentry;
			
			try {
				jentry = (JsonObject) JsonValue.Load (stream);
			} catch (Exception e) {
				Console.WriteLine (e);
				return null;
			}
			try {
				var user = new User ();
				user.UpdateFromJson ((JsonObject) jentry ["user"]);
				lock (Database.Main)
					Database.Main.Insert (user, "OR REPLACE");
				
				return FromJsonEntry (jentry, user);
			} catch (Exception e){
				Console.WriteLine (e);
				return null;
			}
		}
		
		static Tweet FromJsonEntry (JsonObject jentry, User user)
		{
			var tweet = new Tweet () { Kind = TweetKind.Transient };
	
			if (!tweet.TryPopulate (jentry))
				return null;
			
			PopulateUser (tweet, user);
			if (tweet.Retweeter != null){
				user = new User ();
				user.UpdateFromJson ((JsonObject)(jentry ["retweeted_status"]["user"]));
				lock (Database.Main)
					Database.Main.Insert (user, "OR REPLACE");
			}
			return tweet;
		}
			
		//
		// Creates a tweet from a given ID
		//
		public static Tweet FromId (long id)
		{
			lock (Database.Main)
				return Database.Main.Query<Tweet> ("SELECT * FROM Tweet WHERE Id = ?", id).FirstOrDefault ();
		}
	}
	
	public partial class User {
		[PrimaryKey]
		public long Id { get; set; }
		
		[Indexed]
		public string Screenname { get; set; }
		
		// 
		// Loads the users from the stream, as a convenience, 
		// returns the last user loaded (which during lookups is a single one)
		//
		// Requires datbase lock to be taken.
		static public IEnumerable<User> UnlockedLoadUsers (Stream source)
		{
			JsonValue root;
			
			try {
				root = (JsonValue) JsonValue.Load (source);
			} catch (Exception e) {
				Console.WriteLine (e);
				yield break;
			}
			
			foreach (JsonObject juser in root){
				User user = new User ();
				user.UpdateFromJson (juser);
				Database.Main.Insert (user, "OR REPLACE");
				yield return user;
			}
		}
		
		// 
		// Loads a single user from the stream
		//
		static public User LoadUser (Stream source)
		{
			JsonValue root;
			
			try {
				root = JsonValue.Load (source);
			} catch (Exception e){
				Console.WriteLine (e);
				return null;
			}
			User user = new User ();
			user.UpdateFromJson ((JsonObject) root);
			lock (Database.Main)
				Database.Main.Insert (user, "OR REPLACE");
			return user;
		}
		
		// 
		// Loads a user from the database
		//
		public static User FromId (long id)
		{
			lock (Database.Main)
				return Database.Main.Query<User> ("SELECT * FROM User WHERE Id = ?", id).FirstOrDefault ();
		}
		
		public static User FromTweet (Tweet tweet)
		{
			if (tweet.UserId >= ImageStore.TempStartId){
				var u = new User ();
				u.Screenname = tweet.Screename;
				u.Id = tweet.UserId;
				u.PicUrl = tweet.PicUrl;
				return u;
			}
			return FromId (tweet.UserId);
		}
		
		const string lookup = "http://api.twitter.com/1/users/lookup.json";
		public static void FetchUser (long id, Action<User> cback)
		{
			FetchUserFromUrl (lookup + "?user_id=" + id, cback);
		}
		
		public static void FetchUser (string screenName, Action<User> cback)
		{
			FetchUserFromUrl (lookup + "?screen_name=" + screenName, cback);
		}
		
		static void FetchUserFromUrl (string url, Action<User> cback)
		{
			TwitterAccount.CurrentAccount.Download (url, res => { 
				if (res == null){
					cback (null);
					return;
				}
				User user;
				lock (Database.Main){
					user = User.UnlockedLoadUsers (res).FirstOrDefault ();
				}
				cback (user);
			});
		}
		
		public static User FromName (string screenname)
		{
			lock (Database.Main)
				return Database.Main.Query<User> ("SELECT * From User WHERE Screenname = ?", screenname).FirstOrDefault ();
		}	}
}

