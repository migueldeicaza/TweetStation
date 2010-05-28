using System;
using System.Collections.Generic;
using System.Drawing;
using MonoTouch.CoreGraphics;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace TweetStation
{
	/// <summary>
	///   A UIView that renders a tweet in full, including highlighted #hash, @usernames
	///   and urls
	/// </summary>
	public class TweetView : UIView {
		public float Height { get; private set; }

		UIFont regular = UIFont.SystemFontOfSize (fontHeight);
		UIFont bold = UIFont.BoldSystemFontOfSize (fontHeight);
		
		string text;
		RectangleF lastRect;
		List<Block> blocks;
		Block highlighted = null;

		const int fontHeight = 17;
		
		public TweetView (RectangleF frame, string text) : base (frame)
		{
			blocks = new List<Block> ();
			lastRect = RectangleF.Empty;

			this.text = text;
			Height = Layout ();
			
			// Update our Frame size
			var f = Frame;
			f.Height = Height;
			Frame = f;
		}
		
		class Block {
			public string Value;
			public RectangleF Bounds;
			public UIFont Font;
		}
		
		//const int spaceLen = 4;
		const int lineHeight = fontHeight + 4;
		float Layout ()
		{
			float max = Bounds.Width, segmentLength, lastx = 0, x = 0, y = 0;
			int p = 0;
			UIFont font = regular, lastFont = null;
			string line = "";
			
			blocks.Clear ();
			while (p < text.Length){
				int sidx = text.IndexOf (' ', p);
				if (sidx == -1)
					sidx = text.Length-1;
						
				var segment = text.Substring (p, sidx-p+1);
				if (segment.Length == 0)
					break;
				
				var start = segment [0];
				if (start == '@' || start == '#' || segment.StartsWith ("http://", StringComparison.Ordinal))
					font = bold;
				else
					font = regular;
				
				segmentLength = StringSize (segment, font).Width;
			
				// If we would overflow the line.
				if (x + segmentLength >= max){
					// Push the text we have so far, go to next line
					if (line != ""){
						blocks.Add (new Block () {
							Bounds = new RectangleF (lastx, y, x-lastx, lineHeight),
							Value = line,
							Font = lastFont ?? font,
						});
						y += lineHeight;
						lastx = 0;
					}
					
					// Too long to fit even on its own line, stick it on its own line.
					if (segmentLength >= max){
						var dim = StringSize (segment, font, new SizeF (max, float.MaxValue), UILineBreakMode.WordWrap);
						blocks.Add (new Block () {
							Bounds = new RectangleF (new PointF (0, y), dim),
							Value = segment,
							Font = lastFont ?? font
						});
						y += dim.Height;
						x = 0;
						line = "";
					} else {
						x = segmentLength;
						line = segment;
					}
					p = sidx + 1;
					lastFont = font;
				} else {
					// append the segment if the font changed, or if the font
					// is bold (so we can make a tappable element on its own).
					if (x != 0 && (font != lastFont || font == bold)){
						blocks.Add (new Block () {
							Bounds = new RectangleF (lastx, y, x-lastx, lineHeight),
							Value = line,
							Font = lastFont
						});
						lastx = x;
						line = segment;
						lastFont = font;
					} else {
						lastFont = font;
						line = line + segment;
					}
					x += segmentLength;
					p = sidx+1;
				}
				// remove duplicate spaces
				while (p < text.Length && text [p] == ' ')
					p++;
				//Console.WriteLine ("p={0}", p);
			}
			if (line == "")
				return y;
			
			blocks.Add (new Block () {
				Bounds = new RectangleF (lastx, y, x-lastx, lineHeight),
				Value = line,
				Font = font
			});
			
			return y + lineHeight;
		}
		
		public override void Draw (RectangleF rect)
		{
			if (rect != lastRect){
				Layout ();
				lastRect = rect;
			}
			
			var context = UIGraphics.GetCurrentContext ();
			UIFont last = null;

			foreach (var block in blocks){
				var font = block.Font;
				if (font != last){
					if (font == bold)
						context.SetRGBFillColor (0.1f, 0.5f, 0.87f, 1);
					else
						context.SetRGBFillColor (0, 0, 0, 1);
					last = font;
				}

				// selected?
				if (block == highlighted && block.Font == bold){
					context.FillRect (block.Bounds);
					context.SetRGBFillColor (1, 1, 1, 1);
					last = null;
				}
				DrawString (block.Value, block.Bounds, block.Font);
				//context.SetRGBStrokeColor (1, 0, 1, 1);
				//context.StrokeRect (block.Bounds);
			}
		}
		
		public override void TouchesBegan (NSSet touches, UIEvent evt)
		{
			Track ((touches.AnyObject as UITouch).LocationInView (this));
		}

		void Track (PointF pos)
		{
			var current = highlighted;
			
			foreach (var block in blocks){
				if (!block.Bounds.Contains (pos))
					continue;

				highlighted = block;
				SetNeedsDisplay ();
			}
		}
		
		public delegate void TappedEvent (string value);
		
		public event TappedEvent Tapped;
		
		public override void TouchesEnded (NSSet touches, UIEvent evt)
		{
			if (highlighted != null && highlighted.Font == bold){
				if (Tapped != null)
					Tapped (highlighted.Value);
			}
			
			highlighted = null;
			SetNeedsDisplay ();
		}
		
		public override void TouchesCancelled (NSSet touches, UIEvent evt)
		{
			highlighted = null;
			SetNeedsDisplay ();
		}

		public override void TouchesMoved (NSSet touches, UIEvent evt)
		{
			Track ((touches.AnyObject as UITouch).LocationInView (this));
		}
	}
}
