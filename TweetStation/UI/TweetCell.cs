//
// TweetCell.cs: 
//
// This shows both how to implement a custom UITableViewCell and
// how to implement a custom MonoTouch.Dialog.Element.
//
// Author:
//   Miguel de Icaza
//
using System;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.CoreGraphics;
using MonoTouch.Dialog;

namespace TweetStation
{
	// 
	// TweetCell used for the timelines.   It is relatlively light, and 
	// does not do highlighting.   This might work for the iPhone, but
	// for the iPad we probably should just use TweetViews that do the
	// highlighting of url-like things
	//
	public class TweetCell : UITableViewCell {
		// Do these as static to reuse across all instances
		const int userSize = 14;
		const int textSize = 15;
		const int timeSize = 12;
		
		const int PicSize = 48;
		const int PicXPad = 10;
		const int PicYPad = 5;
		
		const int TextLeftStart = 2 * PicXPad + PicSize;
		
		const int TextHeightPadding = 4;
		const int TextYOffset = userSize + 4;
		const int MinHeight = PicSize + 2 * PicYPad;
		const int TimeWidth = 46;
		
		static UIFont userFont = UIFont.BoldSystemFontOfSize (userSize);
		static UIFont textFont = UIFont.SystemFontOfSize (textSize);
		static UIFont timeFont = UIFont.SystemFontOfSize (timeSize);
		static UIColor timeColor = UIColor.FromRGB (147, 170, 204);
		
		Tweet tweet;
		TweetCellView tweetView;
		
		static CGGradient bottomGradient, topGradient;
		
		static TweetCell ()
		{
			using (var rgb = CGColorSpace.CreateDeviceRGB()){
				float [] colorsBottom = {
					1, 1, 1, .5f,
					0.93f, 0.93f, 0.93f, .5f
				};
				bottomGradient = new CGGradient (rgb, colorsBottom, null);
				float [] colorsTop = {
					0.93f, 0.93f, 0.93f, .5f,
					1, 1, 1, 0.5f
				};
				topGradient = new CGGradient (rgb, colorsTop, null);
			}
		}
		
		// Should never happen
		public TweetCell (IntPtr handle) : base (handle) {
			Console.WriteLine (Environment.StackTrace);
		}
		
		public class TweetCellView : UIView, IImageUpdated {
			static UIImage star = UIImage.FromFile ("Images/mini-star-on.png");	
			Tweet tweet;
			string userText;
			UIImage tweetImage, retweetImage;
			
			public TweetCellView (Tweet tweet) : base ()
			{
				Update (tweet);
				Opaque = true;
				BackgroundColor = UIColor.White;
			}

			public void Update (Tweet tweet)
			{
				this.tweet = tweet;
				userText = tweet.Retweeter == null ? tweet.Screename : tweet.Screename + "→" + tweet.Retweeter;

				
				// 
				// For fake UserIDs (returned by the search), we try looking up by screename now
				//
				var img = ImageStore.GetLocalProfilePicture (tweet.UserId);
				if (img == null)
					img = ImageStore.GetLocalProfilePicture (tweet.Screename);
				if (img == null)
					ImageStore.QueueRequestForPicture (tweet.UserId, tweet.PicUrl, this);
				else
					tweet.PicUrl = null;
				tweetImage = img == null ? ImageStore.DefaultImage : img;
				
				// If no retweet, hide our image.
				if (tweet.Retweeter != null){
					img = ImageStore.GetLocalProfilePicture (tweet.RetweeterId);
					if (img == null)
						ImageStore.QueueRequestForPicture (tweet.RetweeterId, tweet.RetweeterPicUrl, this);
					else 
						tweet.RetweeterPicUrl = null;
					
					retweetImage = img == null ? ImageStore.DefaultImage : img;
				}
				SetNeedsDisplay ();
			}
			
			public override void Draw (RectangleF rect)
			{
		        var context = UIGraphics.GetCurrentContext ();

				// Superview is the container, its superview the uitableviewcell
				bool highlighted = (Superview.Superview as UITableViewCell).Highlighted;
				UIColor textColor;
				
				var bounds = Bounds;
				var midx = bounds.Width/2;
				if (highlighted){
					textColor = UIColor.White;
				} else {
					UIColor.White.SetColor ();
					context.FillRect (bounds);

					context.DrawLinearGradient (bottomGradient, new PointF (midx, bounds.Height-17), new PointF (midx, bounds.Height), 0);
					context.DrawLinearGradient (topGradient, new PointF (midx, 1), new PointF (midx, 3), 0);
					                                   
					textColor = UIColor.Black;
				}
				
				textColor.SetColor ();
				DrawString (userText, new RectangleF (TextLeftStart, TextHeightPadding, bounds.Width-TextLeftStart-TextHeightPadding-TimeWidth, userSize), userFont);
				DrawString (tweet.Text, new RectangleF (TextLeftStart, bounds.Y + TextYOffset, bounds.Width-TextLeftStart-TextHeightPadding, bounds.Height-TextYOffset), textFont, UILineBreakMode.WordWrap);
				
				timeColor.SetColor ();
				string time = Util.FormatTime (new TimeSpan (DateTime.UtcNow.Ticks - tweet.CreatedAt));
				if (tweet.Favorited){
					using (var nss = new NSString (time)){
						var size = nss.StringSize (timeFont);
						
						star.Draw (new RectangleF (bounds.Width-6-size.Width-size.Height, TextHeightPadding, size.Height, size.Height));
					}
				}
				DrawString (time, new RectangleF (TextLeftStart, TextHeightPadding, bounds.Width-TextLeftStart-TextHeightPadding, timeSize),
				            timeFont, UILineBreakMode.Clip, UITextAlignment.Right);

				tweetImage.Draw (new RectangleF (PicXPad, PicYPad, PicSize, PicSize));
				
				if (retweetImage != null)
					retweetImage.Draw (new RectangleF (PicXPad+30, PicYPad+30, 23, 23));
			}

