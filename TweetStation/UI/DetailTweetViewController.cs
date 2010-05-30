//
// DetailTweetViewController.cs:
//   Renders a full tweet, with the user profile information
//   and useful operations for it
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
//
using System;
using System.Drawing;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.IO;
using MonoTouch.CoreGraphics;

namespace TweetStation
{
	public class DetailTweetViewController : DialogViewController {
		const int PadX = 4;
		Tweet tweet;
		static string [] buttons = new string [] { 
			Locale.GetText ("Reply"), 
			Locale.GetText ("Retweet"),
			Locale.GetText ("Direct") };
		
		public DetailTweetViewController (Tweet tweet) : base (UITableViewStyle.Grouped, null, true)
		{
			this.tweet = tweet;
			var handlers = new EventHandler [] { Reply, Retweet, Direct };
			var profileRect = new RectangleF (PadX, 0, View.Bounds.Width-30-PadX*2, 100);
			var detailRect = new RectangleF (PadX, 0, View.Bounds.Width-30-PadX*2, 0);
			 
			var shortProfileView = new ShortProfileView (profileRect, tweet.UserId, true);
			shortProfileView.PictureTapped += delegate { PictureViewer.Load (this, tweet.UserId); };
			shortProfileView.Tapped += LoadFullProfile;
			shortProfileView.UrlTapped += delegate { WebViewController.OpenUrl (this, User.FromId (tweet.UserId).Url); };
			
			var main = new Section (shortProfileView){
				new UIViewElement (null, new DetailTweetView (detailRect, tweet, ContentHandler, this), false) { 
					Flags = UIViewElement.CellFlags.DisableSelection 
				}
			};			
			if (tweet.InReplyToStatus != 0){
				var in_reply = new ConversationRootElement (Locale.Format ("In reply to: {0}", tweet.InReplyToUserName), tweet);
			                        
				main.Add (in_reply);
			}
			
			Section replySection = new Section ();
			if (tweet.Kind == TweetKind.Direct)
				replySection.Add (new StringElement (Locale.GetText ("Direct Reply"), delegate { Direct (this, EventArgs.Empty); }));
			else 
				replySection.Add (new UIViewElement (null, new ButtonsView (buttons, handlers), true) {
					Flags = UIViewElement.CellFlags.DisableSelection | UIViewElement.CellFlags.Transparent
				});
			
			Root = new RootElement (tweet.Screename){
				main,
				replySection,
				new Section () {
					TimelineRootElement.MakeTimeline (tweet.Screename, Locale.GetText ("User's timeline"), "http://api.twitter.com/1/statuses/user_timeline.json?skip_user=true&id=" + tweet.UserId, User.FromId (tweet.UserId))
				}
			};
		}

		//
		// Invoked by the TweetView when the content is tapped
		//
		void ContentHandler (string data)
		{
			Util.MainAppDelegate.Open (this, data);
		}
		
		void LoadFullProfile ()
		{
			ActivateController (new FullProfileView (tweet.UserId));
		}
		
		void DisplayUserTimeline ()
		{
			
		}
		
		void Reply (object sender, EventArgs args)
		{
			Util.MainAppDelegate.Reply (this, tweet);
		}
		
		void Direct (object sender, EventArgs args)
		{
			Composer.Main.Direct (this, tweet.Screename);
		}
		
		UIActionSheet sheet;
		void Retweet (object sender, EventArgs args)
		{
			sheet = Util.GetSheet (Locale.GetText ("Retweet"));
			sheet.AddButton (Locale.GetText ("Retweet"));
			sheet.AddButton (Locale.GetText ("Quote Retweet"));
			sheet.AddButton (Locale.GetText ("Cancel"));
			sheet.CancelButtonIndex = 2;
			
			sheet.Clicked += delegate(object s, UIButtonEventArgs e) {
				if (e.ButtonIndex == 0)
					TwitterAccount.CurrentAccount.Post ("http://api.twitter.com/1/statuses/retweet/" + tweet.Id + ".json", ""); 
				else if (e.ButtonIndex == 1){
					Composer.Main.Quote (this, tweet);
				}
			};
			sheet.ShowInView (Util.MainAppDelegate.MainView);
		}
	}
	
	public class DetailTweetView : UIView, IImageUpdated {
		static CGPath borderPath = Graphics.MakeRoundedPath (78);
		static UIImage off = UIImage.FromFileUncached ("Images/star-off.png");
		public static UIImage on = UIImage.FromFileUncached ("Images/star-on.png");
		const int PadY = 4;
		const int smallSize = 12;
		
