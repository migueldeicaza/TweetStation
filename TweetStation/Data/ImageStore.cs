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
	// The IDs used here have the following meaning:
	//   Positive numbers are the small profile pictures and correspond to a twitter ID
	//   Negative numbers are medium size pictures for the same twitter ID
	//   Numbers above TmpStartId are transient pictures, used because Twitter
	//   search returns a *different* set of userIds on search results
	// 

	public static class ImageStore
	{
		public const long TempStartId = 100000000000000;
		const int MaxRequests = 6;
		static string PicDir, RoundedPicDir, LargeRoundedPicDir, TmpDir; 
		public readonly static UIImage DefaultImage;
		static LRUCache<long,UIImage> cache;
		
		// A list of requests that have been issues, with a list of objects to notify.
		static Dictionary<long, List<IImageUpdated>> pendingRequests;
		
		// A list of updates that have completed, we must notify the main thread about them.
		static HashSet<long> queuedUpdates;
		
		// A queue used to avoid flooding the network stack with HTTP requests
		static Stack<long> requestQueue;
		
		// Keeps id -> url mappings around
		static Dictionary<long, string> idToUrl;

		static NSString nsDispatcher = new NSString ("x");
		
		static ImageStore ()
		{
			PicDir = Path.Combine (Util.BaseDir, "Library/Caches/Pictures/");
			RoundedPicDir = Path.Combine (PicDir, "Rounded/");
			LargeRoundedPicDir = Path.Combine (PicDir, "LargeRounded/");
			TmpDir = Path.Combine (Util.BaseDir, "tmp/");
			
			if (!Directory.Exists (PicDir))
				Directory.CreateDirectory (PicDir);
			
			if (!Directory.Exists (RoundedPicDir))
				Directory.CreateDirectory (RoundedPicDir);

			if (!Directory.Exists (LargeRoundedPicDir))
				Directory.CreateDirectory (LargeRoundedPicDir);

			DefaultImage = UIImage.FromFile ("Images/default_profile_4_normal.png");
			cache = new LRUCache<long,UIImage> (200);
			pendingRequests = new Dictionary<long,List<IImageUpdated>> ();
			idToUrl = new Dictionary<long,string> ();
			queuedUpdates = new HashSet<long>();
			requestQueue = new Stack<long> ();
		}
		
		public static void Purge ()
		{
			cache.Purge ();
		}
		
		public static UIImage GetLocalProfilePicture (long id)
		{
			UIImage ret;
			
			lock (cache){
				ret = cache [id];
				if (ret != null)
					return ret;
			}

			lock (requestQueue){
				if (pendingRequests.ContainsKey (id))
					return null;
			}

			string picfile;
			if (id >= TempStartId){
				// Delay execution of this until the user does searches.
				EnsureTmpIsClean ();
				picfile = TmpDir + id + ".png";
			} else if (id >= 0)
				picfile = RoundedPicDir + id + ".png";
			else
				picfile = LargeRoundedPicDir + id + ".png";
			
			if (File.Exists (picfile)){
				ret = UIImage.FromFileUncached (picfile);
				if (ret != null){
					lock (cache)
						cache [id] = ret;
					return ret;
				}
			} 
			
			picfile = PicDir + id + ".png";
			if (File.Exists (PicDir + id + ".png"))
				return RoundedPic (picfile, id);

			return null;
		}
		
		public static UIImage GetLocalProfilePicture (string screenname)
		{
			var user = User.FromName (screenname);
			if (user == null)
				return null;
			return GetLocalProfilePicture (user.Id);
		}
		
		//
		// Fetches a profile picture, the ID is used internally 
		public static UIImage RequestProfilePicture (long id, string optionalUrl, IImageUpdated notify)
		{
			var pic = GetLocalProfilePicture (id);
			if (pic == null){
				QueueRequestForPicture (id, optionalUrl, notify); 
				
				// return low-res version of the picture while waiting for the high-res version to come
				if (id < 0)
					pic = GetLocalProfilePicture (-id);
				if (pic != null)
					return pic;
				
				return DefaultImage;
			}
			
			return pic;
		}
		
		static Uri GetPicUrlFromId (long id, string optionalUrl)
		{
			Uri url;
			
			if (optionalUrl == null){
				if (!idToUrl.TryGetValue (id, out optionalUrl)){
					var user = User.FromId (id);
					if (user == null)
						return null;
					optionalUrl = user.PicUrl;
				}
			}
			if (id < 0 || Graphics.HighRes){
				int _normalIdx = optionalUrl.LastIndexOf ("_normal");	
				if (_normalIdx != -1)
					optionalUrl = optionalUrl.Substring (0, _normalIdx) + "_bigger" + optionalUrl.Substring (_normalIdx + 7);
			}
			if (!Uri.TryCreate (optionalUrl, UriKind.Absolute, out url))
				return null;
			
			idToUrl [id] = optionalUrl;
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
			
			Uri url;
			lock (requestQueue)
				url = GetPicUrlFromId (id, optionalUrl);
			
			if (url == null)
				return;

			lock (requestQueue){
				if (pendingRequests.ContainsKey (id)){
					//Util.Log ("pendingRequest: added new listener for {0}", id);
					pendingRequests [id].Add (notify);
					return;
				}
				var slot = new List<IImageUpdated> (4);
				slot.Add (notify);
				pendingRequests [id] = slot;
				
				if (requestQueue.Count >= MaxRequests){
					Util.Log ("Queuing Image request because {0} >= {1} {2}", requestQueue.Count, MaxRequests, picDownloaders);
					requestQueue.Push (id);
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
		}

		static void EnsureTmpIsClean ()
		{
			if (TmpCleaned)
				return;
			
			foreach (string f in Directory.GetFiles (TmpDir, "*.png"))
				File.Delete (f);
			TmpCleaned = true;
		}
		
		static bool TmpCleaned;
		
		static bool Download (Uri url, string target)
		{
			var buffer = new byte [4*1024];
			
			try {
				using (var file = new FileStream (target, FileMode.Create, FileAccess.Write, FileShare.Read)) {
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
				return true;
			} catch (Exception e) {
				Console.WriteLine ("Problem with {0} {1}", url, e);
				return false;
			}
		}
		
		static long picDownloaders;
		
		static void StartPicDownload (long id, Uri url)
		{
			Interlocked.Increment (ref picDownloaders);
			try {
				_StartPicDownload (id, url);
			} catch (Exception e){
				Util.Log ("CRITICAL: should have never happened {0}", e);
			}
			//Util.Log ("Leaving StartPicDownload {0}", picDownloaders);
			Interlocked.Decrement (ref picDownloaders);
		}
		
		static void _StartPicDownload (long id, Uri url)
		{
			do {
				string picdir = id < TempStartId ? PicDir : TmpDir;
				bool downloaded = false;
				
				downloaded = Download (url, picdir + id + ".png");
				if (!downloaded)
					Console.WriteLine ("Error fetching picture for {0} from {1}", id, url);
				
				// Cluster all updates together
				bool doInvoke = false;
				
				lock (requestQueue){
					if (downloaded){
						queuedUpdates.Add (id);
					
						// If this is the first queued update, must notify
						if (queuedUpdates.Count == 1)
							doInvoke = true;
					} else
						pendingRequests.Remove (id);

					idToUrl.Remove (id);

					// Try to get more jobs.
					if (requestQueue.Count > 0){
						id = requestQueue.Pop ();
						url = GetPicUrlFromId (id, null);
						if (url == null){
							Util.Log ("Dropping request {0} because url is null", id);
							pendingRequests.Remove (id);
							id = -1;
						}
					} else {
						//Util.Log ("Leaving because requestQueue.Count = {0} NOTE: {1}", requestQueue.Count, pendingRequests.Count);
						id = -1;
					}
				}	
				if (doInvoke)
					nsDispatcher.BeginInvokeOnMainThread (NotifyImageListeners);
				
			} while (id != -1);
		}
		
		// Runs on the main thread
		static void NotifyImageListeners ()
		{
			lock (requestQueue){
				foreach (var qid in queuedUpdates){
					var list = pendingRequests [qid];
					pendingRequests.Remove (qid);
					foreach (var pr in list){
						try {
							pr.UpdatedImage (qid);
						} catch (Exception e){
							Console.WriteLine (e);
						}
					}
				}
				queuedUpdates.Clear ();
			}
		}
		
		static UIImage RoundedPic (string picfile, long id)
		{
			lock (cache){				
				using (var pic = UIImage.FromFileUncached (picfile)){
					if (pic == null)
						return null;
					
					UIImage cute;
					if (id > 0)
						cute = Graphics.RemoveSharpEdges (pic);
					else
						cute = Graphics.PrepareForProfileView (pic);
					var bytes = cute.AsPNG ();
					NSError err;
					bytes.Save ((id > 0 ? RoundedPicDir : LargeRoundedPicDir) + id + ".png", false, out err);
					
					// we might as well add it to the cache
					cache [id] = cute;
					
					return cute;
				}
			}
		}
	}
}
