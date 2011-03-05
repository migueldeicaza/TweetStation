#if DEBUGIMAGE

using System;
using System.Collections.Generic;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Threading;

namespace TweetStation
{
	public partial class AppDelegate
	{
		Timer timer;
		
		void AddDebugHook ()
		{
			var b = UIButton.FromType (UIButtonType.RoundedRect);
			b.SetTitle ("Debug", UIControlState.Normal);
			b.Frame = new RectangleF (110, 22, 120, 40);
			b.AddTarget (delegate { RunDebugUi2 (); }, UIControlEvent.TouchDown);
			window.AddSubview (b);
		}
		
		void RunDebugUi2 ()
		{
			var b = new ProgressHud ("Uploading image", "Stop");
			window.BringSubviewToFront (b);
			window.AddSubview (b);
		}
		
		void RunDebugUI ()
		{
			var rect = new RectangleF (10, 20, 280, 400);
			var top = new UIView (rect){
				BackgroundColor = UIColor.FromRGBA (0, 0, 0, 100)
			};
			var button = UIButton.FromType (UIButtonType.RoundedRect);
            button.Frame = new RectangleF (10, 340, 80, 36);
			button.SetTitle ("Done", UIControlState.Normal);
			button.TouchDown += delegate {
				top.RemoveFromSuperview ();
				timer.Dispose ();
			};
			top.AddSubview (button);
			rect = new RectangleF (5, 5, 270, 350);
			var dbg = new ImageLoaderDebug (rect);
			top.AddSubview (dbg);
			window.AddSubview (top);
			
			timer = new System.Threading.Timer (x => { BeginInvokeOnMainThread (dbg.Layout); }, null, 500, 500);
		}
	}
	
	public class ImageLoaderDebug : UIView {
		static UIFont f = UIFont.SystemFontOfSize (12);
		UILabel cpending, crequest, plist, prequest;
		
		public ImageLoaderDebug (RectangleF r) : base (r)
		{
			BackgroundColor = UIColor.Clear;
			r = new RectangleF (0, 0, r.Width, 18);
			cpending = new UILabel (r) { Font = UIFont.BoldSystemFontOfSize (14), TextColor = UIColor.White, BackgroundColor = UIColor.Clear };
			crequest = new UILabel (r) { Text = "Request", Font = UIFont.BoldSystemFontOfSize (14), TextColor = UIColor.White, BackgroundColor = UIColor.Clear };
			plist = new UILabel (r) { Font = f, TextColor = UIColor.White, BackgroundColor = UIColor.Clear, Lines = 0, LineBreakMode = UILineBreakMode.WordWrap };
			prequest = new UILabel (r) { Font = f, TextColor = UIColor.White, BackgroundColor = UIColor.Clear, Lines = 0, LineBreakMode = UILineBreakMode.WordWrap };
			
			prequest.LineBreakMode = plist.LineBreakMode = UILineBreakMode.WordWrap;
			AddSubview (cpending);
			AddSubview (crequest);
			AddSubview (plist);
			AddSubview (prequest);
			
			Layout ();
		}
		
		int updates;
		public void Layout ()
		{
			List<string> pending, request;
			int loaders, reqcount;
			
			ImageStore.GetStatus (out pending, out request, out loaders, out reqcount);
			cpending.Text = String.Format ("Pending={0} req={1} loaders={2} tick={3}", pending.Count, reqcount, loaders, updates++);
			var r = Bounds;
			r.Y += 20;
			var s = "";
			foreach (var p in pending)
				s += p + "\n";
			var ss = StringSize (s, f, new SizeF (r.Width, 120), UILineBreakMode.WordWrap);
			r.Height = ss.Height;
			plist.Frame = r;
			plist.Text = s;
			
			r.Y += r.Height;
			r.Height = 18;
			crequest.Frame = r;

			r.Y = r.Bottom;
			s = "";
			foreach (var re in request)
				s += re + " ";
			ss = StringSize (s, f, new SizeF (r.Width, 120), UILineBreakMode.WordWrap);
			r.Height = ss.Height;
			prequest.Frame = r;
			prequest.Text = s;
		}
	}
}
#endif
