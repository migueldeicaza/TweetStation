//
// Search for a user
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
using System.Collections.Generic;
using System.IO;
using System.Web;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;
using System.Linq;
using System.Threading;

namespace TweetStation
{
	public class SearchViewController : StreamedTimelineViewController {
		
		public SearchViewController (string search) :
			base (search, "http://search.twitter.com/search.json?q=" + OAuth.PercentEncode (search), "rpp=", 50, "since_id=", "page=", null)
		{
			//View.AddSubview (bottomBar);
		}
		
		protected override IEnumerable<Tweet> GetTweetStream (Stream result)
		{
			return Tweet.TweetsFromSearchResults (result, ReferenceUser);
		}
	}
	
	public class SearchFromGeo : StreamedTimelineViewController {
		public SearchFromGeo (MonoTouch.CoreLocation.CLLocation location) :
			base (Locale.GetText ("Nearby Tweets"), 
			      String.Format (Util.AmericanCulture, "http://search.twitter.com/search.json?geocode={0},{1},5km", 
					location.Coordinate.Latitude,
					location.Coordinate.Longitude),
			      "rpp=", 50, "since_id=", "page=", null)
		{
		}

		protected override IEnumerable<Tweet> GetTweetStream (Stream result)
		{
			return Tweet.TweetsFromSearchResults (result, ReferenceUser);
		}
	}
	
	public class SearchElement : RootElement {
		string query;
		
		public SearchElement (string caption, string query) : base (caption)
		{
			this.query = query;
		}

		protected override UIViewController MakeViewController ()
		{
			return new SearchViewController (query) { Account = TwitterAccount.CurrentAccount };
		}
		
	}
	
	public abstract class SearchDialog : DialogViewController {
		protected SearchMirrorElement SearchMirror;
		
		public SearchDialog () : base (null, true)
		{
			EnableSearch = true;
			Style = UITableViewStyle.Plain;
		}

		public override void SearchButtonClicked (string text)
		{
			Save ();
			ActivateController (new SearchViewController (text) { Account = TwitterAccount.CurrentAccount });
		}
		
		public override void OnSearchTextChanged (string text)
		{
			base.OnSearchTextChanged (text);
			SearchMirror.Text = text;
			TableView.SetNeedsDisplay ();
		}

		public abstract SearchMirrorElement MakeMirror ();
		
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			
			SearchMirror = MakeMirror ();
			Section entries = new Section () {
				SearchMirror
			};

			PopulateSearch (entries);
			
			Root = new RootElement (Locale.GetText ("Search")){
				entries,
			};

			StartSearch ();
			PerformFilter ("");
		}
		
		public string GetItemText (NSIndexPath indexPath)
		{
			var element = Root [0][indexPath.Row];
			
			if (element is SearchMirrorElement)
				return ((SearchMirrorElement) element).Text;
			else if (element is StringElement){
				return ((StringElement) element).Caption;
			} else if (element is UserElement) {
				return ((UserElement) element).User.Screenname;
			} else
				throw new Exception ("Unknown item in SearchDialog");
		}
		
		public abstract void PopulateSearch (Section entries);
		
		public virtual void Save () {}
	}

	public class SearchUser : SearchDialog {
		public SearchUser ()
		{
		}
		
		public override SearchMirrorElement MakeMirror ()
		{
			return new SearchMirrorElement (Locale.GetText ("Go to user `{0}'"));
		}
		
		public override void PopulateSearch (Section entries)
		{
			lock (Database.Main)
				entries.Add (from x in Database.Main.Query<User> ("SELECT * from User ORDER BY Screenname")
				             select (Element) new UserElement (x));
		}
		
		public override void Selected (NSIndexPath indexPath)
		{
			ActivateController (new FullProfileView (GetItemText (indexPath)));
		}
	}
	
	// 
	// The user selector is just like the user search, but does not activate the
	// nested controller, instead it sets the value and dismisses the controller
	//
	public class UserSelector : SearchUser {
		Action<string> userSelected;
		
		public UserSelector (Action<string> userSelected)
		{
			this.userSelected = userSelected;
		}
		
		public override SearchMirrorElement MakeMirror ()
		{
			 return new SearchMirrorElement (Locale.GetText ("@{0}"));
		}
		
		public override void Selected (NSIndexPath indexPath)
		{
			var text = GetItemText (indexPath);
			DismissModalViewControllerAnimated (false);
			
			userSelected (text);
		}
		
		public override void FinishSearch ()
		{
			base.FinishSearch ();
			DismissModalViewControllerAnimated (true);
		}
	}
	
	public class TwitterTextSearch : SearchDialog {
		public TwitterTextSearch () {}
		List<string> terms;
		
		public override void PopulateSearch (Section entries)
		{
			int n = Util.Defaults.IntForKey ("searches");

			terms = (from idx in Enumerable.Range (0, n)
			              let value = Util.Defaults.StringForKey ("u-" + idx)
			              where value != null select value).Distinct ().ToList ();
			
			entries.Add (from term in terms select (Element) new StringElement (term));
		}
		
		public override SearchMirrorElement MakeMirror ()
		{
			return new SearchMirrorElement (Locale.GetText ("Search `{0}'"));
		}
		
		public override void Selected (NSIndexPath indexPath)
		{
			if (SearchMirror.Text != ""){
				terms.Add (SearchMirror.Text);
				
				Save ();
			}

			ActivateController (new SearchViewController (GetItemText (indexPath)) { Account = TwitterAccount.CurrentAccount });
		}
		
		public override void Save ()
		{
			Util.Defaults.SetInt (terms.Count, "searches");
			int i = 0;
			foreach (string s in terms)
				Util.Defaults.SetString (s, "u-" + i++);
		}
	}
	
	// 
	// Just a styled string element, but if the search string is not empty
	// the Matches method always returns true
	//
	public class SearchMirrorElement : StyledStringElement {
		string text, format;
		
		public string Text { 
			get { return text; }
			set { text = value; Caption = Locale.Format (format, text); }
		}
		
		public SearchMirrorElement (string format) : base ("")
		{
			this.format = format;
			TextColor = UIColor.FromRGB (0.13f, 0.43f, 0.84f);
			Font = UIFont.BoldSystemFontOfSize (18);
		}
		
		public override bool Matches (string test)
		{
			return !String.IsNullOrEmpty (text);
		}		
	}
}

