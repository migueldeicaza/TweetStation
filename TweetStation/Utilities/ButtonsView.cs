//
// ButtonsView.cs: A view that constructs evently spaced buttons
//
using System;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.Dialog;

namespace TweetStation
{
	
	public class ButtonsView : UIView {
		EventHandler [] actions;
		UIButton [] buttons;
		const int buttonHeight = 44;
		
		public ButtonsView (string [] captions, EventHandler [] actions) : base (new Rectangle (0, 0, 320, buttonHeight))
		{
			if (captions == null || actions == null)
				throw new ArgumentNullException ();
			
			if (captions.Length != actions.Length)
				throw new ArgumentException ("Mismatched array sizes between actions and captions");
	
			buttons = new UIButton [captions.Length];
			for (int i = captions.Length; --i >= 0; ){
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
