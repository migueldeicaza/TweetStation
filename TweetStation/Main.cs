using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Json;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;
using System.Threading;

namespace TweetStation
{
	public class Application
	{
		static void Main (string[] args)
		{
			try {
				UIApplication.Main (args);
			} catch (Exception e){
				Console.WriteLine ("Toplevel exception: {0}", e);
			}
		}
	}

	// The name AppDelegate is referenced in the MainWindow.xib file.
	public partial class AppDelegate : UIApplicationDelegate, IAccountContainer
	{
		public static AppDelegate MainAppDelegate;
		
		static bool useXauth;
		TwitterAccount account;
		TimelineViewController main, mentions, messages;
		SearchesViewController searches;
		StreamedTimelineViewController favorites;
		public UIView MainView;
		
		UINavigationController [] navigationRoots;
		
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			Util.ReportTime ("Entering Finished");
			
			MainAppDelegate = this;
			window.MakeKeyAndVisible ();

			//SpyTouch.SpyTouch.Run ();
			
			// Required by some HTTP calls to Twitter
			System.Net.ServicePointManager.Expect100Continue = false;

#if true
			try {
				if (File.Exists ("/Users/miguel/xauth")){
					using (var f = File.OpenText ("/Users/miguel/xauth")){
						var cfg = TwitterAccount.OAuthConfig;
						cfg.ConsumerKey = f.ReadLine ();
						cfg.ConsumerSecret = f.ReadLine ();
						cfg.Callback = f.ReadLine ();
						useXauth = true;
					}
				} 
			} catch {}				
#else
			var cfg = TwitterAccount.OAuthConfig;
			useXauth = true;

#endif
			
			Util.ReportTime ("Before GetDefaultAccount");
			var defaultAccount = TwitterAccount.GetDefaultAccount ();
			Util.ReportTime ("After GetDefaultAccount");
			if (defaultAccount == null){
				if (useXauth)
					NewAccountXAuth (null, null);
				else
					CreateDefaultAccountWithOAuth ();
			} else {
				Util.ReportTime ("Before UI Creation");
				CreatePhoneGui ();
				Util.ReportTime ("After UI Creation");
				
				Account = defaultAccount;
			}
			return true;
		}
		
		UITabBarController tabbarController;
		
		void CreatePhoneGui ()
		{
			tabbarController = new RotatingTabBar ();
			MainView = tabbarController.View;
			window.AddSubview (MainView);

			main = new TimelineViewController (Locale.GetText ("Friends"), TweetKind.Home, false);
			mentions = new TimelineViewController (Locale.GetText ("Mentions"), TweetKind.Replies, false); 
			messages = new TimelineViewController (Locale.GetText ("Messages"), TweetKind.Direct, false);
			searches = new SearchesViewController ();
			favorites = StreamedTimelineViewController.MakeFavorites ("http://api.twitter.com/1/favorites.json");
			
			navigationRoots = new UINavigationController [5] {
				new UINavigationController (main) {
					TabBarItem = new UITabBarItem (Locale.GetText ("Friends"), UIImage.FromBundle ("Images/home.png"), 0),
				},
				new UINavigationController (mentions) {
					TabBarItem = new UITabBarItem (Locale.GetText ("Mentions"), UIImage.FromBundle ("Images/replies.png"), 1)
				},
				new UINavigationController (messages) {
					TabBarItem = new UITabBarItem (Locale.GetText ("Messages"), UIImage.FromBundle ("Images/messages.png"), 2)
				},
				new UINavigationController (favorites) {
					TabBarItem = new UITabBarItem (Locale.GetText ("Favorites"), UIImage.FromBundle ("Images/fav.png"), 3)
				},
				new UINavigationController (searches) {
					TabBarItem = new UITabBarItem (Locale.GetText ("Search"), UIImage.FromBundle ("Images/lupa.png"), 4)
				}
			};
	
			CheckDatabase ();
			tabbarController.SetViewControllers (navigationRoots, false);
		}

