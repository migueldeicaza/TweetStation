// Composer.cs:
//    Views and ViewControllers for composing messages
//
// Author:
//    Miguel de Icaza (miguel@gnome.org)
//
using System;
using System.Drawing;
using System.Text;
using System.Web;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.CoreLocation;
using SQLite;
using System.IO;

namespace TweetStation
{
	public class ComposerView : UIView {
		const UIBarButtonItemStyle style = UIBarButtonItemStyle.Bordered;
		internal UITextView textView;
		Composer composer;
		UIToolbar toolbar;
		UILabel charsLeft;
		internal UIBarButtonItem GpsButtonItem;
		public event NSAction LookupUserRequested;
		public NSDictionary PictureDict;
		
		public ComposerView (RectangleF bounds, Composer composer) : base (bounds)
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
			
			toolbar.SetItems (new UIBarButtonItem [] {
				new UIBarButtonItem (UIBarButtonSystemItem.Trash, delegate { textView.Text = ""; } ) { Style = style },
				new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace, null),
				new UIBarButtonItem (UIBarButtonSystemItem.Search, delegate { if (LookupUserRequested != null) LookupUserRequested (); }) { Style = style },
				new UIBarButtonItem (UIBarButtonSystemItem.Camera, delegate { TakePicture (); } ) { Style = style },
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
		
		internal void InsertGeo (object sender, EventArgs args)
		{
			GpsButtonItem.Enabled = false;
			composer.RequestLocation ();
		}
		
		internal void GeoDone ()
		{
			GpsButtonItem.Enabled = true;
		}
		
		internal void Reset (string text)
		{
			textView.Text = text;
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
			charsLeft.Frame = new RectangleF (118, bounds.Height-44, 50, 44);
		}
		
		public string Text { 
			get {
				return textView.Text;
			}
			set {
				textView.Text = value;
			}
		}
		
		void TakePicture ()
		{
			if (!UIImagePickerController.IsSourceTypeAvailable (UIImagePickerControllerSourceType.Camera)){
				Camera.SelectPicture (composer, PictureSelected);
				return;
			}
			
			var sheet = new UIActionSheet ("");
			sheet.AddButton (Locale.GetText ("Take a photo or video"));
			sheet.AddButton (Locale.GetText ("From Album"));
			sheet.AddButton (Locale.GetText ("Cancel"));
			
			sheet.CancelButtonIndex = 2;
			sheet.Clicked += delegate(object sender, UIButtonEventArgs e) {
				if (e.ButtonIndex == 2)
					return;
				
				if (e.ButtonIndex == 0)
					Camera.TakePicture (composer, PictureSelected);
				else
					Camera.SelectPicture (composer, PictureSelected);
			};
			sheet.ShowInView (Util.MainAppDelegate.MainView);
		}
		
		void PictureSelected (NSDictionary dict)
		{
			PictureDict = dict;
		}
		
		public void ReleaseResources ()
		{
			if (PictureDict == null)
				return;
			PictureDict.Dispose ();
			PictureDict = null;
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
		CLLocationManager locationManager;
		CLLocation location;
		
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
			composerView = new ComposerView (ComputeComposerSize (RectangleF.Empty), this);
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

		public class MyCLLocationManagerDelegate : CLLocationManagerDelegate {
			Composer parent;
			public MyCLLocationManagerDelegate (Composer parent)
			{
				this.parent = parent;
			}
			
			public override void UpdatedLocation (CLLocationManager manager, CLLocation newLocation, CLLocation oldLocation)
			{
				parent.location = newLocation;
				parent.composerView.GeoDone ();
			}
		}
		
		internal void RequestLocation ()
		{
			if (locationManager == null){
				locationManager = new CLLocationManager () {
					DesiredAccuracy = CLLocation.AccuracyBest,
					Delegate = new MyCLLocationManagerDelegate (this),
					DistanceFilter = 1000f
				};
			}
			if (locationManager.LocationServicesEnabled)
				locationManager.StartUpdatingLocation ();
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
			if (locationManager != null)
				locationManager.StartUpdatingLocation ();
			
			previousController.DismissModalViewControllerAnimated (true);
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
			var pictureDict = composerView.PictureDict;
			if (pictureDict == null){
				Post ();
				return;
			}
			
			if ((pictureDict [UIImagePickerController.MediaType] as NSString) == "public.image"){
				var img = pictureDict [UIImagePickerController.EditedImage] as UIImage;
				if (img == null)
					img = pictureDict [UIImagePickerController.OriginalImage] as UIImage;
				
				var jpeg = img.AsJPEG ();
				Stream stream;
				unsafe { stream = new UnmanagedMemoryStream ((byte*) jpeg.Bytes, jpeg.Length); }
				TwitterAccount.CurrentAccount.UploadPicture (stream, PicUploadComplete);
			} else {
				//NSUrl movieUrl = pictureDict [UIImagePickerController.MediaURL] as NSUrl;
				
				// Future use, when we find a video host that does not require your Twitter login/password
			}
		}
		
		void PicUploadComplete (string name)
		{
			if (name == null){
				var alert = new UIAlertView (Locale.GetText ("Error"), 
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
					var alert = new UIAlertView ("Error",
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
		
		void Activate (UIViewController parent)
		{
			previousController = parent;
			composerView.textView.BecomeFirstResponder ();
			parent.PresentModalViewController (this, true);
		}
		
		public void NewTweet (UIViewController parent)
		{
			ResetComposer (Locale.GetText ("New Tweet"), "");
			
			Activate (parent);
		}
		
		public void ReplyTo (UIViewController parent, Tweet source)
		{
			ResetComposer (Locale.GetText ("Reply Tweet"), "@" + source.Screename + " ");
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
	
	public class Draft {
		static bool inited;
		
		static void Init ()
		{
			if (inited)
				return;
			inited = true;
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
