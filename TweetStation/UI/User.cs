//
// User.cs: UserElement for now, eventually, the complete UI to render users
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Dialog;

namespace TweetStation
{
	public class UserElement : StringElement 
	{
		public UserElement (User user) : base (user.Screenname)
		{
		}
		
		public override void Selected (DialogViewController dvc, UITableView tableView, MonoTouch.Foundation.NSIndexPath path)
		{
			dvc.ActivateController (new FullProfileView (Caption));
		}
	}
}