		void CheckDatabase ()
		{
			int last = Util.Defaults.IntForKey ("LastUpdate");

			int today = new TimeSpan (DateTime.UtcNow.Ticks).Days;
			if (last == 0) 
				Util.Defaults.SetInt (today, "LastUpdate");
			else {
				if (today-last > 15){
					long cutoff = (DateTime.UtcNow-TimeSpan.FromDays (15)).Ticks;
					Database.Main.Execute ("DELETE from Tweet WHERE CreatedAt < " + cutoff);
					Util.Defaults.SetInt (today, "LastUpdate");
				}
			}
		}
		
		public TwitterAccount Account { 
			get {
				return account;
			}
			
			set {
				this.account = value;
				
				main.Account = account;
				searches.Account = account;
				mentions.Account = account;
				favorites.Account = account;
				messages.Account = account;
			}
		}
		
		public override void WillEnterForeground (UIApplication application)
		{
			main.ReloadIfAtTop ();
			mentions.ReloadIfAtTop ();
			messages.ReloadIfAtTop ();
		}
		
		//
		// Dispatcher that can open various assorted link-like text entries
		//
		public void Open (DialogViewController controller, string data)
		{
			if (data.Length == 0)
				return;
			if (data [0] == '@'){
				var profile = new FullProfileView (Util.CleanName (data.Substring (1)));
				controller.ActivateController (profile);
			} else if (data [0] == '#'){
				var search = new SearchViewController (data.Substring (1)) { Account = TwitterAccount.CurrentAccount };
				controller.ActivateController (search);
			} else 
				WebViewController.OpenUrl (controller, data);
		}
		
		// Replies to a tweet
		public void Reply (UIViewController controller, Tweet tweet)
		{
			if (tweet == null)
				throw new ArgumentNullException ("tweet");
			
			int p = tweet.Text.IndexOf ('@');
			if (p == -1){
				Composer.Main.ReplyTo (controller, tweet, false);
				return;
			}
			// If we have a '@' make sure it is not just @user
			// but someone else is included
			if (tweet.Text.Substring (p+1).StartsWith (TwitterAccount.CurrentAccount.Username)){
				p = tweet.Text.IndexOf ('@', p + 1 + TwitterAccount.CurrentAccount.Username.Length);
				if (p == -1){
					Composer.Main.ReplyTo (controller, tweet, false);
					return;
				}
			}
			var sheet = Util.GetSheet ("");
			sheet.AddButton (Locale.GetText ("Reply"));
			sheet.AddButton (Locale.GetText ("Reply All"));
			sheet.AddButton (Locale.GetText ("Cancel"));
			sheet.CancelButtonIndex = 2;
			sheet.Clicked += delegate(object s, UIButtonEventArgs e) {
				if (e.ButtonIndex == 0)
					Composer.Main.ReplyTo (controller, tweet, false);
				else if (e.ButtonIndex == 1){
					Composer.Main.ReplyTo (controller, tweet, true);
				}
			};
			sheet.ShowInView (MainAppDelegate.MainView);
		}
		
		public void Retweet (UIViewController controller, Tweet tweet)
		{
			var sheet = Util.GetSheet (Locale.GetText ("Retweet"));
			sheet.AddButton (Locale.GetText ("Retweet"));
			sheet.AddButton (Locale.GetText ("Quote Retweet"));
			sheet.AddButton (Locale.GetText ("Cancel"));
			sheet.CancelButtonIndex = 2;
			
			sheet.Clicked += delegate(object s, UIButtonEventArgs e) {
				if (!tweet.Favorited && Util.Defaults.IntForKey ("disableFavoriteRetweets") == 0)
					ToggleFavorite (tweet);
				
				if (e.ButtonIndex == 0)
					TwitterAccount.CurrentAccount.Post ("http://api.twitter.com/1/statuses/retweet/" + tweet.Id + ".json", ""); 
				else if (e.ButtonIndex == 1){
					Composer.Main.Quote (controller, tweet);
				}
			};
			sheet.ShowInView (AppDelegate.MainAppDelegate.MainView);
		}
		
