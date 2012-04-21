using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Drawing;
using System.Threading;
using System.Net;
using System.Text;
using System.Collections.Specialized;

namespace TweetStation
{
	public abstract class UrlBookmark {
		
		public static UrlBookmark Instapaper;
		public static UrlBookmark Bookmarker;
		
		static UrlBookmark ()
		{
			Instapaper = new InstapaperBookmark ();
			Bookmarker = Instapaper;
		}
		
		public abstract void Add (string url);
		public abstract bool LoggedIn { get; }
		public abstract void SignIn (UIViewController container, Action<bool> done);
		
		class InstapaperBookmark : UrlBookmark {
			const string kPass = "instapaper.password";
			const string kUser = "instapaper.username";
			static Uri instapaperAddUri = new Uri ("https://www.instapaper.com/api/add");

			string username, password;
			bool passwordError;
			InstapaperSignInController signinController;
			
			public InstapaperBookmark ()
			{
				LoadSettings ();	
			}

			void LoadSettings ()
			{
				username = Util.Defaults.StringForKey (kUser);
				password = Util.Defaults.StringForKey (kPass);
			}
			
			public override void Add (string url)
			{
				var wc = new WebClient ();
				wc.Headers [HttpRequestHeader.Authorization] = MakeAuth (username, password);
				
				try {
					wc.UploadValues (instapaperAddUri, new NameValueCollection () { { "url", url } }); 
				} catch (WebException we) {
					var response = we.Response as HttpWebResponse;
					if (response != null && response.StatusCode == HttpStatusCode.Forbidden)
						passwordError = true;
				}
			}
			
			static string MakeAuth (string user, string pass)
			{
				return "Basic " + Convert.ToBase64String (Encoding.ASCII.GetBytes (user + ":" + pass));
			}
			

			public override bool LoggedIn {
				get {
					return username != null && password != null && passwordError == false;
				}
			}
			
			public override void SignIn (UIViewController container, Action<bool> callback)
			{
				signinController = new InstapaperSignInController (this, result => {
					signinController.Dispose ();
					signinController = null;
					callback (result);
				});
				
				container.PresentModalViewController (signinController, true);
			}

			// 
			// Implements the view controller for doing the Instapaper sign-in
			//
			public class InstapaperSignInController : UINavigationController {
				EntryElement userElement, passwordElement;
				DialogViewController dvc;
				InstapaperBookmark instapaper;
				Action<bool> callback;
				ProgressHud hud;
				UIAlertView alert;
	
				public InstapaperSignInController (InstapaperBookmark instapaper, Action<bool> callback) 
				{
					this.instapaper = instapaper;
					this.callback = callback;
					
					var root = new RootElement ("Instapaper") {
						new Section ("", Locale.GetText ("Get your account at instapaper.com")){
							(userElement = new EntryElement (Locale.GetText ("Username"), Locale.GetText ("or email address"), instapaper.username)),
							(passwordElement = new EntryElement (Locale.GetText ("Password"), "", instapaper.password, true))
						}
					};
					dvc = new DialogViewController (UITableViewStyle.Grouped, root, true);
					dvc.NavigationItem.SetLeftBarButtonItem (new UIBarButtonItem (Locale.GetText ("Close"), UIBarButtonItemStyle.Plain, delegate { Close (false);}), false);
					dvc.NavigationItem.RightBarButtonItem = new UIBarButtonItem (Locale.GetText ("Save"), UIBarButtonItemStyle.Plain, delegate { Save (); });

					SetViewControllers (new UIViewController [] { dvc }, false);
					
					userElement.BecomeFirstResponder (false);
				}
				
				void Close (bool done)
				{
					DismissModalViewControllerAnimated (true);
					callback (done);
				}
				
				void DestroyHud ()
				{
					hud.RemoveFromSuperview ();
					hud = null;
				}
				
				void Save ()
				{
					userElement.FetchValue ();
					passwordElement.FetchValue ();
					
					hud = new ProgressHud (Locale.GetText ("Authenticating"), Locale.GetText ("Cancel"));
					hud.ButtonPressed += delegate { DestroyHud (); };
					dvc.View.AddSubview (hud);
					
					// Send an incomplete request with login/password, if this returns 403, we got the wrong password
					ThreadPool.QueueUserWorkItem ((x) => { 
						bool ok = false;
						try {
							ok = ValidateCredentials (); 
						} catch (Exception e){
							Util.ReportError (this, e, Locale.GetText ("While validating credentials"));
						}
						BeginInvokeOnMainThread (delegate {
							DestroyHud ();
							if (ok)
								Close (true);
						});
					});
				}
				
				bool ValidateCredentials ()
				{
					var req = (HttpWebRequest) WebRequest.Create (instapaperAddUri);
					req.Method = "POST";
					req.Headers.Add ("Authorization", MakeAuth (userElement.Value, passwordElement.Value));
					try {
						req.GetResponse ();
						BeginInvokeOnMainThread (delegate { hud.Progress = 0.5f; });
					} catch (WebException we){
						var response = we.Response as HttpWebResponse;
						if (response != null){
							if (response.StatusCode == HttpStatusCode.Forbidden){
								BeginInvokeOnMainThread (delegate {
									DestroyHud ();
									alert = new UIAlertView (Locale.GetText ("Login error"), Locale.GetText ("Invalid password"), null, Locale.GetText ("Close"));
									alert.WillDismiss += delegate { 
										userElement.BecomeFirstResponder (true); 
										alert = null; 
									};
									alert.Show ();
								});
								return false;
							}
						}
					}
					// We got a valid password
					Util.Defaults.SetString (instapaper.username = userElement.Value, kUser);
					Util.Defaults.SetString (instapaper.password = passwordElement.Value, kPass);
					instapaper.passwordError = false;
					return true;
				}
			}			
		}
	}
}

