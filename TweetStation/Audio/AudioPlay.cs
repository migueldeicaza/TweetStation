//
// A wrapper to play music in a loop
//
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
using System.IO;
using System.Threading;
using StreamingAudio;
using MonoTouch.AudioToolbox;
using MonoTouch.Foundation;

namespace TweetStation
{
	public class AudioPlay {
		StreamingPlayback player;
		FileStream fs;
		volatile bool stop;
		
		static AudioPlay ()
		{
			AudioSession.Initialize ();
		}
		
		public AudioPlay (string file)
		{
			stop = false;
			fs = File.OpenRead (file);
			player = new StreamingPlayback ();
			ThreadPool.QueueUserWorkItem (Play);
		}

		void Play (object state)
		{
			AudioSession.Category = AudioSessionCategory.AmbientSound;
			AudioSession.RoutingOverride = AudioSessionRoutingOverride.Speaker;
			
			var buffer = new byte [8192];
			int n;
			
			while (!stop){
				while ((n = fs.Read (buffer, 0, buffer.Length)) != 0){
					player.ParseBytes (buffer, n, false);
					if (stop)
						break;
				}
				fs.Position = 0;
			}
			player.Dispose ();
		}
		
		public void Stop ()
		{
			// If we have not yet decoded enough data to play, return
			var oqueue = player.OutputQueue;
			if (oqueue == null)
				return;
			
			// Slowly turn off the audio
			NSTimer timer = null;
			float volume = player.OutputQueue.Volume;
			timer = NSTimer.CreateRepeatingScheduledTimer (TimeSpan.FromMilliseconds (100), delegate {
				volume -= 0.05f;
				player.OutputQueue.Volume = volume;
				if (volume <= 0.1){
					InternalStop ();
					timer.Invalidate ();
				}
			});
		}
		
		void InternalStop ()
		{
			// Stop the output queue, then tell the loop to stop processing data
			player.Pause ();
			stop = true;
		}
		
	}
}

