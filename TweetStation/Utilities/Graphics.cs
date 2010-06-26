//
// Utilities for dealing with graphics
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
using MonoTouch.CoreGraphics;
using MonoTouch.UIKit;

namespace TweetStation
{
	public static class Graphics
	{
		static CGPath smallPath = MakeRoundedPath (48);
		static CGPath largePath = MakeRoundedPath (73);
		public static bool HighRes = UIScreen.MainScreen.Scale > 1;
		
        // Child proof the image by rounding the edges of the image
        internal static UIImage RemoveSharpEdges (UIImage image)
        {
			if (image == null)
				throw new ArgumentNullException ("image");
			
			float size = HighRes ? 73 : 48;
			
            UIGraphics.BeginImageContext (new SizeF (size, size));
	        var c = UIGraphics.GetCurrentContext ();

			if (HighRes)
				c.AddPath (largePath);
			else 
				c.AddPath (smallPath);
			
        	c.Clip ();

        	image.Draw (new RectangleF (0, 0, size, size));
            var converted = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return converted;
        }
		
		//
		// Centers image, scales and removes borders
		//
		internal static UIImage PrepareForProfileView (UIImage image)
		{
			const int size = 73;
			if (image == null)
				throw new ArgumentNullException ("image");
			
            UIGraphics.BeginImageContext (new SizeF (73, 73));
            var c = UIGraphics.GetCurrentContext ();

			c.AddPath (largePath);
            c.Clip ();

			// Twitter not always returns squared images, adjust for that.
			var cg = image.CGImage;
			float width = cg.Width;
			float height = cg.Height;
			if (width != height){
				float x = 0, y = 0;
				if (width > height){
					x = (width-height)/2;
					width = height;
				} else {
					y = (height-width)/2;
					height = width;
				}
				c.ScaleCTM (1, -1);
				using (var copy = cg.WithImageInRect (new RectangleF (x, y, width, height))){
					c.DrawImage (new RectangleF (0, 0, size, -size), copy);
				}
			} else 
	            image.Draw (new RectangleF (0, 0, size, size));
			
            var converted = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return converted;
		}
		
		internal static CGPath MakeRoundedPath (float size)
		{
			float hsize = size/2;
			
			var path = new CGPath ();
			path.MoveToPoint (size, hsize);
			path.AddArcToPoint (size, size, hsize, size, 4);
			path.AddArcToPoint (0, size, 0, hsize, 4);
			path.AddArcToPoint (0, 0, hsize, 0, 4);
			path.AddArcToPoint (size, 0, size, hsize, 4);
			path.CloseSubpath ();
			
			return path;
		}
	}
	
	public class TriangleView : UIView {
		UIColor fill, stroke;
		
		public TriangleView (UIColor fill, UIColor stroke) 
		{
			Opaque = false;
			this.fill = fill;
			this.stroke = stroke;
		}
		
		public override void Draw (RectangleF rect)
		{
			var context = UIGraphics.GetCurrentContext ();
			var b = Bounds;
			
			fill.SetColor ();
			context.MoveTo (0, b.Height);
			context.AddLineToPoint (b.Width/2, 0);
			context.AddLineToPoint (b.Width, b.Height);
			context.ClosePath ();
			context.FillPath ();
			
			stroke.SetColor ();
			context.MoveTo (0, b.Width/2);
			context.AddLineToPoint (b.Width/2, 0);
			context.AddLineToPoint (b.Width, b.Width/2);
			context.StrokePath ();
		}
	}
	
}
