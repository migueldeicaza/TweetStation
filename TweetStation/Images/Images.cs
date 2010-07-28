using System;
using MonoTouch.UIKit;
namespace TweetStation
{
	public static class Images
	{
		static UIImage menuShadow;
		
		public static UIImage MenuShadow {
			get {
				if (menuShadow == null)
					menuShadow = UIImage.FromFile ("Images/menu-shadow.png");
				return menuShadow;
			}
		}
	}
}

