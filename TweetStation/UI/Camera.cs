//
// Camera.cs: Support code for taking pictures
//
// Author:
//   Miguel de Icaza (miguel@gnome.org)
//

using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;

namespace TweetStation
{
	//
	// A static class that will reuse the UIImagePickerController
	// as iPhoneOS has a crash if multiple UIImagePickerController are created
	//   http://stackoverflow.com/questions/487173
	// (Follow the links)
	//
	public static class Camera
	{
		static UIImagePickerController picker;
		static Action<NSDictionary> _callback;
		
		static void Init ()
		{
			if (picker != null)
				return;
			
			picker = new UIImagePickerController ();
			picker.Delegate = new CameraDelegate ();
		}
		
		class CameraDelegate : UIImagePickerControllerDelegate {
			public override void FinishedPickingMedia (UIImagePickerController picker, NSDictionary info)
			{
				var cb = _callback;
				_callback = null;
				
				picker.DismissModalViewControllerAnimated (true);
				cb (info);
			}
		}
		
		public static void TakePicture (UIViewController parent, Action<NSDictionary> callback)
		{
			Init ();
			picker.SourceType = UIImagePickerControllerSourceType.Camera;
			_callback = callback;
			parent.PresentModalViewController (picker, true);
		}
		
		public static void SelectPicture (UIViewController parent, Action<NSDictionary> callback)
		{
			Init ();
			picker.SourceType = UIImagePickerControllerSourceType.SavedPhotosAlbum;
			_callback = callback;
			parent.PresentModalViewController (picker, true);
		}
	}
}

