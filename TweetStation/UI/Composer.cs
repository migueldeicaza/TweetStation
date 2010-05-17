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

namespace TweetStation
{
	public class ComposerView : UIView {
		const UIBarButtonItemStyle style = UIBarButtonItemStyle.Bordered;
		internal UITextView textView;
		Composer composer;
		UIToolbar toolbar;
		UILabel charsLeft;
		internal UIBarButtonItem GpsButtonItem;
		
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
				new UIBarButtonItem (UIBarButtonSystemItem.Camera, null, null) { Style = style },
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
			charsLeft.Frame = new RectangleF (160, bounds.Height-44, 50, 44);
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
			sendItem = new UIBarButtonItem ("Send", UIBarButtonItemStyle.Plain, Post);
			navItem.RightBarButtonItem = sendItem;

			navigationBar.PushNavigationItem (navItem, false);
			
			// Composer
			composerView = new ComposerView (ComputeComposerSize (RectangleF.Empty), this);
			
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
		
		void Post (object sender, EventArgs a)
		{
			var content = new StringBuilder ();
			var account = TwitterAccount.CurrentAccount;
			
			if (directRecipient == null){
				content.AppendFormat ("status={0}&source=TweetStation", HttpUtility.UrlEncode (composerView.Text));
				AppendLocation (content);
				if (InReplyTo != 0)
					content.AppendFormat ("&in_reply_to_status_id={0}", InReplyTo);	
				account.Post ("http://twitter.com/statuses/update.json", content.ToString ());
			} else {
				content.AppendFormat ("text={0}&user={1}", HttpUtility.UrlEncode (composerView.Text), HttpUtility.UrlEncode (directRecipient));
				AppendLocation (content);
				account.Post ("http://twitter.com/direct_messages/new.json", content.ToString ());
			}
			CloseComposer (sender, a);
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
