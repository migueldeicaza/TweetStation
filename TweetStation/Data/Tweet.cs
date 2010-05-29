using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Json;
using System.Linq;
using SQLite;
using System.Text;
using System.Web;

namespace TweetStation
{
	/// <summary>
	///   Represents a tweet in memory.   Not all the data from the original tweet
	///   is kept around, most of the data is discarded.
	/// </summary>
	public class Tweet {
		
		[PrimaryKey]
		public long Id { get; set; }
		public int LocalAccountId { get; set; }
		public TweetKind Kind { get; set; }
		
		[Indexed]
		public long CreatedAt { get; set; }
		public string Text { get; set; }
		public string Source { get; set; }
		public bool Favorited { get; set; }
		public long InReplyToStatus { get; set; }
		public long InReplyToUser { get; set; }
		public string InReplyToUserName { get; set; }
		
		// Retweet information
		public string Retweeter { get; set; }
		public string RetweeterPicUrl { get; set; }
		public long RetweeterId { get; set; }
		
		// These are here just for convenience, to avoid doing an extra lookup on the User DB
		public long UserId { get; set; }
		public string Screename { get; set; }
		public string PicUrl { get; set; }

		// Negative values for the UserId indicate that this Tweet
		// is the result of search and is not complete
		public bool Complete {
			get {
				return UserId > 0;
			}
		}
		// 
		// The "source" might be surrounted by an <a href="..."></a> anchor
		// this strips it
		//
		
		static long GetLong (JsonObject json, string key)
		{
			if (json [key].JsonType == JsonType.Number)
				return (long) json [key];
			else
				return 0;
		}
		
		static long ParseCreation (JsonObject json)
		{
			return DateTime.ParseExact (json ["created_at"], "ddd MMM dd HH:mm:ss zzz yyyy", CultureInfo.InvariantCulture).ToUniversalTime ().Ticks;			
		}
		
		// Yes, they even use different formats for the dates they return
		static long ParseCreationSearch (JsonObject json)
		{
			return DateTime.ParseExact (json ["created_at"], "ddd, dd MMM yyyy HH:mm:ss zzz", CultureInfo.InvariantCulture).ToUniversalTime ().Ticks;
		}
		
		static string ParseText (JsonObject json)
		{
			var text = (string)json ["text"] ?? "";
			
			return HttpUtility.HtmlDecode (text).Replace ("\n", " ").Replace ("\r", " ");
		}
		
		bool TryPopulate (JsonObject json)
		{
			try {
				Id = json ["id"];
				CreatedAt = ParseCreation (json);
				Text = ParseText (json);
				Source = Util.StripHtml (HttpUtility.HtmlDecode (json ["source"] ?? ""));
				Favorited = json ["favorited"];
				InReplyToStatus = GetLong (json, "in_reply_to_status_id");
				InReplyToUser = GetLong (json, "in_reply_to_user_id");
				InReplyToUserName = (string) json ["in_reply_to_screen_name"];
				
				if (json.ContainsKey ("retweeted_status")){
					var sub = json ["retweeted_status"];
					var subuser = sub ["user"];
					
					// These are swapped out later.
					Retweeter = subuser ["screen_name"];
					RetweeterPicUrl = subuser ["profile_image_url"];
					RetweeterId = subuser ["id"];
					if (Text.StartsWith ("RT "))
						Text = Text.Substring (3);
				} else {
					RetweeterPicUrl = null;
					Retweeter = null;
					RetweeterId = 0;
				}
				return true;
			} catch (Exception e) {
				Console.WriteLine (e);
				return false;
			}
		}

		bool TryPopulateDirect (JsonObject json)
		{
			try {
				Id = json ["id"];
				CreatedAt = ParseCreation (json);
				Text = ParseText (json);
				return true;
			} catch (Exception e) {
				Console.WriteLine (e);
				return false;
			}
		}

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
			Database.Main.Execute ("BEGIN");
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
			Database.Main.Execute ("COMMIT");
			var end = DateTime.UtcNow;
			Console.WriteLine ("With transactions: Spent {0} ticks in inserting {1} elements", (end-start).Ticks, count);
			return count;
		}

		// 
		// Alternative version that just parses the users and tweets and returns them as lists
		// I thought it would be useful, but it is not.   The JSon parsing is too fast, we
		// only get bogged down with the actual sqlite insert
		//
		public static void ParseJson (Stream stream, int localAccount, TweetKind kind, out List<User> users, out List<Tweet> tweets)
		{
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
				tweets = null;
				users = null;
				return;
			}

