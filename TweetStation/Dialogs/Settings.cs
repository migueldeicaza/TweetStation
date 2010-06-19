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
// using System;
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
				
				parent.Root [indexPath.Section].Remove (indexPath.Row);
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
			
			lock (Database.Main){
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
			}
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
		BooleanElement selfOnRight;
		BooleanElement shadows;
		RootElement compress;
		
		public override void ViewWillDisappear (bool animated)
		{
			base.ViewWillDisappear (animated);
			
			Util.Defaults.SetInt (playMusic.Value ? 0 : 1, "disableMusic");
			Util.Defaults.SetInt (chicken.Value ? 1 : 0, "enableChickens");
			
			int style = (selfOnRight.Value ? 0 : 1) | (shadows.Value ? 0 : 2);
			
			Util.Defaults.SetInt (style, "cellStyle");
			TweetCell.CellStyle = style;
			BaseTimelineViewController.ChickenNoisesEnabled = chicken.Value;
			
			Util.Defaults.SetInt (compress.RadioSelected, "sizeCompression");
			Util.Defaults.Synchronize ();

			parent.ReloadData ();
		}
		
		public Settings (DialogViewController parent) : base (UITableViewStyle.Grouped, null)
		{
			this.parent = parent;
			var aboutUrl = NSUrl.FromFilename ("about.html");
			int cellStyle = Util.Defaults.IntForKey ("cellStyle");
			
			Root = new RootElement (Locale.GetText ("Settings")){
				MakeAccounts (),
				new Section (){
					new RootElement (Locale.GetText ("Settings")){
						new Section (Locale.GetText ("Style")){
							(selfOnRight = new BooleanElement (Locale.GetText ("My tweets on right"), (cellStyle & 1) == 0)),
							(shadows = new BooleanElement (Locale.GetText ("Avatar shadows"), (cellStyle & 2) == 0)),
							(compress = new RootElement ("Image Compression", new RadioGroup ("group", Util.Defaults.IntForKey ("sizeCompression"))) {
								new Section () {
									new RadioElement ("Maximum"),
									new RadioElement ("Medium"),
									new RadioElement ("None")
								}
							})
						},
						new Section (Locale.GetText ("Inspiration"), 
						             Locale.GetText ("Twitter is best used when you are inspired.\n" +
						             				 "I picked music and audio that should\n" +
						                             "inspire you to create clever tweets")){
							(playMusic = new BooleanElement (Locale.GetText ("Music on Composer"), Util.Defaults.IntForKey ("disableMusic") == 0)),
							(chicken = new BooleanElement (Locale.GetText ("Chicken noises"), Util.Defaults.IntForKey ("enableChickens") != 0)),
						}
					},
					//new RootElement (Locale.GetText ("Services"))
				},
				
				new Section (){
					new RootElement (Locale.GetText ("About")){
						new Section (){
							new HtmlElement (Locale.GetText ("About and Credits"), aboutUrl),
							new HtmlElement (Locale.GetText ("Web site"), "http://tirania.org/tweetstation")
						},
						new Section (){
							Twitterista ("migueldeicaza"),
							Twitterista ("itweetstation"),
						},
						new Section ("Music"){
							Twitterista ("kmacleod"),
						},
						new Section ("Conspirators") {
							Twitterista ("JosephHill"),
							Twitterista ("kangamono"),
							Twitterista ("lauradeicaza"),
							Twitterista ("mancha"),
							Twitterista ("mjhutchinson"),
						},
						new Section ("Contributors"){
							Twitterista ("martinbowling"),
						},
						new Section ("Includes X11 code from") {
							Twitterista ("escoz"),
							Twitterista ("praeclarum"),
						}
					}
				}
			};
		}
	
		public RootElement Twitterista (string name)
		{
			return new RootElement ("@" + name, delegate { return new FullProfileView (name); });
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

