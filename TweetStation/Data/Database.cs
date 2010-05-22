using System;
using SQLite;

namespace TweetStation
{
	public class Database : SQLiteConnection {
		internal Database (string file) : base (file)
		{
			CreateTable<TwitterAccount> ();
			CreateTable<TwitterAccount.QueuedTask> ();
			CreateTable<Tweet> ();
			CreateTable<User> ();
		}
		
		static Database ()
		{
			// For debugging
			//System.IO.File.Delete ("tweets.db");
			Main = new Database ("tweets.db");
		}
		
		static public Database Main { get; private set; }
	}
}

