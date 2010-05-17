//
// Timeline.cs: classes for rendering timelines of tweets
//
using System;
using System.Linq;
using System.Collections.Generic;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;
using System.IO;
using MonoTouch.Foundation;

namespace TweetStation {
	
	public abstract class BaseTimelineViewController : DialogViewController
	{
		TwitterAccount account;
		protected TweetKind kind;
		
		public BaseTimelineViewController (bool pushing) : base (null, pushing)
		{			
			Style = UITableViewStyle.Plain;
			NavigationItem.RightBarButtonItem = new UIBarButtonItem (UIBarButtonSystemItem.Compose, delegate {
				if (kind == TweetKind.Direct){
					var sheet = new UIActionSheet ("");
					sheet.AddButton (Locale.GetText ("New Tweet"));
					sheet.AddButton (Locale.GetText ("Direct Message"));
					sheet.AddButton (Locale.GetText ("Cancel"));
					
					sheet.CancelButtonIndex = 2;
					sheet.Clicked += delegate(object sender, UIButtonEventArgs e) {
						if (e.ButtonIndex == 2)
							return;
						
						if (e.ButtonIndex == 0)
							Composer.Main.NewTweet (this);
						else
							Composer.Main.Direct (this, "");
					};
					sheet.ShowInView (Util.MainAppDelegate.MainView);
				} else {
					Composer.Main.NewTweet (this);
				}
			});

			RefreshRequested += delegate {
				ReloadTimeline ();
			};
		}

		public TwitterAccount Account {
			get {
				return account;
			}
			
			set {
				if (account == value)
					return;
				
				account = value;
				ReloadAccount ();
			}
		}
		
		// The title for the root element
		protected abstract string TimelineTitle { get; }
		
		// Must reload the contents, the account has changed and create the view contents
		protected abstract void ResetState ();
		
		// Reloads data from the server
		public abstract void ReloadTimeline ();
		
		void ReloadAccount ()
		{
			if (Root != null)
				Root.Dispose ();

			ResetState ();
		}
	}
	
	public class TimelineViewController : BaseTimelineViewController {
		Section mainSection;
		string timelineTitle;
		
		public TimelineViewController (string title, TweetKind kind, bool pushing) : base (pushing)
		{
			timelineTitle = title;
			this.kind = kind;
			EnableSearch = true;
		}
		
		protected override string TimelineTitle {
			get {
				return timelineTitle;
			}
		}
		
		//
		// Fetches the tweets from the database up to @limit values
		// and @lastId is the last known tweet that we had loaded in the 
		// view, if we find the value in the first @limit values, we know
		// that the timeline is continuous
		//
		bool continuous;
		IEnumerable<Element> FetchTweets (int limit, long lastId, int skip)
		{
			continuous = false;
			foreach (var tweet in Database.Main.Query<Tweet> (
				"SELECT * FROM Tweet WHERE LocalAccountId = ? AND Kind = ? ORDER BY CreatedAt DESC LIMIT ? OFFSET ?", 
				Account.LocalAccountId, kind, limit, skip)){
				if (tweet.Id == lastId)
					continuous = true;
				yield return (Element) new TweetElement (tweet);
			}
		}

		// Gets the ID for the tweet in the tableview at @pos
		long? GetTableTweetId (int pos)
		{
			var mainSection = Root [0];
			long lastId = 0;
			if (mainSection.Elements.Count > pos){
				return (mainSection.Elements [pos] as TweetElement).Tweet.Id;
			} else
				return null;
		}
		
		public override void ReloadTimeline ()
		{
			long? since = null; 
			var res = Database.Main.Query<Tweet> ("SELECT Id FROM Tweet WHERE LocalAccountId = ? AND Kind = ? ORDER BY Id DESC LIMIT 1", Account.LocalAccountId, kind).FirstOrDefault ();
			if (res != null){
				// This should return one overlapping value.
				since = res.Id - 1;
			}
			
			DownloadTweets (0, since, null);
		}
		
