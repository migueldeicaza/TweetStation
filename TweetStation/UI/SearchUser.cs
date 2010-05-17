//
// Search for a user
//
// Author: Miguel de Icaza (miguel@gnome.org)
//
using System;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;
using System.Linq;

namespace TweetStation
{
	public abstract class SearchDialog : DialogViewController {
		SearchMirrorElement searchMirror;
		Section entries;
		
		public SearchDialog () : base (UITableViewStyle.Plain, null)
		{
			EnableSearch = true;
		}
		
		public override void OnSearchTextChanged (string text)
		{
			base.OnSearchTextChanged (text);
			searchMirror.Text = text;
		}
		
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			
			searchMirror = new SearchMirrorElement ();
			Section entries = new Section () {
				searchMirror
			};
			
			Root = new RootElement (Locale.GetText ("Search")){
				entries,
			};
			PopulateSearch (entries);

			StartSearch ();
			PerformFilter ("");
		}
		
		public abstract void PopulateSearch (Section entries);
	}

	public class SearchUser : SearchDialog {
		public SearchUser ()
		{
			Console.WriteLine ("User");
		}
		
		public override void PopulateSearch (Section entries)
		{
			entries.Add (from x in Database.Main.Query<User> ("SELECT Screenname from User ORDER BY Screenname")
				             select (Element) new StringElement (x.Screenname));
		}
	}
	
	// 
	// Just a styled string element, but if the search string is not empty
	// the Matches method always returns true
	//
	public class SearchMirrorElement : StyledStringElement {
		string text;
		public string Text { 
			get { return text; }
			set { text = value; Value = Locale.Format ("Go to user '{0}'", text); }
		}
		
		public SearchMirrorElement () : base ("")
		{
		}
		
		public override bool Matches (string test)
		{
			return !String.IsNullOrEmpty (text);
		}
		
	}
}

