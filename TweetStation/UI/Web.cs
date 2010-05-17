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
		UIWebView webView;

		WebViewController ()
		{
			toolbar = new UIToolbar ();
			items = new UIBarButtonItem [] {
				new UIBarButtonItem (Locale.GetText ("Back"), UIBarButtonItemStyle.Bordered, (o, e) => { webView.GoBack (); }),
				new UIBarButtonItem (Locale.GetText ("Forward"), UIBarButtonItemStyle.Bordered, (o, e) => { webView.GoForward (); }),
				new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace, null),
				new UIBarButtonItem (UIBarButtonSystemItem.Refresh, (o, e) => { webView.Reload (); }),
				new UIBarButtonItem (UIBarButtonSystemItem.Stop, (o, e) => { webView.StopLoading (); })
			};
			toolbar.Items = items;
			
			View.AddSubview (toolbar);
		}

		public void SetupWeb ()
		{
			webView = new UIWebView (){
				ScalesPageToFit = true
			};
			webView.LoadStarted += delegate { Util.PushNetworkActive (); };
			webView.LoadFinished += delegate { Util.PopNetworkActive (); };
			
			//webView.SizeToFit ();
			View.AddSubview (webView);
		}
		
		public override void ViewDidDisappear (bool animated)
		{
			base.ViewDidDisappear (animated);
			webView.RemoveFromSuperview ();
			webView.Dispose ();
			webView = null;
		}
		
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			Console.WriteLine ("Frame: {0}", View.Frame);
			toolbar.Frame =  new RectangleF (0, View.Frame.Height-44, View.Frame.Width, 44);
			webView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height-44);
		}
		
		public static void OpenUrl (DialogViewController parent, string url)
		{
			UIView.BeginAnimations ("foo");
			Main.HidesBottomBarWhenPushed = true;
			Main.SetupWeb ();
			Main.webView.LoadRequest (new NSUrlRequest (new NSUrl (url)));
			parent.ActivateController (Main);
			UIView.CommitAnimations ();
		}
	}
}

