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
		UserSelector selector;
		TwitterAccount account;
		protected TweetKind kind;
		
		public BaseTimelineViewController (bool pushing) : base (null, pushing)
		{			
			RefreshRequested += delegate {
				ReloadTimeline ();
			};

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
						else {
							selector = new UserSelector (name => { 
								Composer.Main.Direct (this, name); 
								selector = null;
							});
							PresentModalViewController (selector, true);
						}
					};
					sheet.ShowInView (Util.MainAppDelegate.MainView);
				} else {
					Composer.Main.NewTweet (this);
				}
			});
			
			NavigationItem.LeftBarButtonItem = new UIBarButtonItem (UIBarButtonSystemItem.Action, delegate {
				PresentModalViewController (new UINavigationController (new Settings (this)), true);
			});
		}

		public TwitterAccount Account {
			get {
				return account;
			}
			
			set {
				if (account == value)
					return;
				
				ReloadComplete ();
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
				if (tweet.Id == lastId){
					continuous = true;
					yield break;
				}
				yield return (Element) new TweetElement (tweet);
			}
		}

		// Gets the ID for the tweet in the tableview at @pos
		long? GetTableTweetId (int pos)
		{
			var mainSection = Root [0];
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
			//if (kind != TweetKind.Home)
			//	return;
			
			Account.ReloadTimeline (kind, since, max_id, count => {
				if (count == -1){
					mainSection.Insert (insertPoint, new StyledStringElement (Locale.Format ("Net failure on {0}", DateTime.Now)){
						Font = UIFont.SystemFontOfSize (14)
					});
					count = 1;
				} else {
					// If we find an overlapping value, the timeline is continous, otherwise, we offer to load more
					
					// If insertPoint == 0, this is a top load, otherwise it is a "Load more tweets" load, so we 
					// need to fetch insertPoint-1 to get the actual last tweet, instead of the message.
					long lastId = GetTableTweetId (insertPoint == 0 ? 0 : insertPoint-1) ?? 0;					
					
					continuous = false;
					int inserted = mainSection.Insert (insertPoint, UITableViewRowAnimation.None, FetchTweets (count, lastId, insertPoint));
					NavigationController.TabBarItem.BadgeValue = (count > 1) ? (count-1).ToString () : null;

					if (!continuous){
						Element more = null;
						more = new StringElement (Locale.GetText ("Load more tweets"), delegate {
							DownloadTweets (insertPoint + count, null, GetTableTweetId (insertPoint + count-1)-1);
							mainSection.Remove (more);
						});
						mainSection.Insert (insertPoint+count, UITableViewRowAnimation.None, more);
					}
					
				}
				ReloadComplete ();
				
				// Only scroll to last unread if this was not an intermediate "Load more tweets"
				if (insertPoint == 0 && count > 0)
					TableView.ScrollToRow (NSIndexPath.FromRowSection (count-1, 0), UITableViewScrollPosition.Middle, false);
			});
		}
		
		protected override void ResetState ()
		{
			mainSection = new Section () {
				FetchTweets (80, 0, 0)
			};

			Root = new RootElement (timelineTitle) {
				UnevenRows = true
			};
			Root.Add (mainSection);
			SearchPlaceholder = Locale.Format ("Search {0}", TimelineTitle);
			Util.ReportTime ("Before trigger");
			if (Util.NeedsUpdate ("update" + kind, TimeSpan.FromSeconds (120))){
				// throttle
				NSTimer.CreateScheduledTimer (TimeSpan.FromMilliseconds (200), delegate {				
					TriggerRefresh ();
				});
			}
			Util.ReportTime ("After trigger");
		}
		
		// 
		// Override the default source so we can track when we reach the top
		// and in that case, clear the badge value
		//
		public override Source CreateSizingSource (bool unevenRows)
		{
			// we are always uneven for TimelineViewControllers
			return new ScrollTrackingSizingSource (this);
		}
		
		class ScrollTrackingSizingSource : DialogViewController.SizingSource {
			public ScrollTrackingSizingSource (DialogViewController dvc) : base (dvc)
			{
			}
			
			public override void Scrolled (UIScrollView scrollView)
			{
				var point = Container.TableView.ContentOffset;
				
				if (point.Y <= 10){
					(Container as TimelineViewController).NavigationController.TabBarItem.BadgeValue = null;
				}
				
				base.Scrolled (scrollView);
			}
			
		}
	}
	
	//
	// This version fo the BaseTimelineViewController does not store anything
	// on the database, it loads the data directly from the network into memory
	//
	// Used for transient information display, it is used by two derived classes
	// one shows tweets, the other shows users.
	//
	public abstract class StreamedViewController : BaseTimelineViewController {
		const int PadX = 4;
		protected User ReferenceUser;
		protected string Title;
		ShortProfileView shortProfileView;
		string url;
		bool loaded;
		
		public StreamedViewController (string title, string url, User reference) : base (true)
		{
			this.url = url;
			this.Title = title;
			this.ReferenceUser = reference;
			
			this.NavigationItem.Title = title;
			EnableSearch = true;
		}
		
		public StreamedViewController (string title, string url) : this (title, url, null)
		{
		}		
		
		protected override string TimelineTitle { get { return Title; } }
		
		protected override void ResetState ()
		{
			if (ReferenceUser != null){
				var profileRect = new RectangleF (PadX, 0, View.Bounds.Width-30-PadX*2, 100);
				shortProfileView = new ShortProfileView (profileRect, ReferenceUser.Id, true);
				shortProfileView.PictureTapped += delegate { PictureViewer.Load (this, ReferenceUser.Id); };
				shortProfileView.UrlTapped += delegate { WebViewController.OpenUrl (this, ReferenceUser.Url); };
				shortProfileView.Tapped += delegate { ActivateController (new FullProfileView (ReferenceUser.Id)); };
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
			Root = Util.MakeProgressRoot (Title);
			TriggerRefresh ();
		}
		
		// Reloads data from the server
		public override void ReloadTimeline ()
		{
			Account.Download (url, result => {
				if (result == null){
					Root = new RootElement (Title) {
						new Section () {
							new StringElement ("Unable to download the timeline")
						}
					};
					return;
				}
				ReloadComplete ();
				PopulateRootFrom (result);
			});
		}
		
		protected abstract void PopulateRootFrom (byte [] data);
	}
	
	public class StreamedTimelineViewController : StreamedViewController {
		public StreamedTimelineViewController (string title, string url, User reference) : base (title, url, reference)
		{
		}
		
		public StreamedTimelineViewController (string title, string url) : this (title, url, null)
		{
		}		

		protected override void PopulateRootFrom (byte [] result)
		{
			var tweetStream = Tweet.TweetsFromStream (new MemoryStream (result), ReferenceUser);
			
			Root = new RootElement (Title){
				new Section () {
					from tweet in tweetStream select (Element) new TweetElement (tweet)
				}
			};
		}
	}

	public class StreamedUserViewController : StreamedViewController {
		public StreamedUserViewController (string title, string url, User reference) : base (title, url, reference)
		{
		}

		protected override void PopulateRootFrom (byte [] result)
		{
			var userStream = User.LoadUsers (new MemoryStream (result));
			
			Root = new RootElement (Title){
				new Section () {
					from user in userStream select (Element) new UserElement (user)
				}
			};
		}
	}
	
	public class TimelineRootElement : RootElement {
		User reference;
		string nestedCaption;
		string url;
		
		public TimelineRootElement (string nestedCaption, string caption, string url, User reference) : base (caption)
		{
			this.nestedCaption = nestedCaption;
			this.reference = reference;
			this.url = url;
		}
		
		protected override UIViewController MakeViewController ()
		{						
			return new StreamedTimelineViewController (nestedCaption, url, reference) {
				Account = TwitterAccount.CurrentAccount
			};
		}
	}
	
	public class UserRootElement : RootElement {
		User reference;
		string url;
		
		public UserRootElement (User reference, string caption, string url) : base (caption)
		{
			this.reference = reference;
			this.url = url;
		}
		
		protected override UIViewController MakeViewController ()
		{
			return new StreamedUserViewController (reference.Screenname, url, reference) {
				Account = TwitterAccount.CurrentAccount
			};
		}
	}
}


