using System;
using MonoTouch.Foundation;

namespace TweetStation
{
	public static class Locale
	{
		static NSBundle main = NSBundle.MainBundle;
		
		public static string GetText (string str)
		{
			return main.LocalizedString (str, str, "");
		}
		
		public static string Format (string fmt, params object [] args)
		{
			var msg = main.LocalizedString (fmt, fmt, "");
			
			return String.Format (fmt, args);
		}
	}
}
