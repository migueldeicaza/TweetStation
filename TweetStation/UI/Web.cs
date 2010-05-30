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
		
		UIToolbar toolbar;
		UIBarButtonItem [] items;
		protected UIWebView WebView;

		protected WebViewController ()
		{
			toolbar = new UIToolbar () {
				AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleBottomMargin
			};
			
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
				ScalesPageToFit = true,
				MultipleTouchEnabled = true,
				AutoresizingMask = UIViewAutoresizing.FlexibleHeight|UIViewAutoresizing.FlexibleWidth
			};
			WebView.LoadStarted += delegate { Util.PushNetworkActive (); };
			WebView.LoadFinished += delegate { Util.PopNetworkActive (); };
			
			//webView.SizeToFit ();
			View.AddSubview (WebView);
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