		void DownloadTweets (int insertPoint, long? since, long? max_id)
		{
			if (kind != TweetKind.Home)
				return;
			
			Account.ReloadTimeline (kind, since, max_id, count => {
				if (count == -1){
					mainSection.Insert (insertPoint, new StringElement (Locale.Format ("Net failure on {0}", DateTime.Now)));
					count = 1;
				} else {
					// If we find an overlapping value, the timeline is continous, otherwise, we offer to load more
					
					// If insertPoint == 0, this is a top load, otherwise it is a "Load more tweets" load, so we 
					// need to fetch insertPoint-1 to get the actual last tweet, instead of the message.
					long lastId = GetTableTweetId (insertPoint == 0 ? 0 : insertPoint-1) ?? 0;					
					
					continuous = false;
					int newTweets = mainSection.Insert (insertPoint, UITableViewRowAnimation.None, FetchTweets (count, lastId, insertPoint));
					NavigationController.TabBarItem.BadgeValue = count.ToString ();

					if (!continuous){
						Element more = null;
						more = new StringElement ("Load more tweets", delegate {
							DownloadTweets (insertPoint + count, null, GetTableTweetId (insertPoint + count-1)-1);
							mainSection.Remove (more);
						});
						mainSection.Insert (insertPoint+count, UITableViewRowAnimation.None, more);
					}
				}
				ReloadComplete ();
				
				// Only scroll to last unread if this was not an intermediate "Load more tweets"
				if (insertPoint == 0)
					TableView.ScrollToRow (NSIndexPath.FromRowSection (count-1, 0), UITableViewScrollPosition.Middle, false);
			});
		}
		
		protected override void ResetState ()
		{
			mainSection = new Section () {
				FetchTweets (200, 0, 0)
			};

			Root = new RootElement (timelineTitle) {
				UnevenRows = true
			};
			Root.Add (mainSection);
			SearchPlaceholder = Locale.Format ("Search {0}", TimelineTitle);
			if (Util.NeedsUpdate ("update" + kind, TimeSpan.FromSeconds (120)))
				TriggerRefresh ();
		}
	}
	
	//
	// This version fo the BaseTimelineViewController does not store anything
	// on the database, it loads the data directly from the network into memory
	//
	// Used for transient information display
	//
	public class StreamedTimelineViewController : BaseTimelineViewController {
		const int PadX = 4;
		ShortProfileView shortProfileView;
		string title;
		Uri url;
		User reference;
		bool loaded;
		
		public StreamedTimelineViewController (string title, Uri url, User reference) : base (true)
		{
			this.url = url;
			this.title = title;
			this.reference = reference;
			
			this.NavigationItem.Title = title;
			EnableSearch = true;
		}
		
		public StreamedTimelineViewController (string title, Uri url) : this (title, url, null)
		{
		}		
		
		protected override string TimelineTitle { get { return title; } }
		
		protected override void ResetState ()
		{
			if (reference != null){
				var profileRect = new RectangleF (PadX, 0, View.Bounds.Width-30-PadX*2, 100);
				shortProfileView = new ShortProfileView (profileRect, reference.Id, true);
				shortProfileView.PictureTapped += delegate { PictureViewer.Load (this, reference.Id); };
				shortProfileView.UrlTapped += delegate { WebViewController.OpenUrl (this, reference.Url); };
				shortProfileView.Tapped += delegate { ActivateController (new FullProfileView (reference.Id)); };
				TableView.TableHeaderView = shortProfileView;
			}
		}
		
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			if (loaded)
				return;
			SearchPlaceholder = "Search";
			loaded = true;
			Root = Util.MakeProgressRoot (title);
			TriggerRefresh ();
		}
		
		// Reloads data from the server
		public override void ReloadTimeline ()
		{
			Account.Download (url, result => {
				if (result == null){
					Root = new RootElement (title) {
						new Section () {
							new StringElement ("Unable to download the timeline")
						}
					};
					return;
				}
				ReloadComplete ();
				
				var tweetStream = Tweet.TweetsFromStream (new MemoryStream (result), reference);
				
				Root = new RootElement (title){
					new Section () {
						from tweet in tweetStream select (Element) new TweetElement (tweet)
					}
				};
			});
		}
	}
	
	public class TimelineElement : RootElement {
		User reference;
		string nestedCaption;
		string url;
		
		public TimelineElement (string nestedCaption, string caption, string url, User reference) : base (caption)
		{
			this.nestedCaption = nestedCaption;
			this.reference = reference;
			this.url = url;
		}
		
		protected override UIViewController MakeViewController ()
		{						
			return new StreamedTimelineViewController (nestedCaption, new Uri (url), reference) {
				Account = TwitterAccount.CurrentAccount
			};
		}
		
	}
}