		// If there is a picture, this contains the Y offset of the picture start
		float borderAt;
		TweetView tweetView;
		UIButton buttonView;
		UIImage image;
		
		public DetailTweetView (RectangleF rect, Tweet tweet, TweetView.TappedEvent handler, DialogViewController parent) : base (rect)
		{
			var tweetRect = rect;
			if (tweet.Kind != TweetKind.Direct)
				tweetRect.Width -= 30;
			
			BackgroundColor = UIColor.Clear;
			tweetView = new TweetView (tweetRect, tweet.Text){
				BackgroundColor = UIColor.Clear,
			};
			if (handler != null)
				tweetView.Tapped += handler;
			
			AddSubview (tweetView);
			float y = tweetView.Frame.Height + PadY;
			
			string thumbUrl, previewUrl;
			var picUrl = PicDetect.FindPicUrl (tweet.Text, out thumbUrl, out previewUrl);
			if (picUrl != null){
				borderAt = y;
				SetupImagePreview (parent, y, picUrl, thumbUrl, previewUrl);
				y += 90;
			} 
			
			rect.Y = y;
			rect.Height = smallSize;
			AddSubview (new UILabel (rect) {
				Text = Util.FormatTime (new TimeSpan (DateTime.UtcNow.Ticks - tweet.CreatedAt)) + " ago from " + tweet.Source,
				TextColor = UIColor.Gray,
				Font = UIFont.SystemFontOfSize (smallSize)
			});
			y += PadY;
				
			var f = Frame;
			f.Y += PadY;
			f.Height = y + PadY + smallSize + 2;
			Frame = f;

			if (tweet.Kind != TweetKind.Direct){
				// Now that we now our size, center the button
				buttonView = UIButton.FromType (UIButtonType.Custom);
				buttonView.Frame = new RectangleF (tweetRect.X + tweetRect.Width, (f.Height-38)/2-4, 38, 38);
				UpdateButtonImage (tweet);

				buttonView.TouchDown += delegate {
					tweet.Favorited = !tweet.Favorited;
					Util.MainAppDelegate.FavoriteChanged (tweet);
					TwitterAccount.CurrentAccount.Post (String.Format ("http://api.twitter.com/1/favorites/{0}/{1}.json", tweet.Favorited ? "create" : "destroy", tweet.Id),"");
					UpdateButtonImage (tweet);
					tweet.Replace (Database.Main);
				};
			
				AddSubview (buttonView);
			}
		}
		
		void SetupImagePreview (DialogViewController parent, float y, string picUrl, string thumbUrl, string previewUrl)
		{
			var rect = new RectangleF (0, y, 78, 78);
			
			var imageButton = new UIButton (rect) {
				BackgroundColor = UIColor.Clear
			};
			imageButton.TouchUpInside += delegate {
				string html = "<html><body style='background-color:black'><div style='text-align:center; width: 320px; height: 480px;'><img src='{0}'/></div></body></html>";
				WebViewController.OpenHtmlString (parent, String.Format (html, previewUrl), new NSUrl (picUrl).BaseUrl);
			};
			AddSubview (imageButton);
			ImageStore.QueueRequestForPicture (serial++, thumbUrl, this);
		}

		public override void Draw (RectangleF rect)
		{
			Console.WriteLine (rect);
			if (borderAt < 1)
				return;
				
			var context = UIGraphics.GetCurrentContext ();
			context.SaveState ();
			context.TranslateCTM (0, borderAt);
			context.AddPath (borderPath);
			UIColor.Gray.SetColor ();
			context.SetLineWidth (1);
			
			// Device and Sim interpret the Y for the shadow differently.
			context.SetShadowWithColor (new SizeF (0, -1), 3, UIColor.DarkGray.CGColor);
			context.StrokePath ();
			
			// Clip the image to the path and paint it
			if (image != null){
				context.AddPath (borderPath);
				context.Clip ();
				image.Draw (new RectangleF (0, 0, 78, 78));
            }
			context.RestoreState ();
		}

		void UpdateButtonImage (Tweet tweet)
		{
			var image = tweet.Favorited ? on : off;
			
			buttonView.SetImage (image, UIControlState.Normal);
			buttonView.SetImage (image, UIControlState.Selected);
		}

		#region IImageUpdated implementation
		// Fake user ID to take advantage of the queue system
		static long serial = ImageStore.TempStartId*2;
		public void UpdatedImage (long id)
		{
			image = ImageStore.GetLocalProfilePicture (id);
			SetNeedsDisplay ();
		}
		#endregion
	}
}
