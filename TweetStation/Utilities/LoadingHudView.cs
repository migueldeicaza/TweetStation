//
//  LoadingHUDView.cs
//
//  Converted to MonoTouch on 1/18/09 - Eduardo Scoz || http://escoz.com
//  Originaly created by Devin Ross on 7/2/09 - tapku.com || http://github.com/devinross/tapkulibrary
//
/*
 
 Permission is hereby granted, free of charge, to any person
 obtaining a copy of this software and associated documentation
 files (the "Software"), to deal in the Software without
 restriction, including without limitation the rights to use,
 copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the
 Software is furnished to do so, subject to the following
 conditions:
 
 The above copyright notice and this permission notice shall be
 included in all copies or substantial portions of the Software.
 
 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 OTHER DEALINGS IN THE SOFTWARE.
 
 */

using MonoTouch.UIKit;
using System.Drawing;
using MonoTouch.CoreGraphics;
using MonoTouch.Foundation;
using System;

namespace TweetStation {
	public class LoadingHUDView : UIView {
	
		public static int WIDTH_MARGIN = 20;
		public static int HEIGHT_MARGIN = 20;
	
		string _title, _message;
		UIActivityIndicatorView _activity;
		bool _hidden;
		UIFont titleFont = UIFont.BoldSystemFontOfSize(16);
		UIFont messageFont = UIFont.SystemFontOfSize(13);
	
		public string Title {
			get { return _title; }
			set {
				_title = value; 
				this.SetNeedsDisplay();
			}
		}
	
		public string Message {
			get { return _message; }
			set {
				_message = value; 
				this.SetNeedsDisplay();
			}
		}
	
		public LoadingHUDView(string title, string message) 
		{
			Title = title;
			Message = message;
			_activity = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.White);
			_hidden = true;
			this.BackgroundColor = UIColor.Clear;
			Frame = new RectangleF(0,0,320,480);
			this.AddSubview(_activity);
		}
	
		public LoadingHUDView(string title) : this(title, null) {}
	
		public void StartAnimating() {
			if (!_hidden) return;
	
			_hidden = false;
			this.SetNeedsDisplay();
			this.Superview.BringSubviewToFront(this);
			_activity.StartAnimating();
		}
	
		public void StopAnimating() {
			if (_hidden) return;
			
			_hidden = true;
			this.SetNeedsDisplay();
			this.Superview.SendSubviewToBack(this);
			_activity.StopAnimating();
		}
	
	
		protected void AdjustHeight() {
			SizeF titleSize = calculateHeightOfTextForWidth(_title, titleFont, 200, UILineBreakMode.TailTruncation);
			SizeF messageSize = calculateHeightOfTextForWidth(_message, messageFont, 200, UILineBreakMode.WordWrap);
	
			var textHeight = titleSize.Height + messageSize.Height;
			
			RectangleF r = this.Frame;
			r.Size = new SizeF(300, textHeight + 20);
			this.Frame = r;
		}
	
		public override void Draw (RectangleF rect)
		{
			if (_hidden) return;
	
			int width, rWidth, rHeight, x;
			SizeF titleSize = calculateHeightOfTextForWidth(_title, titleFont, 200, UILineBreakMode.TailTruncation);
			SizeF messageSize = calculateHeightOfTextForWidth(_message, messageFont, 200, UILineBreakMode.WordWrap);
	
			if (_title.Length<1) titleSize.Height = 0;
			if (_message==null || _message.Length<1) messageSize.Height = 0;
			
			rHeight = (int)(titleSize.Height+HEIGHT_MARGIN*2 + _activity.Frame.Size.Height);
			rHeight += (int)(messageSize.Height>0 ? messageSize.Height + 10 : 0);
			rWidth = width = (int)Math.Max(titleSize.Width, messageSize.Width);
			rWidth += WIDTH_MARGIN * 2;
			x = (320-rWidth) /2;
	
			_activity.Center = new PointF(320/2, HEIGHT_MARGIN + 120 + _activity.Frame.Size.Height/2);
	
			// Rounded rectangle
			RectangleF areaRect = new RectangleF(x, 100 + HEIGHT_MARGIN, rWidth, rHeight);
			this.DrawRoundRectangle(areaRect, 8, UIColor.FromRGBA(0.0f, 0.0f, 0.0f, 0.75f)); // alpha = 0.75
			
			// Title
			UIColor.White.SetColor();
			var textRect = new RectangleF(x+WIDTH_MARGIN, 95 + _activity.Frame.Size.Height + 25 + HEIGHT_MARGIN,
				width, titleSize.Height);
			 SizeF titleDrawSize = this.DrawString(_title, textRect, titleFont, UILineBreakMode.TailTruncation, UITextAlignment.Center);
	
			// Description
			UIColor.White.SetColor();
			textRect.Y += titleDrawSize.Height+10;
			textRect = new RectangleF(textRect.Location, new SizeF(textRect.Size.Width, messageSize.Height));
			
			if (_message!=null)
				this.DrawString(_message, textRect, messageFont, UILineBreakMode.WordWrap, UITextAlignment.Center);
		}
	
		protected SizeF calculateHeightOfTextForWidth(string text, UIFont font, float width, UILineBreakMode lineBreakMode){
			return text==null? new SizeF(0, 0) : this.StringSize(text, font, new SizeF(width, 300), lineBreakMode);
		}
	}
	
	public static class UIViewExtensions {
		
		public static void DrawRoundRectangle (this UIView view, RectangleF rrect, float radius, UIColor color) 
		{
			var context = UIGraphics.GetCurrentContext ();
	
			color.SetColor ();	
			
			float minx = rrect.Left;
			float midx = rrect.Left + (rrect.Width)/2;
			float maxx = rrect.Right;
			float miny = rrect.Top;
			float midy = rrect.Y+rrect.Size.Height/2;
			float maxy = rrect.Bottom;
	
			context.MoveTo (minx, midy);
			context.AddArcToPoint (minx, miny, midx, miny, radius);
			context.AddArcToPoint (maxx, miny, maxx, midy, radius);
			context.AddArcToPoint (maxx, maxy, midx, maxy, radius);
			context.AddArcToPoint (minx, maxy, minx, midy, radius);
			context.ClosePath ();
			context.DrawPath (CGPathDrawingMode.Fill); // test others?
		}
	}
}