			users = new List<User> (root.Count/4);
			tweets = new List<Tweet> (root.Count);
			
			var start = DateTime.UtcNow;
			
			var usersSeen = new HashSet<long> ();
			var user = new User ();
			foreach (JsonObject jentry in root){
				var juser = jentry [userKey];
				bool result;
				
				try {
					user.UpdateFromJson ((JsonObject) juser);
					if (!usersSeen.Contains (user.Id)){
						usersSeen.Add (user.Id);
						users.Add (user);
						user = new User ();
					}
				} catch {
					continue;
				}
				
				var tweet = new Tweet ();
				if (kind == TweetKind.Direct)
					result = tweet.TryPopulateDirect (jentry);
				else
					result = tweet.TryPopulate (jentry);
				
				if (result){
					PopulateUser (tweet, user);
					tweets.Add (tweet);
				}	
				
				// Repeat user loading for the retweet info
				if (tweet.Retweeter != null){
					user.UpdateFromJson ((JsonObject)(jentry ["retweeted_status"]["user"]));
					if (!usersSeen.Contains (user.Id)){
						usersSeen.Add (user.Id);
						users.Add (user);
						user = new User ();
					}
				}
			}
			var end = DateTime.UtcNow;		
			Console.WriteLine ("Parsing time for tweet stream: {0} for {1} tweets", end-start, tweets.Count);
		}

		/// <summary>
		/// Creates an IEnumerable of the tweets, does not store in the database.
		/// </summary>
		/// <remarks>
		/// If the referenceUser is null, the users are parsed from the stream
		/// and stored on the database, if not, the tweet information is copied
		/// from the reference user.
		/// </remarks>
		public static IEnumerable<Tweet> TweetsFromStream (Stream stream, User referenceUser)
		{
			JsonValue root;
			
			try {
				root = JsonValue.Load (stream);
			} catch (Exception e) {
				Console.WriteLine (e);
				yield break;
			}

			var usersSeen = referenceUser == null ? new HashSet<long> () : null;						
			var user = referenceUser == null ? new User () : referenceUser;
			
			foreach (JsonObject jentry in root){
				if (referenceUser == null){
					var juser = jentry ["user"];
					ParseUser ((JsonObject) juser, user, usersSeen);
				} 
					
				var tweet = FromJsonEntry (jentry, user);
				if (referenceUser == null && tweet.Retweeter != null){
					ParseUser ((JsonObject)(jentry ["retweeted_status"]["user"]), user, usersSeen);
				}
				if (tweet != null)
					yield return tweet;
			}
		}
		
		// We pick a user ID large enough that it wont clash with actual users
		static long serial = ImageStore.TempStartId;
		
		public bool IsSearchResult {
			get {
				return (UserId >= ImageStore.TempStartId);
			}
		}
		
		// Returns an IEnumerable of tweets when parsing search
		// results from twitter.   The returned Tweet objects are
		// not really complete and have the UserId busted (negative
		// numbers) since the userids returned by twitter for
		// searches have no relationship with the rest of the system
		public static IEnumerable<Tweet> TweetsFromSearchResults (Stream stream)
		{
			JsonValue root;
			
			try {
				root = JsonValue.Load (stream);
			} catch (Exception e) {
				Console.WriteLine (e);
				yield break;
			}
				
			foreach (JsonObject result in root ["results"]){
				Tweet tweet;
				
				try {
					tweet = new Tweet () {
						CreatedAt = ParseCreationSearch (result),
						Id = (long) result ["id"],
						Text = ParseText (result),
						Source = Util.StripHtml (HttpUtility.HtmlDecode (result ["source"]) ?? ""),
						UserId = serial++,
						Screename = (string) result ["from_user"],
						PicUrl = (string) result ["profile_image_url"]
					};
				} catch (Exception e){
					Console.WriteLine (e);
					tweet = null;
				}
				if (tweet != null)
					yield return tweet;
			}
		}
		
		public static Tweet ParseTweet (Stream stream)
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
				Database.Main.Insert (user, "OR REPLACE");
			}
			return tweet;
		}
	
		// Populates a Tweet object with cached and useful user information
		static void PopulateUser (Tweet tweet, User user)
		{
			if (tweet.Retweeter != null){
				tweet.UserId = tweet.RetweeterId;
				tweet.Screename = tweet.Retweeter;
				tweet.PicUrl = tweet.RetweeterPicUrl;
				
				tweet.RetweeterId = user.Id;
				tweet.Retweeter = user.Screenname;
				tweet.RetweeterPicUrl = user.PicUrl;
				if (tweet.RetweeterPicUrl == null)
					Console.WriteLine ("TWEFF");
			} else {
				tweet.UserId = user.Id;
				tweet.Screename = user.Screenname;
				tweet.PicUrl = user.PicUrl;
			}
		}
	

		//
		// Creates a tweet from a given ID
		//
		public static Tweet FromId (long id)
		{
			return Database.Main.Query<Tweet> ("SELECT * FROM Tweet WHERE Id = ?", id).FirstOrDefault ();
		}
		
		public delegate void LoadCallback (Tweet tweet);
		
		public static void LoadFullTweet (long id, LoadCallback callback)
		{
			TwitterAccount.CurrentAccount.Download ("http://api.twitter.com/1/statuses/show.json?id="+id, result => {
				if (result == null)
					callback (null);
				
				var tweet = Tweet.ParseTweet (new MemoryStream (result));
				if (tweet == null)
					callback (null);

				callback (tweet);
			});
		}
		
		// Gets the recipients from this tweet
		public string GetRecipients ()
		{
			string text = Text;
			if (text.IndexOf ('@') == -1)
				return '@' + Screename;
			
			var recipients = new List<string> ();

			recipients.Add (Screename);
			for (int i = 0; i < text.Length; i++){
				if (text [i] != '@')
					continue;
				
				var res = new StringBuilder ();
				for (i++; i < text.Length && Char.IsLetterOrDigit (text [i]); i++){
					res.Append (text [i]);
				}
				recipients.Add (res.ToString ());
			}
			return "@" + String.Join (" @", recipients.ToArray ()) + " ";
		}
		
		
		public override string ToString ()
		{
			return String.Format ("{0} - {1}", Id, Text);
		}
	}
	
	// 
	// Common fields are stored in the database, the rest is stored as the
	// json string representation.  Load the uncommon data on demand
	//
	public class User {
		[PrimaryKey]
		public long Id { get; set; }
		
		[Indexed]
		public string Screenname { get; set; }
		public string PicUrl { get; set; }		
		public string JsonString { get; set; }

		JsonValue _json;
		JsonValue Json { 
			get {
				if (_json == null)
						_json = JsonValue.Parse (JsonString);
				return _json;
			}
		}
		
		// 
		// Not stored in the database, but parsed from the Json 
		// as it is not very common
		// 
		public string Name { 
			get {
				return (string) Json ["name"];
			}
		}
		
		public long FollowersCount {
			get {
				return (long) Json ["followers_count"];
			}
		}
		
		public long FriendsCount {
			get {
				return (long) Json ["friends_count"];
			}
		}
			
		public long StatusesCount {
			get {
				return (long) Json ["statuses_count"];
			}
		}
		
		public long FavCount {
			get {
				return (long) Json ["favourites_count"];
			}
		}
		
		public string Location {
			get {
				return (string) Json ["location"];
			}
		}
		
		public string Url {
			get {
				return (string) Json ["url"];
			}
		}
		
		public string Description {
			get {
				return (string) Json ["description"];
			}
		}
		
		public void UpdateFromJson (JsonObject json)
		{
			try {
				Id = json ["id"];
				Screenname = json ["screen_name"];
				PicUrl = json ["profile_image_url"];
				JsonString = json.ToString ();
			} catch (Exception e){
				Console.WriteLine (e);
			}
		}
		
		// 
		// Loads the users from the stream, as a convenience, 
		// returns the last user loaded (which during lookups is a single one)
		//
		static public IEnumerable<User> LoadUsers (Stream source)
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
			Database.Main.Insert (user, "OR REPLACE");
			return user;
		}
		
		// 
		// Loads a user from the database
		//
		public static User FromId (long id)
		{
			return Database.Main.Query<User> ("SELECT * FROM User WHERE Id = ?", id).FirstOrDefault ();
		}
		
		public static User FromName (string screenname)
		{
			return Database.Main.Query<User> ("SELECT * From User WHERE Screenname = ?", screenname).FirstOrDefault ();
		}
	}
}

