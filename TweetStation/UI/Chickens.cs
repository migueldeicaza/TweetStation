//
// Split on a separate partial class, so people can remove it easily
//
//
// Chicke.cs: classes for supporting the Chicken Infrastructure of TweetStation
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
//
using System;
using MonoTouch.Dialog;
using MonoTouch.AVFoundation;
using System.Drawing;
using MonoTouch.Foundation;

namespace TweetStation
{
	public partial class BaseTimelineViewController {
		public static bool ChickenNoisesEnabled = Util.Defaults.IntForKey ("enableChickens") == 1;
		
		// A subclass of the header, we hook up to SetStatus to put our own effects
		// We do this lazily to avoid startup costs
		
		public class CapturePullEvents : RefreshTableHeaderView {
			AVAudioPlayer chickenStart, chickenEnd;

			public CapturePullEvents (RectangleF rect) : base (rect)
			{
			}
			
			bool playedFirstChicken;
			public override void SetStatus (RefreshViewStatus status)
			{
				base.SetStatus (status);
				
				if (!ChickenNoisesEnabled)
					return;
				
				switch (status){
				case RefreshViewStatus.ReleaseToReload:
					if (chickenStart == null)
						chickenStart = AVAudioPlayer.FromUrl (new NSUrl ("Audio/chicken1.caf"));
					if (chickenStart != null){
						chickenStart.CurrentTime = 0;
						chickenStart.Volume = 0.5f;
						chickenStart.Play ();
						playedFirstChicken = true;
					}
					break;
					
				case RefreshViewStatus.Loading:
					if (chickenEnd == null)
						chickenEnd = AVAudioPlayer.FromUrl (new NSUrl ("Audio/chicken2.caf"));
					if (chickenEnd != null && playedFirstChicken){
						chickenStart.CurrentTime = 0;
						chickenEnd.Volume = 0.5f;
						chickenEnd.Play ();
					}
					playedFirstChicken = false;
					break;
					
				default:
					playedFirstChicken = false;
					break;
				}
			}
		}

		public override RefreshTableHeaderView MakeRefreshTableHeaderView (RectangleF rect)
		{
			return new CapturePullEvents (rect);
		}
	}
}

