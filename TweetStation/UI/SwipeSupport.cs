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
// THE  SOFTWARE.

//
// TODO:
//   * Sometimes it keeps a cell selected, this creates some sort of view that
//     shows up as blue and prevents the grey background from being shown.
//
//   * Needs texture for the background image
//
//   * Needs actions hooked up
//
//   * Menu needs to be cancelled when items are added
//

#if true || SWIPE_SUPPORT

using System;
using MonoTouch.ObjCRuntime;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;
using MonoTouch.CoreAnimation;
using MonoTouch.CoreGraphics;

namespace TweetStation
{
	public partial class BaseTimelineViewController
	{
		internal class SwipeDetectingTableView : UITableView {
			BaseTimelineViewController container;
			
			public SwipeDetectingTableView (RectangleF bounds, UITableViewStyle style, BaseTimelineViewController container)
				: base (bounds, style)
			{
				this.container = container;
			}
			
			bool ignoreTouchEvents;
			PointF? touchStart;
			public override void TouchesBegan (NSSet touches, UIEvent evt)
			{
				var touch = touches.AnyObject as UITouch;
				touchStart = touch.LocationInView (this);
				
				ignoreTouchEvents = container.CancelMenu ();
				
				base.TouchesBegan (touches, evt);
			}
			
			public override void TouchesMoved (NSSet touches, UIEvent evt)
			{
				if (ignoreTouchEvents)
					return;
				
				if (touchStart != null){
					var touch = touches.AnyObject as UITouch;
					var currentPos = touch.LocationInView (this);
					var deltaX = Math.Abs (touchStart.Value.X - currentPos.X);
					var deltaY = Math.Abs (touchStart.Value.Y - currentPos.Y);
					
					if (deltaY < 5 && deltaX > 16){
						var menuPath = IndexPathForRowAtPoint (currentPos);
						var cell = CellAt (menuPath);
						
						container.OnSwipe (menuPath, cell);
						ignoreTouchEvents = true;
						touchStart = null;
						return;
					}
				}
				base.TouchesMoved (touches, evt);
			}
			
			public override void TouchesEnded (NSSet touches, UIEvent evt)
			{
				if (ignoreTouchEvents)
					return;
				
				if (container.DisableSelection)
					return;
				
				base.TouchesEnded (touches, evt);
				touchStart = null;
			}
		}
	
		static void Move (UIView view, float xoffset)
		{
			//Console.WriteLine ("    Moving {0} from {1} to {2}", view, view.Frame.X, view.Frame.X + xoffset);
			var frame = view.Frame;
			frame.Offset (xoffset, 0);
			view.Frame = frame;
		}
		
		const double delay = 0.5;
		UIView currentMenuView;
		UITableViewCell menuCell;
	
		void ShowMenu (UIView menuView, UITableViewCell cell)
		{
			HideMenu ();
			DisableSelection = true;
			var p = TableView.IndexPathForCell (cell);
			float offset = cell.ContentView.Frame.Width;
			//Console.WriteLine ("Activating swipe at {0},{1} OFFSET={2}", p.Section, p.Row, offset);

			currentMenuView = menuView;
			menuCell = cell;
			cell.ContentView.InsertSubview (menuView, 0);

			UIView.BeginAnimations ("Foo");
			UIView.SetAnimationDuration (delay);
			UIView.SetAnimationCurve (UIViewAnimationCurve.EaseIn);

			foreach (var view in cell.ContentView.Subviews){
				if (view == menuView)
					continue;
				Move (view, offset);
			}
			UIView.CommitAnimations ();
		}

		bool HideMenu ()
		{
			if (menuCell == null || currentMenuView == null)
				return false;
			
			float offset = menuCell.ContentView.Frame.Width;
			var p = TableView.IndexPathForCell (menuCell);
			Console.WriteLine ("REMOVING swite at {0},{1} OFFSET={2}", p.Section, p.Row, offset);
			
			UIView.BeginAnimations ("Foo");
			UIView.SetAnimationDuration (delay);
			UIView.SetAnimationDidStopSelector (new Selector ("animationDidStop:finished:context:"));
			UIView.SetAnimationDelegate (this);
			UIView.SetAnimationCurve (UIViewAnimationCurve.EaseInOut);			
			
			var animation = MakeBounceAnimation (Math.Abs (offset));
			
			foreach (var view in menuCell.ContentView.Subviews){
				if (view == currentMenuView)
					continue;
				
				var b = view.Bounds;
				view.Layer.Position = new PointF (b.Width/2, b.Height/2);
				view.Layer.AddAnimation (animation, "position");
			}

			UIView.CommitAnimations ();
			menuCell = null;
			DisableSelection = false;
			return true;
		}
		
