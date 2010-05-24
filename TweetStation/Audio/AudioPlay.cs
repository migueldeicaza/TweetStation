//
// A wrapper to play music in a loop
//
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
		Thread thread;
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

