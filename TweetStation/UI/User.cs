//
// User.cs: UserElement for now, eventually, the complete UI to render users
//
using System;
using System.IO;
using System.Linq;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;

namespace TweetStation
{
	public class UserCell : UITableViewCell, IImageUpdated {
		User user;

		public UserCell (NSString key) : base (UITableViewCellStyle.Subtitle, key)
		{
		}
		
		public void UpdateFromUser (User user)
		{
			if (user == this.user)
				return;
			this.user = user;
			TextLabel.Text = user.Screenname;
			DetailTextLabel.Text = user.Name;
			ImageView.Image = ImageStore.RequestProfilePicture (user.Id, user.PicUrl, this);
			SetNeedsDisplay ();
		}
		
		public void UpdatedImage (long id)
		{
			if (id == user.Id)
				ImageView.Image = ImageStore.GetLocalProfilePicture (id);
		}
	}
	
	public class UserElement : Element
	{
		public readonly User User;
		static NSString ukey = new NSString ("UserElement");
		
		public UserElement (User user) : base (user.Screenname)
		{
			User = user;
		}
		
		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (ukey);
			if (cell == null){
				cell = new UserCell (ukey) {
					SelectionStyle = UITableViewCellSelectionStyle.Blue,
					Accessory = UITableViewCellAccessory.DisclosureIndicator
				};
			}
			((UserCell) cell).UpdateFromUser (User);
			
			return cell;
		}
		
		public override void Selected (DialogViewController dvc, UITableView tableView, MonoTouch.Foundation.NSIndexPath path)
		{
			dvc.ActivateController (new FullProfileView (Caption));
		}
	}

	// 
	// A progressive loader for user results
	//
	public class StreamedUserViewController : StreamedViewController {
		public StreamedUserViewController (string title, string url, User reference) : base (title, url, reference)
		{
		}

		protected override void PopulateRootFrom (byte [] result)
		{
			Database.Main.Execute ("BEGIN");
			var userStream = User.LoadUsers (new MemoryStream (result));
			
			Root = new RootElement (StreamedTitle){
				new Section () {
					from user in userStream select (Element) new UserElement (user)
				}
			};
			Database.Main.Execute ("END");
		}
	}
	
	public class UserRootElement : RootElement {
		User reference;
		string url;
		
		public UserRootElement (User reference, string caption, string url) : base (caption)
		{
			this.reference = reference;
			this.url = url;
		}
		
		protected override UIViewController MakeViewController ()
		{
			return new StreamedUserViewController (reference.Screenname, url, reference) {
				Account = TwitterAccount.CurrentAccount
			};
		}
	}
}