		public void ToggleFavorite (Tweet tweet)
		{
			tweet.Favorited = !tweet.Favorited;
			FavoriteChanged (tweet);
			TwitterAccount.CurrentAccount.Post (String.Format ("http://api.twitter.com/1/favorites/{0}/{1}.json", tweet.Favorited ? "create" : "destroy", tweet.Id),"");
			
			lock (Database.Main)
				tweet.Replace (Database.Main);
		}
		
		// Broadcast the change, since tweets can be instantiated multiple times.
		void FavoriteChanged (Tweet tweet)
		{
			favorites.FavoriteChanged (tweet);
			main.FavoriteChanged (tweet);
			mentions.FavoriteChanged (tweet);
		}
		
		UINavigationController loginRoot = null;
		DialogViewController loginDialog = null;

		void MakeLoginDialog (DialogViewController parent, RootElement root)
		{
			loginDialog = new DialogViewController (UITableViewStyle.Grouped, root);
			
			if (parent == null){
				loginRoot = new UINavigationController (loginDialog);
				window.AddSubview (loginRoot.View);
			} else {
				loginDialog.NavigationItem.RightBarButtonItem = 
					new UIBarButtonItem (Locale.GetText ("Close"),
                                         UIBarButtonItemStyle.Plain, 
					                     delegate { loginDialog.DismissModalViewControllerAnimated (true); });
				parent.ActivateController (loginDialog);
			}
		}
		
		void StartupAfterAuthorization (OAuthAuthorizer oauth)
		{
			loginRoot.View.RemoveFromSuperview ();
			CreatePhoneGui ();
			SetDefaultAccount (oauth);
			loginDialog = null;
			loginRoot = null;
		}
			
		// Creates the login dialog using Xauth, this is a nicer
		// user experience, but requires Twitter to approve your 
		// app
		void NewAccountXAuth (DialogViewController parent, NSAction callback)
		{
			var login = new EntryElement (Locale.GetText ("Username"), Locale.GetText ("Your twitter username"), "");
			var password = new EntryElement (Locale.GetText ("Password"), Locale.GetText ("Your password"), "", true);
			var root = new RootElement (Locale.GetText ("Login")){
				new Section (){
					login,
					password
				},
				new Section (){
					new LoadMoreElement (Locale.GetText ("Login to Twitter"), Locale.GetText ("Contacting twitter"), delegate {
						login.FetchValue ();
						password.FetchValue ();
						StartXauthLogin (login.Value.Trim (), password.Value.Trim (), callback); 
					}, UIFont.BoldSystemFontOfSize (16), UIColor.Black)
				}
			};
			MakeLoginDialog (parent, root);
		}
		
		UIAlertView loginAlert;
		void StartXauthLogin (string user, string password, NSAction callback)
		{
			LoadMoreElement status = loginDialog.Root [1][0] as LoadMoreElement;
			
			// Spin off a thread to start the OAuth authentication process,
			// let the GUI thread show the spinner. 
			ThreadPool.QueueUserWorkItem (delegate {
				var oauth = new OAuthAuthorizer (TwitterAccount.OAuthConfig, user, password);
				
				if (oauth.AcquireAccessToken ()){
					BeginInvokeOnMainThread (delegate {
						if (callback == null)
							StartupAfterAuthorization (oauth);
						else {
							SetDefaultAccount (oauth);
							callback ();
						}
					});	
					return;
				}
				
				BeginInvokeOnMainThread (delegate { 
					status.Animating = false; 
					loginAlert = new UIAlertView (Locale.GetText ("Error"), 
					                             Locale.GetText ("Unable to login"), 
					                             null, null, Locale.GetText ("Ok"));
					loginAlert.Dismissed += delegate { loginAlert = null; };
					loginAlert.Show ();
				});
			});			
		}
		
