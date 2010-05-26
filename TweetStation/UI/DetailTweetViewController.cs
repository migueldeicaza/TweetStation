//
// DetailTweetViewController.cs:
//   Renders a full tweet, with the user profile information
//   and useful operations for it
//
// Author:
//   Miguel de Icaza (miguel@gnome.org)
//
//
using System;
using System.Drawing;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.IO;

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
				new UIViewElement (null, new DetailTweetView (detailRect, tweet, ContentHandler), false) { 
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
				replySection.Add (new UIViewElement (null, new ButtonsView (buttons, handlers), true));
			
			Root = new RootElement (tweet.Screename){
				main,
				replySection,
				new Section () {
					new TimelineRootElement (tweet.Screename, Locale.GetText ("User's timeline"), "http://api.twitter.com/1/statuses/user_timeline.json?skip_user=true&id=" + tweet.UserId, User.FromId (tweet.UserId))
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
		
		void Retweet (object sender, EventArgs args)
		{
			var sheet = new UIActionSheet (Locale.GetText ("Retweet"));
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
		static UIImage off = UIImage.FromFileUncached ("Images/star-off.png");
		static UIImage on = UIImage.FromFileUncached ("Images/star-on.png");
		const int PadY = 4;
		const int smallSize = 12;
		TweetView tweetView;
		UIButton buttonView;
		UIImageView imageView;
		
		public DetailTweetView (RectangleF rect, Tweet tweet, TweetView.TappedEvent handler) : base (rect)
		{
			var tweetRect = rect;
			if (tweet.Kind != TweetKind.Direct)
				tweetRect.Width -= 30;
			
			tweetView = new TweetView (tweetRect, tweet.Text){
				BackgroundColor = UIColor.Clear,
			};
			if (handler != null)
				tweetView.Tapped += handler;
			
			AddSubview (tweetView);
			float y = tweetView.Frame.Height + PadY;
			
			string thumbUrl;
			var picUrl = PicDetect.FindPicUrl (tweet.Text, out thumbUrl);
			if (picUrl != null){
				imageView = new UIImageView (new RectangleF (0, y, 78, 78));
				y += 90;
				//image.AddTarget (delegate { Util.MainAppDelegate.Open (}, UIControlEvent.TouchUpInside);
				AddSubview (imageView);
				ImageStore.QueueRequestForPicture (serial++, thumbUrl, this);
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
					TwitterAccount.CurrentAccount.Post (String.Format ("http://api.twitter.com/1/favorites/{0}/{1}.json", tweet.Favorited ? "create" : "destroy", tweet.Id),"");
					UpdateButtonImage (tweet);
					tweet.Replace (Database.Main);
				};
			
				AddSubview (buttonView);
			}
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
			imageView.Image = ImageStore.GetLocalProfilePicture (id);
		}
		#endregion
	}
}
