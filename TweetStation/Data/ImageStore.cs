using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.CoreGraphics;

namespace TweetStation
{
	public interface IImageUpdated {
		void UpdatedImage (long id);
	}
	
	//
	// Provides an interface to download pictures in the background
	// and keep a local cache of the original files + rounded versions
	//
	public static class ImageStore
	{
		const int MaxRequests = 4;
		static string PicDir, SmallPicDir, TmpDir, RoundedPicDir; 
		public readonly static UIImage DefaultImage;
		static LRUCache<long,UIImage> cache;
		
		// A list of requests that have been issues, with a list of objects to notify.
		static Dictionary<long, List<IImageUpdated>> pendingRequests;
		
		// A list of updates that have completed, we must notify the main thread about them.
		static HashSet<long> queuedUpdates;
		
		// A queue used to avoid flooding the network stack with HTTP requests
		static Queue<long> requestQueue;

		static NSString nsDispatcher = new NSString ("x");
		
		static ImageStore ()
		{
			PicDir = Path.Combine (Util.BaseDir, "Library/Caches/Pictures");
			TmpDir = Path.Combine (Util.BaseDir, "tmp/downloads/");
			SmallPicDir = Path.Combine (PicDir, "Scaled/");
			RoundedPicDir = Path.Combine (PicDir, "Rounded/");
			
			if (!Directory.Exists (SmallPicDir))
				Directory.CreateDirectory (SmallPicDir);
			
			if (!Directory.Exists (TmpDir))
				Directory.CreateDirectory (TmpDir);
			
			if (!Directory.Exists (RoundedPicDir))
				Directory.CreateDirectory (RoundedPicDir);

			DefaultImage = UIImage.FromFile ("Images/default_profile_4_normal.png");
			cache = new LRUCache<long,UIImage> (200);
			pendingRequests = new Dictionary<long,List<IImageUpdated>> ();
			queuedUpdates = new HashSet<long>();
			requestQueue = new Queue<long> ();
		}
		
		public static UIImage GetLocalProfilePicture (long id)
		{
			UIImage ret;
			
			lock (cache){
				ret = cache [id];
				if (ret != null)
					return ret;
			}
			
			if (pendingRequests.ContainsKey (id))
				return null;
			
			string picfile = RoundedPicDir + id + ".png";			
			if (File.Exists (picfile)){
				ret = UIImage.FromFileUncached (picfile);
				lock (cache)
					cache [id] = ret;
				return ret;
			} if (File.Exists (SmallPicDir + id + ".jpg"))
				return RoundedPic (id);
			else
				return null;
		}
		
		public static UIImage GetLocalProfilePicture (string screenname)
		{
			var user = User.FromName (screenname);
			if (user == null)
				return null;
			return GetLocalProfilePicture (user.Id);
		}
		
		
		public static UIImage RequestProfilePicture (long id, string optionalUrl, IImageUpdated notify)
		{
			var pic = GetLocalProfilePicture (id);
			if (pic == null){
				QueueRequestForPicture (id, optionalUrl, notify);
				return DefaultImage;
			}
			
			return pic;
		}
		
		public static Uri GetPicUrlFromId (long id, string optionalUrl)
		{
			Uri url;
			
			if (optionalUrl == null){
				var user = User.FromId (id);
				if (user == null)
					return null;
				optionalUrl = user.PicUrl;
			}
			if (!Uri.TryCreate (optionalUrl, UriKind.Absolute, out url))
				return null;
			
			return url;
		}
		
		static string Name (long id)
		{
			var user = User.FromId (id);
			return user.Screenname;
		}
		
		//
		// Requests that the picture for "id" be downloaded, the optional url prevents
		// one lookup, it can be null if not known
		//
		public static void QueueRequestForPicture (long id, string optionalUrl, IImageUpdated notify)
		{
			if (notify == null)
				throw new ArgumentNullException ("notify");
			
			Console.WriteLine ("Requesting Pic: {0} at {1}", id, optionalUrl);
			Uri url = GetPicUrlFromId (id, optionalUrl);
			if (url == null)
				return;

			if (pendingRequests.ContainsKey (id)){
				pendingRequests [id].Add (notify);
				return;
			}
			var slot = new List<IImageUpdated> (4);
			slot.Add (notify);
			pendingRequests [id] = slot;
			if (pendingRequests.Count >= MaxRequests){
				lock (requestQueue)
					requestQueue.Enqueue (id);
			} else {
				ThreadPool.QueueUserWorkItem (delegate { 
					try {
						StartPicDownload (id, url); 
					} catch (Exception e){
						Console.WriteLine (e);
					}
				});
			}
		}
				
		static void StartPicDownload (long id, Uri url)
		{
			do {
				var buffer = new byte [4*1024];
				
				using (var file = new FileStream (SmallPicDir+ id + ".jpg", FileMode.Create, FileAccess.Write, FileShare.Read)) {
	                	var req = WebRequest.Create (url) as HttpWebRequest;
					
	                using (var resp = req.GetResponse()) {
						using (var s = resp.GetResponseStream()) {
							int n;
							while ((n = s.Read (buffer, 0, buffer.Length)) > 0){
								file.Write (buffer, 0, n);
	                        }
						}
	                }
				}
				
				// Cluster all updates together
				bool doInvoke = false;
				lock (queuedUpdates){
					queuedUpdates.Add (id);
					
					// If this is the first queued update, must notify
					if (queuedUpdates.Count == 1)
						doInvoke = true;
				}
				
				// Try to get more jobs.
				lock (requestQueue){
					if (requestQueue.Count > 0){
						id = requestQueue.Dequeue ();
						url = GetPicUrlFromId (id, null);
						if (url == null)
							id = -1;
					} else
						id = -1;
				}				
				if (doInvoke)
					nsDispatcher.BeginInvokeOnMainThread (NotifyImageListeners);
			} while (id != -1);
		}
		
		// Runs on the main thread
		static void NotifyImageListeners ()
		{
			try {
			lock (queuedUpdates){
				foreach (var qid in queuedUpdates){
					var list = pendingRequests [qid];
					pendingRequests.Remove (qid);
					foreach (var pr in list){
							Console.WriteLine ("Notifying of picture {0}", qid);
						pr.UpdatedImage (qid);
					}
				}
				queuedUpdates.Clear ();
			}
			} catch (Exception e){
				Console.WriteLine (e);
			}
			         
		}
		
		static UIImage RoundedPic (long id)
		{
			lock (cache){
				string smallpic = SmallPicDir + id + ".jpg";
				
				using (var pic = UIImage.FromFileUncached (smallpic)){
					if (pic == null)
						return null;
					
					var cute = Graphics.RemoveSharpEdges (pic);
					var bytes = cute.AsPNG ();
					NSError err;
					bytes.Save (RoundedPicDir + id + ".png", false, out err);
					
					// we might as well add it to the cache
					cache [id] = cute;
					
					return cute;
				}
			}
		}
	}
}
