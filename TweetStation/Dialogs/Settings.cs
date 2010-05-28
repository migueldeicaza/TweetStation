using System;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace TweetStation
{
	// 
	// The view controller for our settings, it overrides the source class so we can
	// make it editable
	//
	public class Settings : DialogViewController {
		DialogViewController parent;
		
		// 
		// This source overrides the EditingStyleForRow to enable editing
		// of the table.   The editing is triggered with a button on the navigation bar
		//
		class MySource : Source {
			Settings parent;
			
			public MySource (Settings parent) : base (parent)
			{
				this.parent = parent;
			}
			
			public override UITableViewCellEditingStyle EditingStyleForRow (UITableView tableView, NSIndexPath indexPath)
			{
				if (indexPath.Section == 0 && indexPath.Row < parent.Root [0].Count-1) 
					return UITableViewCellEditingStyle.Delete;
				return UITableViewCellEditingStyle.None;
			}
			
			public override void CommitEditingStyle (UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
			{
				var account = (parent.Root [indexPath.Section][indexPath.Row] as AccountElement).Account;
				
				TwitterAccount.Remove (account);
			}
		}
		
		public override Source CreateSizingSource (bool unevenRows)
		{
			return new MySource (this);
		}
		
		Section MakeAccounts ()
		{
			var section = new Section (Locale.GetText ("Accounts"));
			
			foreach (var account in Database.Main.Query<TwitterAccount> ("SELECT * from TwitterAccount")){
				var copy = account;
				var element = new AccountElement (account);
				element.Tapped += delegate {
					DismissModalViewControllerAnimated (true);
					
					TwitterAccount.SetDefault (copy);
					Util.MainAppDelegate.Account = copy;
				};
				section.Add (element);
			};
			var addAccount = new StringElement (Locale.GetText ("Add account"));
			addAccount.Tapped += delegate {
				Util.MainAppDelegate.AddAccount (this, delegate {
					DismissModalViewControllerAnimated (false);
				});
			};
			section.Add (addAccount);
			return section;
		}
		
		void SetupLeftItemEdit ()
		{
			NavigationItem.LeftBarButtonItem = new UIBarButtonItem (UIBarButtonSystemItem.Edit, delegate {
				SetupLeftItemDone ();
				TableView.SetEditing (true, true);
			});
		}
		
		void SetupLeftItemDone ()
		{
			NavigationItem.LeftBarButtonItem = new UIBarButtonItem (UIBarButtonSystemItem.Done, delegate {
				SetupLeftItemEdit ();
				TableView.SetEditing (false, true);
			});
		}
		
		
		public override void ViewWillAppear (bool animated)
		{
			NavigationItem.RightBarButtonItem = new UIBarButtonItem (Locale.GetText ("Close"), UIBarButtonItemStyle.Plain, delegate {
				DismissModalViewControllerAnimated (true);
			});
			if (Root [0].Count > 2)
				SetupLeftItemEdit ();
		}
		
		BooleanElement playMusic;
		BooleanElement chicken;
		
		public Settings (DialogViewController parent) : base (UITableViewStyle.Grouped, null)
		{
			this.parent = parent;
			Root = new RootElement (Locale.GetText ("Settings")){
				MakeAccounts (),
				new Section (){
					new RootElement (Locale.GetText ("Inspiration")){
						new Section (Locale.GetText ("Magic"), 
						             Locale.GetText ("Twitter is best used when you are inspired\n" +
						                             "to write the best possible tweets.  I picked\n" +
						                             "music and audio that should inspire\n" +
						                             "you to create clever tweets")){
							(playMusic = new BooleanElement (Locale.GetText ("Music on Composer"), Util.Defaults.IntForKey ("disableMusic") == 0)),
							(chicken = new BooleanElement (Locale.GetText ("Chicken noises"), Util.Defaults.IntForKey ("disableChickens") == 0)),
						}
					},
					//new RootElement (Locale.GetText ("Services"))
				},
				new Section (){
					new RootElement (Locale.GetText ("About")){
						new Section (){
							new StringElement (Locale.GetText ("Version"))
						},
						new Section (){
							new RootElement ("@migueldeicaza", delegate { return new FullProfileView ("migueldeicaza"); }),
							new RootElement ("@icluck", delegate { return new FullProfileView ("icluck"); }),
						},
						new Section (){
							new StringElement (Locale.GetText ("Credits"))
						},
						new Section () {
							new HtmlElement (Locale.GetText ("Web site"), "http://tirania.org/tweetstation")
						}
					}
				}
			};
			playMusic.ValueChanged += delegate {
				Util.Defaults.SetInt (playMusic.Value ? 0 : 1, "disableMusic");
				Util.Defaults.Synchronize ();
			};
			chicken.ValueChanged += delegate {
				Util.Defaults.SetInt (chicken.Value ? 0 : 1, "disableChickens");
				Util.Defaults.Synchronize ();
			};
		}
	}

	// An element for Accounts that supports deleting
	public class AccountElement : Element {
		static NSString skey = new NSString ("AccountElement");
		public TwitterAccount Account;
		
		public AccountElement (TwitterAccount account) : base (account.Username) 
		{
			Account = account;	
		}
		
		public event NSAction Tapped;
				
		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (skey);
			if (cell == null){
				cell = new UITableViewCell (UITableViewCellStyle.Default, skey) {
					SelectionStyle = UITableViewCellSelectionStyle.Blue,
					Accessory = UITableViewCellAccessory.None,
				};
			}
			cell.TextLabel.Text = Caption;
			
			return cell;
		}

		public override string Summary ()
		{
			return Caption;
		}
		
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath indexPath)
		{
			if (Tapped != null)
				Tapped ();
			tableView.DeselectRow (indexPath, true);
		}
	}
}

