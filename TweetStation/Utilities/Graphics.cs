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
        // Child proof the image by rounding the edges of the image
        internal static UIImage RemoveSharpEdges (UIImage image)
        {
			if (image == null)
				throw new ArgumentNullException ("image");
			
            UIGraphics.BeginImageContext (new SizeF (48, 48));
            var c = UIGraphics.GetCurrentContext ();

            c.BeginPath ();
            c.MoveTo (48, 24);
            c.AddArcToPoint (48, 48, 24, 48, 4);
            c.AddArcToPoint (0, 48, 0, 24, 4);
            c.AddArcToPoint (0, 0, 24, 0, 4);
            c.AddArcToPoint (48, 0, 48, 24, 4);
            c.ClosePath ();
            c.Clip ();

            image.Draw (new PointF (0, 0));
            var converted = UIGraphics.GetImageFromCurrentImageContext ();
            UIGraphics.EndImageContext ();
            return converted;
        }
	}
}
