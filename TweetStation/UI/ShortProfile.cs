using System;
using System.Drawing;
using MonoTouch.Dialog;
using MonoTouch.CoreGraphics;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace TweetStation
{
	public class ShortProfileView : UIView, IImageUpdated
	{
		const int userSize = 19;
		const int followerSize = 13;
		const int locationSize = 14;
		const int urlSize = 14;
		const int TextX = 95;
		
		static UIFont userFont = UIFont.BoldSystemFontOfSize (userSize);
		static UIFont followerFont = UIFont.SystemFontOfSize (followerSize);
		static UIFont locationFont = UIFont.SystemFontOfSize (locationSize);
		static UIFont urlFont = UIFont.BoldSystemFontOfSize (urlSize);
		static CGPath borderPath = Graphics.MakeRoundedPath (75);
		
		UIImageView profilePic;
		UIButton url;
		User user;
		
		public ShortProfileView (RectangleF rect, long userId, bool discloseButton) : base (rect)
		{
			BackgroundColor = UIColor.Clear;
			
			user = User.FromId (userId);
			if (user == null){
				Console.WriteLine ("userid={0}", userId);
				return;
			}
			
			// Pics are 73x73, but we will add a border.
			profilePic = new UIImageView (new RectangleF (10, 10, 73, 73));
			profilePic.BackgroundColor = UIColor.Clear;
			
			profilePic.Image = ImageStore.RequestProfilePicture (-userId, user.PicUrl, this);
			AddSubview (profilePic);
			
			url = UIButton.FromType (UIButtonType.Custom);
			url.Font = urlFont;
			url.Font = urlFont;
			url.LineBreakMode = UILineBreakMode.TailTruncation;
			url.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
			url.TitleShadowOffset = new SizeF (0, 1);

			url.SetTitle (user.Url, UIControlState.Normal);
			url.SetTitle (user.Url, UIControlState.Highlighted);
			url.SetTitleColor (UIColor.FromRGB (0x32, 0x4f, 0x85), UIControlState.Normal);
			url.SetTitleColor (UIColor.Red, UIControlState.Highlighted);
			url.SetTitleShadowColor (UIColor.White, UIControlState.Normal);
			url.Frame = new RectangleF (TextX, 70, rect.Width-TextX, urlSize);
			
			url.AddTarget (delegate { if (UrlTapped != null) UrlTapped (); }, UIControlEvent.TouchUpInside);

			AddSubview (url);
			
			if (discloseButton){
				var button = UIButton.FromType (UIButtonType.DetailDisclosure);
				button.Frame = new RectangleF (290, 36, 20, 20);
				AddSubview (button);
			}
		}
		
		public event NSAction PictureTapped;
		public event NSAction UrlTapped;
		public event NSAction Tapped;
		
		public override void TouchesBegan (NSSet touches, UIEvent evt)
		{
			if (user == null)
				return;
			
			var touch = touches.AnyObject as UITouch;
			var location = touch.LocationInView (this);
			if (profilePic.Frame.Contains (location)){
				if (PictureTapped != null)
					PictureTapped ();
			} else {
				if (Tapped != null)
					Tapped ();
			}
		}
		
		public override void Draw (RectangleF rect)
		{
			// Perhaps we should never instantiate this view if the user is null.
			if (user == null)
				return;
			
			var w = rect.Width-TextX;
			var context = UIGraphics.GetCurrentContext ();
			
			context.SaveState ();
			context.SetRGBFillColor (0, 0, 0, 1);
			context.SetShadowWithColor (new SizeF (0, -1), 1, UIColor.White.CGColor);
			
			DrawString (user.Name, new RectangleF (TextX, 12, w, userSize), userFont, UILineBreakMode.TailTruncation);
			DrawString (user.Location, new RectangleF (TextX, 50, w, locationSize), locationFont, UILineBreakMode.TailTruncation);
			
			UIColor.DarkGray.SetColor ();
			DrawString (user.FollowersCount + " followers", new RectangleF (TextX, 34, w, followerSize), followerFont);

			//url.Draw (rect);
			
			// Spicy border around the picture
			context.RestoreState ();
			
			context.TranslateCTM (9, 9);
			context.AddPath (borderPath);
			context.SetRGBStrokeColor (0.5f, 0.5f, 0.5f, 1);
			context.SetLineWidth (0.5f);
			context.StrokePath ();
		}

		#region IImageUpdated implementation
		public void UpdatedImage (long id)
		{
			profilePic.Image = ImageStore.GetLocalProfilePicture (id);
		}
		#endregion
	}
}