			void IImageUpdated.UpdatedImage (long onId)
			{
				// Discard notifications that might have been queued for an old cell
				if (tweet == null || (tweet.UserId != onId && tweet.RetweeterId != onId))
					return;
				
				// Discard the url string once the image is loaded, we wont be using it.
				if (onId == tweet.UserId){
					tweetImage = ImageStore.GetLocalProfilePicture (onId);
					tweet.PicUrl = null;
				} else {
					retweetImage = ImageStore.GetLocalProfilePicture (onId);
					tweet.RetweeterPicUrl = null;
				}
				SetNeedsDisplay ();
			}
		}
		
		// Create the UIViews that we will use here, layout happens in LayoutSubviews
		public TweetCell (UITableViewCellStyle style, NSString ident, Tweet tweet) : base (style, ident)
		{
			this.tweet = tweet;
			SelectionStyle = UITableViewCellSelectionStyle.Blue;
			
			tweetView = new TweetCellView (tweet);
			UpdateCell (tweet);
			ContentView.Add (tweetView);
		}

		// 
		// This method is called when the cell is reused to reset
		// all of the cell values
		//
		public void UpdateCell (Tweet tweet)
		{
			this.tweet = tweet;
			
			tweetView.Update (tweet);
			SetNeedsDisplay ();
		}

		public static float GetCellHeight (RectangleF bounds, Tweet tweet)
		{
			bounds.Height = 999;
			
			// Keep the same as LayoutSubviews
			bounds.X = TextLeftStart;
			bounds.Width -= TextLeftStart+TextHeightPadding;
			
			using (var nss = new NSString (tweet.Text)){
				var dim = nss.StringSize (textFont, bounds.Size, UILineBreakMode.WordWrap);
				return Math.Max (dim.Height + TextYOffset + 2*TextHeightPadding, MinHeight);
			}
		}

		public override void LayoutSubviews ()
		{
			base.LayoutSubviews ();
			
			tweetView.Frame = ContentView.Bounds;
		}
	}
	
	// 
	// A MonoTouch.Dialog.Element that renders a TweetCell
	//
	public class TweetElement : Element, IElementSizing {
		static NSString key = new NSString ("tweetelement");
		public Tweet Tweet;
		
		public TweetElement (Tweet tweet) : base (null)
		{
			Tweet = tweet;	
		}
		
		// Gets a cell on demand, reusing cells
		public override UITableViewCell GetCell (UITableView tv)
		{
			var cell = tv.DequeueReusableCell (key) as TweetCell;
			if (cell == null)
				cell = new TweetCell (UITableViewCellStyle.Default, key, Tweet);
			else
				cell.UpdateCell (Tweet);
			
			return cell;
		}
		
		public override void Selected (DialogViewController dvc, UITableView tableView, NSIndexPath path)
		{
			// For partial tweets we need to load the full tweet
			if (Tweet.UserId < 0)
				Tweet.LoadFullTweet (Tweet.Id, t => {
					if (t == null)
						return;
					
					Tweet = t;
					Activate (dvc, t);
				});
			else 
				Activate (dvc, Tweet);
		}

		void Activate (DialogViewController dvc, Tweet source)
		{
			var profile = new DetailTweetViewController (source);
			dvc.ActivateController (profile);
		}

		public override bool Matches (string text)
		{
			return (Tweet.Screename.IndexOf (text, StringComparison.CurrentCultureIgnoreCase) != -1) || 
				(Tweet.Text.IndexOf (text, StringComparison.InvariantCultureIgnoreCase) != -1) || 
				(Tweet.Retweeter != null ? Tweet.Retweeter.IndexOf (text, StringComparison.CurrentCultureIgnoreCase) != -1 : false);
		}
		
		#region IElementSizing implementation
		public float GetHeight (UITableView tableView, NSIndexPath indexPath)
		{
			return TweetCell.GetCellHeight (tableView.Bounds, Tweet);
		}
		#endregion
	}
}
