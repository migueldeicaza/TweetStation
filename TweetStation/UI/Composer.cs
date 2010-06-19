// Composer.cs:
//    Views and ViewControllers for composing messages
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
using System.Drawing;
using System.Linq;
using System.Text;
using System.Web;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.CoreLocation;
using SQLite;
using System.IO;
using System.Net;
using MonoTouch.AVFoundation;
using System.Text.RegularExpressions;
using System.Threading;

namespace TweetStation
{
	public class ComposerView : UIView {
		const UIBarButtonItemStyle style = UIBarButtonItemStyle.Bordered;
		internal UITextView textView;
		Composer composer;
		UIToolbar toolbar;
		UILabel charsLeft;
		internal UIBarButtonItem GpsButtonItem, ShrinkItem;
		public event NSAction LookupUserRequested;
		public bool justShrank;
		
		public ComposerView (RectangleF bounds, Composer composer, EventHandler cameraTapped) : base (bounds)
		{
			this.composer = composer;
			textView = new UITextView (RectangleF.Empty) {
				Font = UIFont.SystemFontOfSize (18)
			};
			textView.Changed += HandleTextViewChanged;

			charsLeft = new UILabel (RectangleF.Empty) { 
				Text = "140", 
				TextColor = UIColor.White,
				BackgroundColor = UIColor.Clear,
				TextAlignment = UITextAlignment.Right
			};

			toolbar = new UIToolbar (RectangleF.Empty);
			GpsButtonItem = new UIBarButtonItem (UIImage.FromFile ("Images/gps.png"), style, InsertGeo);
			ShrinkItem = new UIBarButtonItem (UIImage.FromFile ("Images/arrows.png"), style, OnShrinkTapped);
			
			toolbar.SetItems (new UIBarButtonItem [] {
				new UIBarButtonItem (UIBarButtonSystemItem.Trash, delegate { textView.Text = ""; } ) { Style = style },
				new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace, null),
				ShrinkItem,
				new UIBarButtonItem (UIBarButtonSystemItem.Search, delegate { if (LookupUserRequested != null) LookupUserRequested (); }) { Style = style },
				new UIBarButtonItem (UIBarButtonSystemItem.Camera, cameraTapped) { Style = style },
				GpsButtonItem }, false);	

			AddSubview (toolbar);
			AddSubview (textView);
			AddSubview (charsLeft);
		}
		
		void HandleTextViewChanged (object sender, EventArgs e)
		{
			string text = textView.Text;
			
			var enabled = composer.sendItem.Enabled;
			if (enabled ^ (text.Length != 0))
			    composer.sendItem.Enabled = !enabled;
			
			var left = 140-text.Length;
			if (left < 0)
				charsLeft.TextColor = UIColor.Red;
			else
				charsLeft.TextColor = UIColor.White;
			
			charsLeft.Text = (140-text.Length).ToString ();
		}
		
		internal void OnShrinkTapped (object sender, EventArgs args)
		{
			// Double tapping on the shrink link removes bowels, idea from Nat.
			// you -> u
			// vowels removed
			// and -> &
			if (justShrank){
				var copy = Regex.Replace (textView.Text, "\\band\\b", "&", RegexOptions.IgnoreCase);
				copy = Regex.Replace (copy, "\\byou\\b", "\u6666", RegexOptions.IgnoreCase);
				copy = Regex.Replace (copy, "\\B[aeiou]\\B", "");
				copy = copy.Replace ("\u6666", " u ");
				textView.Text = copy;
				
				// Hack because the text changed event is not raised
				// synchronously, but after the UI pumps for events
				justShrank = false;
				return;
			}
			
			var words = textView.Text.Split (new char [] { ' '}, StringSplitOptions.RemoveEmptyEntries);
			
			foreach (var word in words)
				if (word.StartsWith ("http://")){
					ShrinkUrls (words);
					break;
				}
			
			textView.Text = String.Join (" ", words);
			justShrank = true;
		}

		// Need HUD display here
		void ShrinkUrls (string [] words)
		{
			var hud = new LoadingHUDView (Locale.GetText ("Shrinking"));
			this.AddSubview (hud);
			hud.StartAnimating ();
			
			var wc = new WebClient ();
			for (int i = 0; i < words.Length; i++){
				if (!words [i].StartsWith ("http://"))
				    continue;
				    
				try {
					words [i] = wc.DownloadString (new Uri ("http://is.gd/api.php?longurl=" + HttpUtility.UrlEncode (words [i])));
				} catch {
				}
			}
			hud.StopAnimating ();
			hud.RemoveFromSuperview ();
			hud = null;
		}
		
