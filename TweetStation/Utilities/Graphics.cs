//
// Utilities for dealing with graphics
//
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
		
        // Child proof the image by rounding the edges of the image
        internal static UIImage RemoveSharpEdges (UIImage image)
        {
			if (image == null)
				throw new ArgumentNullException ("image");
			
            UIGraphics.BeginImageContext (new SizeF (48, 48));
            var c = UIGraphics.GetCurrentContext ();

			c.AddPath (smallPath);
            c.Clip ();

            image.Draw (new RectangleF (0, 0, 48, 48));
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
}
