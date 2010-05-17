//
// The page that shows the various search options
// Search, Nearby, User, saved searches and trending topics
//
using System;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Web;

namespace TweetStation
{
	public class FavoritesViewController : BaseTimelineViewController {
		string name;
		
		public FavoritesViewController (string name) : base (true)
		{
			this.name = name;	
		}
		
		protected override string TimelineTitle {
			get {
				return "Favorites";
			}
		}
		
	}
}

