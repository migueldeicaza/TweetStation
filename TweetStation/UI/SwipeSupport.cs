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
#if SWIPE_SUPPORT

using System;
using MonoTouch.ObjCRuntime;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;

namespace TweetStation
{
	public partial class BaseTimelineViewController
	{
			class SwipeDetectingTableView : UITableView {
			BaseTimelineViewController container;
			NSIndexPath menuPath;
			
			public SwipeDetectingTableView (RectangleF bounds, UITableViewStyle style, BaseTimelineViewController container)
				: base (bounds, style)
			{
				this.container = container;
			}
			
			PointF touchStart;
			public override void TouchesBegan (NSSet touches, UIEvent evt)
			{
				var touch = touches.AnyObject as UITouch;
				touchStart = touch.LocationInView (this);
				
				if (menuPath != null){
					var path = IndexPathForRowAtPoint (touchStart);
					
					if (path.Row != menuPath.Row || path.Section != menuPath.Section){
						container.CancelMenu ();
					}
				}
				
				base.TouchesBegan (touches, evt);
			}
			
			public override void TouchesMoved (NSSet touches, UIEvent evt)
			{
				var touch = touches.AnyObject as UITouch;
				var currentPos = touch.LocationInView (this);
				var deltaX = Math.Abs (touchStart.X - currentPos.X);
				var deltaY = Math.Abs (touchStart.Y - currentPos.Y);
				
				if (deltaY < 5 && deltaX > 16){
					menuPath = IndexPathForRowAtPoint (currentPos);
					var cell = CellAt (menuPath);
					
					container.OnSwipe (menuPath, cell);
					touchStart = new PointF (-100, -100);
					return;
				}
				base.TouchesMoved (touches, evt);
			}
			
			public override void TouchesEnded (NSSet touches, UIEvent evt)
			{
				base.TouchesEnded (touches, evt);
			}
		}
	
		static void Move (UIView view, float xoffset)
		{
			var frame = view.Frame;
			frame.Offset (xoffset, 0);
			view.Frame = frame;
		}
		
		UIView currentMenuView;
		UITableViewCell currentCell;
	
		void ShowMenu (UIView menuView, UITableViewCell cell)
		{
			HideMenu ();
			float offset = cell.ContentView.Frame.Width;

			currentMenuView = menuView;
			currentCell = cell;
			Move (menuView, -offset);
			cell.ContentView.AddSubview (menuView);
			
			UIView.BeginAnimations ("");
			UIView.SetAnimationDuration (0.4);
			UIView.SetAnimationCurve (UIViewAnimationCurve.EaseInOut);

			foreach (var view in cell.ContentView.Subviews){
				if (view == menuView)
					continue;
				Move (view, offset);
			}
			menuView.Frame = cell.ContentView.Frame;
			UIView.CommitAnimations ();
		}

		void HideMenu ()
		{
			if (currentMenuView == null)
				return;
			float offset = currentCell.ContentView.Frame.Width;
			UIView.BeginAnimations (null);
			UIView.SetAnimationDuration (0.4);
			UIView.SetAnimationDidStopSelector (new Selector ("hideFinished"));
			UIView.SetAnimationDelegate (this);
			UIView.SetAnimationCurve (UIViewAnimationCurve.EaseInOut);			

			Move (currentMenuView, -offset);
			foreach (var view in currentCell.ContentView.Subviews){
				if (view == currentMenuView)
					continue;
				Move (view, -offset);
			}
			UIView.CommitAnimations ();
		}
		
		public virtual void OnSwipe (NSIndexPath path, UITableViewCell cell)
		{
			var e = Root [path.Section][path.Row];
			if (e is TweetElement){
				var frame = cell.ContentView.Frame;
				
				TableView.ScrollEnabled = false;
				var button = UIButton.FromType (UIButtonType.RoundedRect);
				button.Frame = new RectangleF (0, 0, frame.Width, frame.Height);
				
				ShowMenu (button, cell);
			}
		}
		
		public virtual void CancelMenu ()
		{
			TableView.ScrollEnabled = true;
			HideMenu ();
		}
		
		public override UITableView MakeTableView (RectangleF bounds, UITableViewStyle style)
		{
			return new SwipeDetectingTableView (bounds, style, this);
		}
	}
}

#endif