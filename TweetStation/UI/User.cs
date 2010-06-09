//
// User.cs: UserElement for now, eventually, the complete UI to render users
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
			lock (Database.Main){
				Database.Main.Execute ("BEGIN");
				var userStream = User.UnlockedLoadUsers (new MemoryStream (result));
				
				Root = new RootElement (StreamedTitle){
					new Section () {
						from user in userStream select (Element) new UserElement (user)
					}
				};
				Database.Main.Execute ("END");
			}
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