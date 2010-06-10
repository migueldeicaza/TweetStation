
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
			UIApplication.Main (args);
		}
	}

	// The name AppDelegate is referenced in the MainWindow.xib file.
	public partial class AppDelegate : UIApplicationDelegate, IAccountContainer
	{
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
			window.MakeKeyAndVisible ();

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

			main = new TimelineViewController ("Friends", TweetKind.Home, false);
			mentions = new TimelineViewController ("Mentions", TweetKind.Replies, false); 
			messages = new TimelineViewController ("Messages", TweetKind.Direct, false);
			searches = new SearchesViewController ();
			favorites = StreamedTimelineViewController.MakeFavorites ("http://api.twitter.com/1/favorites.json");
			
			navigationRoots = new UINavigationController [5] {
				new UINavigationController (main) {
					TabBarItem = new UITabBarItem ("Friends", UIImage.FromFileUncached ("Images/home.png"), 0),
				},
				new UINavigationController (mentions) {
					TabBarItem = new UITabBarItem ("Mentions", UIImage.FromFileUncached ("Images/replies.png"), 1)
				},
				new UINavigationController (messages) {
					TabBarItem = new UITabBarItem ("Messages", UIImage.FromFileUncached ("Images/messages.png"), 2)
				},
				new UINavigationController (favorites) {
					TabBarItem = new UITabBarItem ("Favorites", UIImage.FromFileUncached ("Images/fav.png"), 3)
				},
				new UINavigationController (searches) {
					TabBarItem = new UITabBarItem ("Search", UIImage.FromFileUncached ("Images/lupa.png"), 4)
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
			sheet.ShowInView (Util.MainAppDelegate.MainView);
		}
		
		// Broadcast the change, since tweets can be instantiated multiple times.
		public void FavoriteChanged (Tweet tweet)
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
			var login = new EntryElement ("Username", "Your twitter username", "");
			var password = new EntryElement ("Password", "Your password", "", true);
			var root = new RootElement (Locale.GetText ("Login")){
				new Section (){
					login,
					password
				},
				new Section (){
					new LoadMoreElement ("Login to Twitter", "Contacting twitter", delegate {
						StartXauthLogin (login.Value, password.Value, callback); 
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
		
		public class PositionView : UIView {
			public PositionView () 
			{
				Opaque = false;
			}
			
			public override void Draw (RectangleF rect)
			{
				var context = UIGraphics.GetCurrentContext ();
				
				context.SetRGBFillColor (0.26f, 0.26f, 0.26f, 1);
				context.MoveTo (0, 6);
				context.AddLineToPoint (5, 0);
				context.AddLineToPoint (10, 6);
				context.ClosePath ();
				context.FillPath ();
				
				context.SetRGBStrokeColor (0, 0, 0, 1);
				context.MoveTo (0, 5);
				context.AddLineToPoint (5, 0);
				context.AddLineToPoint (10, 5);
				context.StrokePath ();
			}
		}
	
		public class RotatingTabBar : UITabBarController {
			UIView indicator;
			
			public RotatingTabBar () : base ()
			{
				indicator = new PositionView ();
				//indicator = UIButton.FromType (UIButtonType.DetailDisclosure);
				View.AddSubview (indicator);
				ViewControllerSelected += OnSelected;
			}
			
			public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
			{
				return (toInterfaceOrientation != UIInterfaceOrientation.PortraitUpsideDown);
			}

			void UpdatePosition (int pos)
			{
				var w = View.Bounds.Width/5;
				var x = w * pos;
				
				UIView.BeginAnimations (null);
				UIView.SetAnimationCurve (UIViewAnimationCurve.EaseInOut);
				indicator.Frame = new RectangleF (x+((w-10)/2), View.Bounds.Height-TabBar.Bounds.Height-4, 10, 6);
				UIView.CommitAnimations ();
			}
			
			public override void ViewWillAppear (bool animated)
			{
				base.ViewWillAppear (animated);
				
				UpdatePosition (0);
			}
			
			public void OnSelected (object sender, UITabBarSelectionEventArgs a)
			{
				var vc = ViewControllers;
				
				for (int i = 0; i < vc.Length; i++){
					if (vc [i] == a.ViewController){
						UpdatePosition (i);
						return;
					}
				}
			}
		}
	}
}
