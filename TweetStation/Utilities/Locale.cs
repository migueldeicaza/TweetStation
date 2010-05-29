using System;

namespace TweetStation
{
	public static class Locale
	{
			public static string GetText (string str)
		{
			return str;
		}
		
		public static string Format (string fmt, params object [] args)
		{
			return String.Format (fmt, args);
		}
	}
}
