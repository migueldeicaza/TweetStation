//
// PicDetect, utility to find if there are any picture urls that we can render
//
using System;
namespace TweetStation
{
	public static class PicDetect
	{
		//
		// Detects picture urls, returns the picture url, and if possible
		// the thumbnail url.   For now, it detects one, but perhaps we should
		// detect multiple?
		//
		public static string FindPicUrl (string text, out string thumbUrl)
		{
			int last = 0;
			thumbUrl = null;
			Uri url = null;
			
			while (last < text.Length) {
				int p = text.IndexOf ("http://", last);
				if (p == -1)
					break;
				int urlEnd = text.IndexOf (' ', p);
				try {
					if (urlEnd == -1) {
						url = new Uri (text.Substring (p));
						last = text.Length;
					} else {
						url = new Uri (text.Substring (p, urlEnd-p));
						last = urlEnd;
					}
				} catch { 
					break;
				}
				switch (url.Host.ToLower ()){
				case "twitpic.com":
					thumbUrl = "http://" + url.Host + "/show/thumb" + url.LocalPath;
					break;
					
				case "yfrog.com":
				case "yfrog.ru":
				case "yfrog.com.tr":
				case "yfrog.it":
				case "yfrog.fr":
				case "yfrog.co.il":
				case "yfrog.co.uk":
				case "yfrog.com.pl":
				case "yfrog.pl":
				case "yfrog.eu":
					thumbUrl = url.ToString () + ".th.jpg";
					break;
					
				case "tweetphoto.com":
					thumbUrl = "http://tweetphotoapi.com/api/TPAPI.svc/imagefromurl?size=thumbnail&url=" + url.ToString ();
					break;
					
				case "img.ly":
					thumbUrl = "http://" + url.Host + "/show/thumb" + url.LocalPath;
					break;
					
				case "moby.to":
				case "twitgoo.com":
					// requires another key
					break;
					
				default:
					url = null;
					break;
				}
				if (thumbUrl == null)
					continue;

				return url.ToString ();
			} 
			
			thumbUrl = null;
			return null;
		}
	}
}