		CAAnimation MakeBounceAnimation (float offset)
		{
			var animation = (CAKeyFrameAnimation) CAKeyFrameAnimation.FromKeyPath ("position.x");
			
			animation.Duration = delay;
			float left = offset/2;
			animation.Values = new NSNumber [] {
				NSNumber.FromFloat (offset),
				NSNumber.FromFloat (left-60),
				NSNumber.FromFloat (left+40),
				NSNumber.FromFloat (left-30),
				NSNumber.FromFloat (left+10),
				NSNumber.FromFloat (left-10),
				NSNumber.FromFloat (left),
			};
			
			return animation;
#endif
		}
		
		[Export ("animationDidStop:finished:context:")]
		[Preserve]
		public void HideFinished (string name, NSNumber numFinished, IntPtr context)
		{
			if (currentMenuView != null){
				currentMenuView.RemoveFromSuperview ();
				currentMenuView = null;
				
			}
		}

		internal class SwipeMenuView : UIView {
			static UIImage texture = UIImage.FromBundle ("Images/texture.png");
			static UIImage [] images = {
				UIImage.FromBundle ("Images/swipe-reply.png"),
				UIImage.FromBundle ("Images/swipe-retweet.png"),
				UIImage.FromBundle ("Images/swipe-star-off.png"),
				UIImage.FromBundle ("Images/swipe-profile.png")
			};

			BaseTimelineViewController parent;
			
			internal SwipeMenuView (BaseTimelineViewController parent, RectangleF frame) : base (frame)
			{
				this.parent = parent;
				BackgroundColor = UIColor.FromPatternImage (texture);
				var views = new CALayer [images.Length];
				
				float slotsize = frame.Width/views.Length;
				for (int i = 0; i < views.Length; i++){
					var image = images [i];
					var layer = views [i] = new CALayer ();

					
					UIGraphics.BeginImageContext (new SizeF (image.Size.Width+8, image.Size.Height+8));
					var ctx = UIGraphics.GetCurrentContext ();

					ctx.SaveState ();
					ctx.SetShadowWithColor (new SizeF (1, 1), 10, UIColor.Black.CGColor);
					image.Draw (new PointF (4, 4));
					ctx.RestoreState ();

					image.Draw (new PointF (4, 4));
					image = UIGraphics.GetImageFromCurrentImageContext ();
					layer.Contents = image.CGImage;
					UIGraphics.EndImageContext ();
					
					var alpha = (CABasicAnimation) CABasicAnimation.FromKeyPath ("opacity");
					alpha.From = new NSNumber (0);
					alpha.To = new NSNumber (1);
					alpha.BeginTime = delay/views.Length*i;
					
					var size = (CAKeyFrameAnimation) CAKeyFrameAnimation.FromKeyPath ("transform.scale");
					size.Values = new NSNumber [] {
						NSNumber.FromFloat (0.8f),
						NSNumber.FromFloat (1.2f),
						NSNumber.FromFloat (1),
					};
					
					var group = CAAnimationGroup.CreateAnimation ();
					group.Animations = new CAAnimation [] { alpha, size };
					group.Duration = delay; 
					
					layer.AddAnimation (group, "showup");					
					
					layer.Frame = new RectangleF (
						(int) (slotsize*i+image.Size.Width/2), 
						(int) (frame.Height-image.Size.Height)/2, 
						image.Size.Width, image.Size.Height);

					Layer.AddSublayer (layer);
				}
			}
			
			CALayer tracking;
			
			void StopTracking ()
			{
				if (tracking != null)
					tracking.BorderWidth = 0;
				
				tracking = null;
			}
			
			public override void TouchesBegan (NSSet touches, UIEvent evt)
			{
				base.TouchesBegan (touches, evt);
				
                var touch = touches.AnyObject as UITouch;
                var location = touch.LocationInView (this);
                
				tracking = Layer.HitTest (location);
				if (tracking == Layer)
					tracking = null;

				StopTracking ();
			}
			
			public override void TouchesEnded (NSSet touches, UIEvent evt)
			{
				base.TouchesEnded (touches, evt);
				if (tracking != null){
					// Cancel menu
					// Raise event.
				}
				
				StopTracking ();
			}
			
			public override void TouchesMoved (NSSet touches, UIEvent evt)
			{
				base.TouchesMoved (touches, evt);
				
			}
			
			public override void TouchesCancelled (NSSet touches, UIEvent evt)
			{
				base.TouchesCancelled (touches, evt);
				StopTracking ();
			}
		}
		
		public virtual void OnSwipe (NSIndexPath path, UITableViewCell cell)
		{
			var e = Root [path.Section][path.Row];
			if (e is TweetElement){
				var frame = cell.ContentView.Frame;
				
				TableView.ScrollEnabled = false;
				var menu = new SwipeMenuView (this, frame);
				ShowMenu (menu, cell);
			}
		}
		
		public virtual bool CancelMenu ()
		{
			if (HideMenu ()){
				TableView.ScrollEnabled = true;
				return true;
			}
			return false;
		}
		
		public override UITableView MakeTableView (RectangleF bounds, UITableViewStyle style)
		{
			return new SwipeDetectingTableView (bounds, style, this);
		}
	}
}