		//
		// Creates the default account using OAuth
		//
		void CreateDefaultAccountWithOAuth ()
		{
			MakeLoginDialog (null, new RootElement (Locale.GetText ("Login to Twitter")){
				new Section (Locale.GetText ("Welcome to TweetStation!\n\n" +
				                             "To get started, authorize\n" +
				                             "TweetStation to get access\n" +  
				                             "to your twitter account.\n\n")){
					new StringElement ("Login to Twitter", delegate { StartLogin (loginDialog); })
				}
			});
		}
		
		void StartLogin (DialogViewController dvc)
		{
			dvc.Root.RemoveAt (1);
			LoadMoreElement status;
			
			if (dvc.Root.Count == 1){
				status = new LoadMoreElement (
				Locale.GetText ("Could not authenticate with twitter"), 
				Locale.GetText ("Contacting twitter"), null, UIFont.BoldSystemFontOfSize (16), UIColor.Black) {
					Animating = true
				};
				dvc.Root.Add (new Section () { status });
			} else
				status = (LoadMoreElement) dvc.Root [1][0];
			
			// Spin off a thread to start the OAuth authentication process,
			// let the GUI thread show the spinner. 
			ThreadPool.QueueUserWorkItem (delegate {
				var oauth = new OAuthAuthorizer (TwitterAccount.OAuthConfig);
	
				try {
					if (oauth.AcquireRequestToken ()){
						BeginInvokeOnMainThread (delegate {
							oauth.AuthorizeUser (dvc, delegate {
								StartupAfterAuthorization (oauth);
							});
						});
						return;
					} 
				} catch (Exception e){
					Console.WriteLine (e);
				}
				
				BeginInvokeOnMainThread (delegate { status.Animating = false; });
			});
		}

		public void AddAccount (DialogViewController dvc, NSAction action)
		{
			var oauth = new OAuthAuthorizer (TwitterAccount.OAuthConfig);

			if (useXauth)
				NewAccountXAuth (dvc, action);
			else {
				if (oauth.AcquireRequestToken ()){
					oauth.AuthorizeUser (dvc, delegate {
						SetDefaultAccount (oauth);
						action ();
					});
				}
			}
		}
		
		public void SetDefaultAccount (OAuthAuthorizer oauth)
		{
			var newAccount = TwitterAccount.Create (oauth);
			TwitterAccount.SetDefault (newAccount);
			Account = newAccount;
		}
		
		public class RotatingTabBar : UITabBarController {
			UIView indicator;
			int selected;
			
			public RotatingTabBar () : base ()
			{
			}
			
			public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
			{
				return (toInterfaceOrientation != UIInterfaceOrientation.PortraitUpsideDown);
			}

			void UpdatePosition (bool animate)
			{
				var w = View.Bounds.Width/5;
				var x = w * selected;
				
				if (animate){
					UIView.BeginAnimations (null);
					UIView.SetAnimationCurve (UIViewAnimationCurve.EaseInOut);
				}
				
				indicator.Frame = new RectangleF (x+((w-10)/2), View.Bounds.Height-TabBar.Bounds.Height-4, 10, 6);
				
				if (animate)
					UIView.CommitAnimations ();
			}
			
			public override void DidRotate (UIInterfaceOrientation fromInterfaceOrientation)
			{
				base.DidRotate (fromInterfaceOrientation);
				
				UpdatePosition (false);
			}
			
			public override void ViewWillAppear (bool animated)
			{
				base.ViewWillAppear (animated);
			
				if (indicator == null){
					indicator = new TriangleView (UIColor.FromRGB (0.26f, 0.26f, 0.26f), UIColor.Black);
					View.AddSubview (indicator);
					ViewControllerSelected += OnSelected;
					UpdatePosition (false);
				}
			}
			
			public void OnSelected (object sender, UITabBarSelectionEventArgs a)
			{
				var vc = ViewControllers;
				
				for (int i = 0; i < vc.Length; i++){
					if (vc [i] == a.ViewController){
						selected = i;
						UpdatePosition (true);
						return;
					}
				}
			}
		}
	}
}
