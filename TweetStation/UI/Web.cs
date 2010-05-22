//
//
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using System.Drawing;

namespace TweetStation
{
	public class WebViewController : UIViewController {
		static WebViewController Main = new WebViewController ();
		
		UIToolbar toolbar;
		UIBarButtonItem [] items;
		protected UIWebView WebView;

		protected WebViewController ()
		{
			toolbar = new UIToolbar ();
			items = new UIBarButtonItem [] {
				new UIBarButtonItem (Locale.GetText ("Back"), UIBarButtonItemStyle.Bordered, (o, e) => { WebView.GoBack (); }),
				new UIBarButtonItem (Locale.GetText ("Forward"), UIBarButtonItemStyle.Bordered, (o, e) => { WebView.GoForward (); }),
				new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace, null),
				new UIBarButtonItem (UIBarButtonSystemItem.Refresh, (o, e) => { WebView.Reload (); }),
				new UIBarButtonItem (UIBarButtonSystemItem.Stop, (o, e) => { WebView.StopLoading (); })
			};
			toolbar.Items = items;
			
			View.AddSubview (toolbar);
		}

		public void SetupWeb ()
		{
			WebView = new UIWebView (){
				ScalesPageToFit = true
			};
			WebView.LoadStarted += delegate { Util.PushNetworkActive (); };
			WebView.LoadFinished += delegate { Util.PopNetworkActive (); };
			
			//webView.SizeToFit ();
			View.AddSubview (WebView);
		}
		
		public override void ViewDidDisappear (bool animated)
		{
			base.ViewDidDisappear (animated);
			WebView.RemoveFromSuperview ();
			WebView.Dispose ();
			WebView = null;
		}
		
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			toolbar.Frame =  new RectangleF (0, View.Frame.Height-44, View.Frame.Width, 44);
			WebView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height-44);
		}
		
		public static void OpenUrl (DialogViewController parent, string url)
		{
			UIView.BeginAnimations ("foo");
			Main.HidesBottomBarWhenPushed = true;
			Main.SetupWeb ();
			Main.WebView.LoadRequest (new NSUrlRequest (new NSUrl (url)));
			parent.ActivateController (Main);
			UIView.CommitAnimations ();
		}
	}
}

