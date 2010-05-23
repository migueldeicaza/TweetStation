
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Json;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;

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
		TwitterAccount account;
		TimelineViewController main, mentions, messages;
		SearchesViewController searches;
		StreamedViewController favorites;
		public UIView MainView;
		
		UINavigationController [] navigationRoots;
		
		public override bool FinishedLaunching (UIApplication app, NSDictionary options)
		{
			window.MakeKeyAndVisible ();
			if (options != null){
				var url = options.ObjectForKey (UIApplication.LaunchOptionsUrlKey) as NSUrl;
				Console.WriteLine ("The url was: {0}", url.AbsoluteUrl);
			}

			System.Net.ServicePointManager.Expect100Continue = false;
			
			var defaultAccount = TwitterAccount.GetDefaultAccount ();
			if (defaultAccount == null)
				CreateDefaultAccount ();
			else {
				CreatePhoneGui ();
				Account = defaultAccount;
			}
			return true;
		}
		
		void CreatePhoneGui ()
		{
			MainView = tabbarController.View;
			window.AddSubview (MainView);

			main = new TimelineViewController ("Friends", TweetKind.Home, false);
			mentions = new TimelineViewController ("Mentions", TweetKind.Replies, false); 
			messages = new TimelineViewController ("Messages", TweetKind.Direct, false);
			searches = new SearchesViewController ();
			favorites = new StreamedTimelineViewController ("Favorites", "http://api.twitter.com/version/favorites.json");
			
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
					TabBarItem = new UITabBarItem ("Search", UIImage.FromFileUncached ("Images/lupa.png"), 3)
				}
			};
	
#if false
			TwitterOAuth oauth = new TwitterOAuth ();
			if (File.Exists ("/Users/miguel/xauth")){
				using (var config = File.OpenText ("/Users/miguel/xauth")){
					oauth.ConsumerKey = config.ReadLine ();
					oauth.ConsumerSecret = config.ReadLine ();
					oauth.xAuthUsername = config.ReadLine ();
					oauth.xAuthPassword = config.ReadLine ();
				}
			} else {
				// Settings for Tweetstation Public, they require the web browser
				oauth.ConsumerKey = "VSemGxR6ZNpo5IWY3dS8uQ";
				oauth.Callback = "http://tirania.org/tweetstation/oauth";
				oauth.ConsumerSecret = "MEONRf8QqJDotJWioW1v1sSZVhXlOsTI85xu9eZfJf8";
			}
			
			if (oauth.xAuthUsername != null){
				if (oauth.AcquireAccessToken ()){
					oauth.xAuthPassword = null;
				}
			}
			else {
				if (oauth.AcquireRequestToken ())
					oauth.AuthorizeUser (tabbarController);
			}
#endif
			tabbarController.SetViewControllers (navigationRoots, false);
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
			
			var sheet = new UIActionSheet ("");
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
		
		//
		// Creates the default account using OAuth
		//
		void CreateDefaultAccount ()
		{
			DialogViewController dvc = null;
			
			var root = new RootElement (Locale.GetText ("Login to Twitter")){
				new Section (Locale.GetText ("\n\n\n" +
				                             "Welcome to TweetStation!\n\n" +
				                             "To get started, authorize\n" +
				                             "TweetStation to get access\n" +  
				                             "to your twitter account.\n\n")){
					new StringElement ("Login to Twitter", delegate { StartLogin (dvc); })
				}
			};
			
			dvc = new DialogViewController (UITableViewStyle.Grouped, root);
			window.AddSubview (dvc.View);
		}
		
		void StartLogin (DialogViewController dvc)
		{
			var oauth = new OAuthAuthorizer (TwitterAccount.OAuthConfig);

			if (oauth.AcquireRequestToken ()){
				oauth.AuthorizeUser (dvc, delegate {
					dvc.View.RemoveFromSuperview ();
					CreatePhoneGui ();
					SetDefaultAccount (oauth);
				});
			}
		}
		
		public void SetDefaultAccount (OAuthAuthorizer oauth)
		{
			var newAccount = TwitterAccount.Create (oauth);
			TwitterAccount.SetDefault (newAccount);
			Account = newAccount;
		}
	}
}
