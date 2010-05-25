using System;
using System.IO;
using SQLite;

namespace TweetStation
{
	public class Database : SQLiteConnection {
		internal Database (string file) : base (file)
		{
			Util.ReportTime ("Database init");
			CreateTable<TwitterAccount> ();
			CreateTable<TwitterAccount.QueuedTask> ();
			CreateTable<Tweet> ();
			CreateTable<User> ();
			Util.ReportTime ("Database finish");
		}
		
		static Database ()
		{
			// For debugging
			//System.IO.File.Delete ("tweets.db");
			Main = new Database (Util.BaseDir + "/Documents/tweets.db");
		}
		
		static public Database Main { get; private set; }
	}
}

