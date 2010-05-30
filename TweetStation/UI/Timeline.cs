//
// Timeline.cs: classes for rendering timelines of tweets
//
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
//
using System;
using System.Linq;
using System.Collections.Generic;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;
using System.IO;
using MonoTouch.Foundation;
using System.Text;
using MonoTouch.ObjCRuntime;

namespace TweetStation {
	
	public abstract partial class BaseTimelineViewController : DialogViewController
	{
		UserSelector selector;
		TwitterAccount account;
		protected TweetKind kind;
		
		public BaseTimelineViewController (bool pushing) : base (null, pushing)
		{			
			Autorotate = true;
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
		static UIImage settingsImage = UIImage.FromFile ("Images/settings.png");
		
		public TimelineViewController (string title, TweetKind kind, bool pushing) : base (pushing)
		{
			timelineTitle = title;
			this.kind = kind;
			EnableSearch = true;
			
			NavigationItem.LeftBarButtonItem = new UIBarButtonItem (settingsImage, UIBarButtonItemStyle.Plain, delegate {
				PresentModalViewController (new UINavigationController (new Settings (this)), true);
			});
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
			
			DownloadTweets (0, since, null, null);
		}
		
		void DownloadTweets (int insertPoint, long? since, long? max_id, Element removeOnInsert)
		{
			//if (kind != TweetKind.Home)
			//	return;
			
			Account.ReloadTimeline (kind, since, max_id, count => {
				mainSection.Remove (removeOnInsert);
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
					int nParsed = mainSection.Insert (insertPoint, UITableViewRowAnimation.None, FetchTweets (count, lastId, insertPoint));
					NavigationController.TabBarItem.BadgeValue = (nParsed > 1) ? nParsed.ToString () : null;

					if (!continuous){
						LoadMoreElement more = null;
						more = new LoadMoreElement (Locale.GetText ("Load more tweets"), Locale.GetText ("Loading"), delegate {
							more.Animating = true;
							DownloadTweets (insertPoint + nParsed, null, GetTableTweetId (insertPoint + count-1)-1, more);
						});
						try {
							mainSection.Insert (insertPoint+nParsed, UITableViewRowAnimation.None, more);
						} catch {
							Console.WriteLine ("on {0} inserting at {1}+{2} section has {3}", kind, insertPoint, count, mainSection.Count);
						}
					}
					count = nParsed;
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
				FetchTweets (200, 0, 0)
			};

			Root = new RootElement (timelineTitle) {
				UnevenRows = true
			};
			Root.Add (mainSection);
			SearchPlaceholder = Locale.Format ("Search {0}", TimelineTitle);
			Util.ReportTime ("Before trigger");
			if (Util.NeedsUpdate ("update" + kind, TimeSpan.FromSeconds (120))){
				// throttle, just to let the UI startup
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
		protected string StreamedTitle;		
		ShortProfileView shortProfileView;
		protected string url;
		bool loaded;
		
		public StreamedViewController (string title, string url, User reference) : base (true)
		{
			this.url = url;
			
			this.StreamedTitle = title;
			this.ReferenceUser = reference;
			
			this.NavigationItem.Title = title;
			EnableSearch = true;
		}
		
		protected override string TimelineTitle { get { return StreamedTitle; } }
		
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
			Root = Util.MakeProgressRoot (StreamedTitle);
			TriggerRefresh ();
		}
		
		// Reloads data from the server
		public override void ReloadTimeline ()
		{
			Account.Download (url, result => {
				if (result == null){
					Root = new RootElement (StreamedTitle) {
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
		
		protected virtual void PopulateRootFrom (byte [] data) {}
	}
	
	// 
	// This version does pagination by having a bunch of parameters passed,
	// sadly, twitter is a mess when it comes to this
	// lists/statuses: since_id, page, per_page
	// statuses/user_timeline: since_id, count
	// favorites: page with hardcoded count to 20
	// searches: page, since_id and "rpp" is used as "count"
	//s
	public class StreamedTimelineViewController : StreamedViewController {
		LoadMoreElement loadMore;
		string countStr, sinceStr, pageStr;
		int expectedCount;
		long last_id;
		
		public StreamedTimelineViewController (string title, string url, string countStr, int count, string sinceStr, string pageStr, User reference) : base (title, url, reference)
		{
			this.countStr = countStr;
			this.sinceStr = sinceStr;
			this.pageStr = pageStr;
			this.expectedCount = count;
		}
		
		// Reloads data from the server
		public override void ReloadTimeline ()
		{
			Load (1, last_id);
		}
		
		protected virtual IEnumerable<Tweet> GetTweetStream (byte [] result)
		{
			return Tweet.TweetsFromStream (new MemoryStream (result), ReferenceUser);
		}
			
		void Load (int page, long since_id)
		{
			var fullUrl = BuildUrl (page, since_id);
			TwitterAccount.CurrentAccount.Download (fullUrl, res => {
				if (res == null){
					Root = Util.MakeError (TimelineTitle);
					return;
				}
				var tweetStream = GetTweetStream (res);
				
				Section section;
				if (since_id == 0){
					if (page == 1){
						// If we are the first batch of data being loaded, not load more, or refresh
						var root = new RootElement (StreamedTitle) { UnevenRows = true };
						section = new Section ();
						root.Add (section);
						Root = root;
					} else { 
						section = Root [0];
						section.Remove (loadMore);
					}
					
					int n = section.Add (from tweet in tweetStream select (Element) new TweetElement (tweet));
					
					if (n == expectedCount){
						loadMore = new LoadMoreElement (Locale.GetText ("Load more"), Locale.GetText ("Loading"), delegate {
							Load (page+1, 0);
						}, UIFont.BoldSystemFontOfSize (14), UIColor.Black);
					
						section.Add (loadMore);
					}
				} else {
					section = Root [0];
					section.Insert (0, UITableViewRowAnimation.None, from tweet in tweetStream select (Element) new TweetElement (tweet));
				}
				if (sinceStr != null && section.Count > 0)
					last_id = (section [0] as TweetElement).Tweet.Id;

				ReloadComplete ();
			});
		}
		
		string BuildUrl (int page, long since_id)
		{
			Uri uri = new Uri (url);
			string query = uri.Query;
			
			var newUri = new StringBuilder (url);
			char next = query == "" ? '?' : '&';
			
			if (pageStr != null) {
				newUri.Append (next);
				newUri.Append (pageStr + page);
				next = '&';
			}
			if (countStr != null){
				newUri.Append (next);
				newUri.Append (countStr + expectedCount);
				next = '&';
			}
			if (sinceStr != null && since_id != 0){
				newUri.Append (next);
				newUri.Append (sinceStr + since_id);
			}
			return newUri.ToString ();
		}
		
		public static StreamedTimelineViewController MakeFavorites (string url)
		{
			return new StreamedTimelineViewController (Locale.GetText ("Favorites"), url, null, 20, null, "page=", null);
		}
		
		public static StreamedTimelineViewController MakeUserTimeline (string url)
		{
			return new StreamedTimelineViewController (Locale.GetText ("User's timeline"), url, "count=", 50, "since_id=", null, null);
		}
	}

	// 
	// A MonoTouch.Dialog Element that can be inserted in dialogs
	//
	public class TimelineRootElement : RootElement {
		User reference;
		string nestedCaption;
		string url, countStr, pageStr, sinceStr;
		int count;
		
		public TimelineRootElement (string nestedCaption, string caption, string url, string countStr, int count, string sinceStr, string pageStr, User reference) : base (caption)
		{
			this.nestedCaption = nestedCaption;
			this.reference = reference;
			this.url = url;
			this.pageStr = pageStr;
			this.countStr = countStr;
			this.sinceStr = sinceStr;
			this.count = count;
		}
		
		public static TimelineRootElement MakeTimeline (string nestedCaption, string caption, string url, User reference)
		{
			return new TimelineRootElement (nestedCaption, caption, url, "count=", 50, "since_id=", null, reference);
		}
		
		public static TimelineRootElement MakeFavorites (string nestedCaption, string caption, string url, User reference)
		{
			return new TimelineRootElement (nestedCaption, caption, url, null, 20, null, "page=", reference);
		}
		
		public static TimelineRootElement MakeList (string nestedCaption, string caption, string url)
		{
			return new TimelineRootElement (nestedCaption, caption, url, "per_page=", 20, "since_id", "page=", null);
		}
		
		protected override UIViewController MakeViewController ()
		{						
			return new StreamedTimelineViewController (nestedCaption, url, countStr, count, sinceStr, pageStr, reference) {
				Account = TwitterAccount.CurrentAccount
			};
		}
	}
}
