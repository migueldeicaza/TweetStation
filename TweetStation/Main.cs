
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Json;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;

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
			CreatePhoneGui ();
			if (options != null){
				var url = options.ObjectForKey (UIApplication.LaunchOptionsUrlKey) as NSUrl;
				Console.WriteLine ("The url was: {0}", url.AbsoluteUrl);
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
			favorites = new StreamedTimelineViewController ("Favorites", new Uri ("http://api.twitter.com/version/favorites.json"));
			
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
						
			tabbarController.SetViewControllers (navigationRoots, false);
			window.MakeKeyAndVisible ();

			var defaultAccount = TwitterAccount.GetDefaultAccount ();
			if (defaultAccount == null){
				var editor = new EditAccount (this, null, false);
				tabbarController.PresentModalViewController (editor, false);
			}
			Account = defaultAccount;
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

	}
}
