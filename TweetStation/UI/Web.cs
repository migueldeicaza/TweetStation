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
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using System.Drawing;

namespace TweetStation
{
	public class WebViewController : UIViewController {
		static WebViewController Main = new WebViewController ();
		
		UIToolbar topBar;
		UIToolbar toolbar;
		UIBarButtonItem backButton, forwardButton, stopButton, refreshButton;
		UILabel title;
		protected UIWebView WebView;

		protected WebViewController ()
		{
			var fixedSpace = new UIBarButtonItem (UIBarButtonSystemItem.FixedSpace, null);
			var flexibleSpace = new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace, null);

			toolbar = new UIToolbar ();
			topBar = new UIToolbar ();
			
			title = new UILabel (new RectangleF (0, 0, 80, 30)){
				BackgroundColor = UIColor.Clear,
				AdjustsFontSizeToFitWidth = true,
				Font = UIFont.BoldSystemFontOfSize (22),
				MinimumFontSize = 14,
				TextColor = UIColor.White,
				ShadowColor = UIColor.FromRGB (64, 74, 87),
				ShadowOffset = new SizeF (0, -1)
			};
			
			topBar.Items = new UIBarButtonItem []  {
				new UIBarButtonItem (title),
				flexibleSpace,
				new UIBarButtonItem (Locale.GetText ("Close"), UIBarButtonItemStyle.Bordered, (o, e) => { DismissModalViewControllerAnimated (true);} )
			};
			
			backButton = new UIBarButtonItem (UIImage.FromFile ("Images/back.png"), UIBarButtonItemStyle.Plain, (o, e) => { WebView.GoBack (); });
			forwardButton = new UIBarButtonItem (UIImage.FromFile ("Images/forward.png"), UIBarButtonItemStyle.Plain, (o, e) => { WebView.GoForward (); });
			refreshButton = new UIBarButtonItem (UIBarButtonSystemItem.Refresh, (o, e) => { WebView.Reload (); });
			stopButton = new UIBarButtonItem (UIBarButtonSystemItem.Stop, (o, e) => { WebView.StopLoading (); });

			toolbar.Items = new UIBarButtonItem [] { backButton,	fixedSpace, forwardButton, flexibleSpace, stopButton, refreshButton };

			View.AddSubview (topBar);
			View.AddSubview (toolbar);
		}

		public void SetupWeb ()
		{
			WebView = new UIWebView (){
				ScalesPageToFit = true,
				MultipleTouchEnabled = true,
				AutoresizingMask = UIViewAutoresizing.FlexibleHeight|UIViewAutoresizing.FlexibleWidth
			};
			WebView.LoadStarted += delegate { 
				stopButton.Enabled = true;
				refreshButton.Enabled = false;
				backButton.Enabled = true;
				Util.PushNetworkActive (); 
			};
			WebView.LoadFinished += delegate {
				stopButton.Enabled = false;
				refreshButton.Enabled = true;
				Util.PopNetworkActive (); 
				backButton.Enabled = WebView.CanGoBack;
				forwardButton.Enabled = WebView.CanGoForward;
				
				title.Text = WebView.EvaluateJavascript ("document.title");
			};
			
			View.AddSubview (WebView);
			backButton.Enabled = false;
			forwardButton.Enabled = false;
		}
		
		public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
		{
			return true;
		}
		
		public override void ViewDidDisappear (bool animated)
		{
			base.ViewDidDisappear (animated);
			if (WebView != null){
				WebView.RemoveFromSuperview ();
				WebView.Dispose ();
				WebView = null;
			}
		}

		void LayoutViews ()
		{
		}
		
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
		}

		public override void DidRotate (UIInterfaceOrientation fromInterfaceOrientation)
		{
			base.DidRotate (fromInterfaceOrientation);
			var sbounds = View.Bounds;

			toolbar.Frame =  new RectangleF (0, sbounds.Height-44, sbounds.Width, 44);
			WebView.Frame = new RectangleF (0, 44, sbounds.Width, sbounds.Height-88);
			topBar.Frame = new RectangleF (0, 0, sbounds.Width, 44);
			title.Frame = new RectangleF (0, 0, sbounds.Width-80, 38);
		}
		
		public static void OpenUrl (DialogViewController parent, string url)
		{
			UIView.BeginAnimations ("foo");
			Main.HidesBottomBarWhenPushed = true;
			Main.SetupWeb ();
			Main.WebView.LoadRequest (new NSUrlRequest (new NSUrl (url)));
			
			parent.PresentModalViewController (Main, true);

			UIView.CommitAnimations ();
		}
		
		public static void OpenHtmlString (DialogViewController parent, string htmlString, NSUrl baseUrl)
		{
			UIView.BeginAnimations ("foo");
			Main.HidesBottomBarWhenPushed = true;
			Main.SetupWeb ();
			
			Main.WebView.LoadHtmlString (htmlString, baseUrl);
			parent.ActivateController (Main);
			UIView.CommitAnimations ();
		}
		
		public static void OpenHtmlString (DialogViewController parent, string htmlString)
		{
			OpenHtmlString (parent, htmlString, new NSUrl (Util.BaseDir, true));
		}
	}
}

