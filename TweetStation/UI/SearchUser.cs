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
		protected SearchMirrorElement searchMirror;
		
		public SearchDialog () : base (null, true)
		{
			EnableSearch = true;
			Style = UITableViewStyle.Plain;
		}
		
		public override void OnSearchTextChanged (string text)
		{
			base.OnSearchTextChanged (text);
			searchMirror.Text = text;
			TableView.SetNeedsDisplay ();
		}
		
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			
			searchMirror = new SearchMirrorElement ();
			Section entries = new Section () {
				searchMirror
			};
			PopulateSearch (entries);
			
			Root = new RootElement (Locale.GetText ("Search")){
				entries,
			};

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
		
		public override void Selected (NSIndexPath indexPath)
		{
			var element = Root [0][indexPath.Row];
			DialogViewController dvc;
			
			if (element is SearchMirrorElement)
				dvc = new FullProfileView (((SearchMirrorElement) element).Text);
			else
				dvc = new FullProfileView (((StringElement) element).Caption);
			
			ActivateController (dvc);
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
			set { text = value; Caption = Locale.Format ("Go to user '{0}'", text); }
		}
		
		public SearchMirrorElement () : base ("Foo")
		{
			TextColor = UIColor.FromRGB (0.13f, 0.43f, 0.84f);
			Font = UIFont.SystemFontOfSize (14);
		}
		
		public override bool Matches (string test)
		{
			return !String.IsNullOrEmpty (text);
		}		
	}
}