		internal void InsertGeo (object sender, EventArgs args)
		{
			GpsButtonItem.Enabled = false;
			Util.RequestLocation (newLocation => {
				composer.location = newLocation;
				GpsButtonItem.Enabled = true;
			});
		}
		
		internal void Reset (string text)
		{
			textView.Text = text;
			justShrank = false;
			HandleTextViewChanged (null, null);
		}
		
		public override void LayoutSubviews ()
		{
			Resize (Bounds);
		}
		
		void Resize (RectangleF bounds)
		{
			textView.Frame = new RectangleF (0, 0, bounds.Width, bounds.Height-44);
			toolbar.Frame = new RectangleF (0, bounds.Height-44, bounds.Width, 44);
			charsLeft.Frame = new RectangleF (64, bounds.Height-44, 50, 44);
		}
		
		public string Text { 
			get {
				return textView.Text;
			}
			set {
				textView.Text = value;
			}
		}
	}
	
	/// <summary>
	///   Composer is a singleton that is shared through the lifetime of the app,
	///   the public methods in this class reset the values of the composer on 
	///   each invocation.
	/// </summary>
	public class Composer : UIViewController
	{
		ComposerView composerView;
		UINavigationBar navigationBar;
		UINavigationItem navItem;
		internal UIBarButtonItem sendItem;
		UIViewController previousController;
		long InReplyTo;
		string directRecipient;
		internal CLLocation location;
		AudioPlay player;
		LoadingHUDView hud;
		bool FromLibrary;
		UIImage Picture;
		
		public static readonly Composer Main = new Composer ();
		
		Composer () : base (null, null)
		{
			// Navigation Bar
			navigationBar = new UINavigationBar (new RectangleF (0, 0, 320, 44));
			navItem = new UINavigationItem ("");
			var close = new UIBarButtonItem ("Close", UIBarButtonItemStyle.Plain, CloseComposer);
			navItem.LeftBarButtonItem = close;
			sendItem = new UIBarButtonItem ("Send", UIBarButtonItemStyle.Plain, PostCallback);
			navItem.RightBarButtonItem = sendItem;

			navigationBar.PushNavigationItem (navItem, false);
			
			// Composer
			composerView = new ComposerView (ComputeComposerSize (RectangleF.Empty), this, CameraTapped);
			composerView.LookupUserRequested += delegate {
				PresentModalViewController (new UserSelector (userName => {
					composerView.Text += ("@" + userName + " ");
				}), true);
			};
			
			// Add the views
			NSNotificationCenter.DefaultCenter.AddObserver ("UIKeyboardWillShowNotification", KeyboardWillShow);

			View.AddSubview (composerView);
			View.AddSubview (navigationBar);
		}

		void CameraTapped (object sender, EventArgs args)
		{
			if (Picture == null){
				TakePicture ();
				return;
			}
			
			var sheet = Util.GetSheet (Locale.GetText ("Tweet already contains a picture"));
			sheet.AddButton (Locale.GetText ("Discard Picture"));
			sheet.AddButton (Locale.GetText ("Pick New Picture"));
			sheet.AddButton (Locale.GetText ("Cancel"));
			
			sheet.CancelButtonIndex = 2;
			sheet.Clicked += delegate(object ss, UIButtonEventArgs e) {
				if (e.ButtonIndex == 2)
					return;
				
				if (e.ButtonIndex == 0)
					Picture = null;
				else
					TakePicture ();
			};
			sheet.ShowInView (Util.MainAppDelegate.MainView);

		}
		
		void TakePicture ()
		{
			FromLibrary = true;
			if (!UIImagePickerController.IsSourceTypeAvailable (UIImagePickerControllerSourceType.Camera)){
				Camera.SelectPicture (this, PictureSelected);
				return;
			}
			
			var sheet = Util.GetSheet ("");
			sheet.AddButton (Locale.GetText ("Take a photo or video"));
			sheet.AddButton (Locale.GetText ("From Album"));
			sheet.AddButton (Locale.GetText ("Cancel"));
			
			sheet.CancelButtonIndex = 2;
			sheet.Clicked += delegate(object sender, UIButtonEventArgs e) {
				if (e.ButtonIndex == 2)
					return;
				
				if (e.ButtonIndex == 0){
					FromLibrary = false;
					Camera.TakePicture (this, PictureSelected);
				} else
					Camera.SelectPicture (this, PictureSelected);
			};
			sheet.ShowInView (Util.MainAppDelegate.MainView);
		}

		UIImage Scale (UIImage image, SizeF size)
		{
			UIGraphics.BeginImageContext (size);
			image.Draw (new RectangleF (new PointF (0, 0), size));
			var ret = UIGraphics.GetImageFromCurrentImageContext ();
			UIGraphics.EndImageContext ();
			return ret;
		}
		
		void PictureSelected (NSDictionary pictureDict)
		{
			int level = Util.Defaults.IntForKey ("sizeCompression");
			
			if ((pictureDict [UIImagePickerController.MediaType] as NSString) == "public.image"){
				Picture = pictureDict [UIImagePickerController.EditedImage] as UIImage;
				if (Picture == null)
					Picture = pictureDict [UIImagePickerController.OriginalImage] as UIImage;
				
				// Save a copy of the original picture
				if (!FromLibrary){
					Picture.SaveToPhotosAlbum (delegate {
						// ignore errors
					});
				}
				
				var size = Picture.Size;
				float maxWidth;
				switch (level){
				case 0:
					maxWidth = 640;
					break;
				case 1:
					maxWidth = 1024;
					break;
				default:
					maxWidth = size.Width;
					break;
				}

				var hud = new LoadingHUDView (Locale.GetText ("Image"), "Compressing");
				View.AddSubview (hud);
				hud.StartAnimating ();
				
				// Show the UI, and on a callback, do the scaling, so the user gets an animation
				NSTimer.CreateScheduledTimer (TimeSpan.FromSeconds (0), delegate {
					if (size.Width > maxWidth || size.Height > maxWidth)
						Picture = Scale (Picture, new SizeF (maxWidth, maxWidth*size.Height/size.Width));
					hud.StopAnimating ();
					hud.RemoveFromSuperview ();
				});
			} else {
				//NSUrl movieUrl = pictureDict [UIImagePickerController.MediaURL] as NSUrl;
				
				// Future use, when we find a video host that does not require your Twitter login/password
			}
			
			pictureDict.Dispose ();
		}
		
		public void ReleaseResources ()
		{
		}
		
		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();			
		}

		public void ResetComposer (string caption, string initialText)
		{
			composerView.Reset (initialText);
			InReplyTo = 0;
			directRecipient = null;
			location = null;
			composerView.GpsButtonItem.Enabled = true;
			navItem.Title = caption;
		}
		
		void CloseComposer (object sender, EventArgs a)
		{
			if (Picture != null){
				Picture.Dispose ();
				Picture = null;
			}
			
			sendItem.Enabled = true;
			previousController.DismissModalViewControllerAnimated (true);
			if (player != null)
				player.Stop ();
		}
		
		void AppendLocation (StringBuilder content)
		{
			if (location == null)
				return;

			// TODO: check if geo_enabled is set for the user, if not, open a browser to have the user change this.
			content.AppendFormat ("&lat={0}&long={1}", location.Coordinate.Latitude, location.Coordinate.Longitude);
		}
		
		void PostCallback (object sender, EventArgs a)
		{
			sendItem.Enabled = false;
			
			if (Picture == null){
				Post ();
				return;
			}

			hud = new LoadingHUDView (Locale.GetText ("Image"), "Uploading");
			View.AddSubview (hud);
			hud.StartAnimating ();
			
			var jpeg = Picture.AsJPEG ();
			Stream stream;
			unsafe { stream = new UnmanagedMemoryStream ((byte*) jpeg.Bytes, jpeg.Length); }
			
			ThreadPool.QueueUserWorkItem (delegate {
				TwitterAccount.CurrentAccount.UploadPicture (stream, PicUploadComplete);
				
				// This captures the variable and handle of jpeg, and then we clear it
				// to release it
				jpeg = null;
			});
		}
		
		UIAlertView alert;
		void PicUploadComplete (string name)
		{
			hud.StopAnimating ();
			hud.RemoveFromSuperview ();
			hud = null;
			
			if (name == null){
				alert = new UIAlertView (Locale.GetText ("Error"), 
	                Locale.GetText ("There was an error uploading the media, do you want to post without it?"), null, 
                    Locale.GetText ("Cancel Post"), Locale.GetText ("Post"));
				
				alert.Clicked += delegate(object sender, UIButtonEventArgs e) {
					if (e.ButtonIndex == 1)
						Post ();
				};
				alert.Show ();
			} else {
				var text = composerView.Text.Trim ();
				if (text.Length + name.Length > 140){
					alert = new UIAlertView ("Error",
						Locale.GetText ("Message is too long"), null, null, "Ok");
					alert.Show ();
				} else {
					text = text + " " + name;
					if (text.Length > 140)
						text = text.Trim ();
					composerView.Text = text;
					Post ();
				}
			}
		}
		
		void Post ()
		{
			var content = new StringBuilder ();
			var account = TwitterAccount.CurrentAccount;
			
			if (directRecipient == null){
				content.AppendFormat ("status={0}", OAuth.PercentEncode (composerView.Text));
				AppendLocation (content);
				if (InReplyTo != 0)
					content.AppendFormat ("&in_reply_to_status_id={0}", InReplyTo);	
				account.Post ("http://twitter.com/statuses/update.json", content.ToString ());
			} else {
				content.AppendFormat ("text={0}&user={1}", OAuth.PercentEncode (composerView.Text), OAuth.PercentEncode (directRecipient));
				AppendLocation (content);
				account.Post ("http://twitter.com/direct_messages/new.json", content.ToString ());
			}
			CloseComposer (this, EventArgs.Empty);
		}
		
		void KeyboardWillShow (NSNotification notification)
		{
			var kbdBounds = (notification.UserInfo.ObjectForKey (UIKeyboard.BoundsUserInfoKey) as NSValue).RectangleFValue;
			
			composerView.Frame = ComputeComposerSize (kbdBounds);
		}

		RectangleF ComputeComposerSize (RectangleF kbdBounds)
		{
			var view = View.Bounds;
			var nav = navigationBar.Bounds;

			return new RectangleF (0, nav.Height, view.Width, view.Height-kbdBounds.Height-nav.Height);
		}
		
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			composerView.textView.BecomeFirstResponder ();
		}
		
		void Activate (UIViewController parent)
		{
			previousController = parent;
			composerView.textView.BecomeFirstResponder ();
			parent.PresentModalViewController (this, true);
			
			if (Util.Defaults.IntForKey ("disableMusic") != 0)
				return;
			
			try {
				if (player == null)
					player = new AudioPlay ("Audio/composeaudio.mp3");
				player.Play ();
			} catch (Exception e){
				Console.WriteLine (e);
			}
		}
		
		public void NewTweet (UIViewController parent)
		{
			NewTweet (parent, "");
		}
		
		public void NewTweet (UIViewController parent, string initialText)
		{
			ResetComposer (Locale.GetText ("New Tweet"), initialText);
			
			Activate (parent);
		}

		public void ReplyTo (UIViewController parent, Tweet source, bool replyAll)
		{
			ResetComposer (Locale.GetText ("Reply Tweet"), replyAll ? source.GetRecipients () : '@' + source.Screename + ' ');
			InReplyTo = source.Id;
			directRecipient = null;
			
			Activate (parent);
		}
		
		public void ReplyTo (UIViewController parent, Tweet source, string recipient)
		{
			ResetComposer (Locale.GetText ("Reply Tweet"), recipient	);
			InReplyTo = source.Id;
			directRecipient = null;
			
			Activate (parent);
		}

		public void Quote (UIViewController parent, Tweet source)
		{
			ResetComposer (Locale.GetText ("Quote"), "RT @" + source.Screename + " " + source.Text);
			
			Activate (parent);
		}
		
		public void Direct (UIViewController parent, string username)
		{
			ResetComposer (username == "" ? Locale.GetText ("Direct message") : Locale.Format ("Direct to {0}", username), "");
			directRecipient = username;
			
			Activate (parent);
		}
	}
	
	// Does anyone really use drafts? 
	public class Draft {
		static bool inited;
		
		static void Init ()
		{
			if (inited)
				return;
			inited = true;
			lock (Database.Main)
				Database.Main.CreateTable<Draft> ();
		}
		
		[PrimaryKey]
		public int Id { get; set; }
		public long AccountId { get; set; }
		public string Recipient { get; set; }
		public long InReplyTo { get; set; }
		public bool DirectMessage { get; set; }
		public string Message { get; set; }
	}
}
