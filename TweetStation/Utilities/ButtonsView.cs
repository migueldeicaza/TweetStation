//
// ButtonsView.cs: A view that constructs evently spaced buttons
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
using System;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.Dialog;

namespace TweetStation
{
	
	public class ButtonsView : UIView {
		UIButton [] buttons;
		const int buttonHeight = 44;
		
		public ButtonsView (int len, string [] captions, EventHandler [] actions) : base (new Rectangle (0, 0, 320, buttonHeight))
		{
			if (captions == null || actions == null)
				throw new ArgumentNullException ();
			
			if (len > captions.Length || len > actions.Length)
				throw new ArgumentException ("Mismatched array sizes between actions and captions");
	
			buttons = new UIButton [len];
			for (int i = len; --i >= 0; ){
				var button = UIButton.FromType (UIButtonType.RoundedRect);
				
				button.Font = UIFont.BoldSystemFontOfSize (14);
				button.SetTitle (captions [i], UIControlState.Normal);
				AddSubview (button);
				button.AddTarget (actions [i], UIControlEvent.TouchUpInside);
				
				buttons [i] = button;
			}
		}
		
		const int XPad = 0;
		
		public override void LayoutSubviews ()
		{
			var cellWidth = (310-2*XPad) / buttons.Length;

			for (int i = 0; i < buttons.Length; i++){
				buttons [i].Frame = new RectangleF (XPad + i * cellWidth, 0, cellWidth-8, buttonHeight);
			}
		}
	}
}
