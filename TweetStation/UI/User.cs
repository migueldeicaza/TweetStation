//
// User.cs: UserElement for now, eventually, the complete UI to render users
//
using System;
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
}

