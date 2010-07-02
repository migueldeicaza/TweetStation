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
#if true || SWIPE_SUPPORT

using System;
using MonoTouch.ObjCRuntime;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;
using MonoTouch.CoreAnimation;

namespace TweetStation
{
	public partial class BaseTimelineViewController
	{
		class SwipeDetectingTableView : UITableView {
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
				
#if false
				NSNumber.FromFloat (-40),
				NSNumber.FromFloat (100),
				NSNumber.FromFloat (-20),
				NSNumber.FromFloat (20),
				NSNumber.FromFloat (0),
#endif
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
		
		public virtual void OnSwipe (NSIndexPath path, UITableViewCell cell)
		{
			var e = Root [path.Section][path.Row];
			if (e is TweetElement){
				var frame = cell.ContentView.Frame;
				
				TableView.ScrollEnabled = false;
				var button = UIButton.FromType (UIButtonType.RoundedRect);
				button.Frame = new RectangleF (0, 0, frame.Width, frame.Height);
				
				//Console.WriteLine ("Swipe detected!");
				ShowMenu (button, cell);
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
