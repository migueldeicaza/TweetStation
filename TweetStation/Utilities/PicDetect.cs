//
// PicDetect, utility to find if there are any picture urls that we can render
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
namespace TweetStation
{
	public static class PicDetect
	{
		//
		// Detects picture urls, returns the picture url, and if possible
		// the thumbnail url.   For now, it detects one, but perhaps we should
		// detect multiple?
		//
		public static string FindPicUrl (string text, out string thumbUrl, out string previewUrl)
		{
			int last = 0;
			thumbUrl = null;
			previewUrl = null;
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
					previewUrl = "http://" + url.Host + "/show/large" + url.LocalPath;
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
					previewUrl = url.ToString () + ":iphone";
					break;
					
				case "pic.gd":
				case "tweetphoto.com":
					thumbUrl = "http://tweetphotoapi.com/api/TPAPI.svc/imagefromurl?size=thumbnail&url=" + url.ToString ();
					previewUrl = "http://tweetphotoapi.com/api/TPAPI.svc/imagefromurl?size=medium&url=" + url.ToString ();
					break;
					
				case "img.ly":
					thumbUrl = "http://" + url.Host + "/show/thumb" + url.LocalPath;
					previewUrl = "http://" + url.Host + "/show/large" + url.LocalPath;
					break;
					
				case "moby.to":
				case "twitgoo.com":
					// life is too short to sign up for another key
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

