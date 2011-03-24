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
			UINavigationController nav;
			DialogViewController dvc;
			
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
			
			public override bool LoggedIn {
				get {
					return username != null && password != null && passwordError == false;
				}
			}
			
			public override void SignIn (UIViewController container, Action<bool> done)
			{
				EntryElement ue, pe;
				
				var root = new RootElement ("Instapaper") {
					new Section ("", "Get your account at instapaper.com"){
						(ue = new EntryElement ("Username", "or email address", username)),
						(pe = new EntryElement ("Password", "", password, true))
					}
				};
				dvc = new DialogViewController (UITableViewStyle.Grouped, root, true);
				nav = new UINavigationController (dvc);
				
				ue.BecomeFirstResponder (false);
				
				dvc.NavigationItem.SetLeftBarButtonItem (new UIBarButtonItem (Locale.GetText ("Close"), UIBarButtonItemStyle.Plain, delegate { Close (done, false);}), false);
				dvc.NavigationItem.RightBarButtonItem = new UIBarButtonItem (Locale.GetText ("Save"), UIBarButtonItemStyle.Plain, delegate { Save (done, ue, pe); });

				container.PresentModalViewController (nav, true);
			}
			
			void Close (Action<bool> callback, bool done)
			{
				nav.DismissModalViewControllerAnimated (true);
				nav = null;
				dvc = null;
				callback (done);
			}
			
			ProgressHud hud;
			UIAlertView alert;

			void DestroyHud ()
			{
				hud.RemoveFromSuperview ();
				hud = null;
			}
			
			string MakeAuth (string user, string pass)
			{
				return "Basic " + Convert.ToBase64String (Encoding.ASCII.GetBytes (user + ":" + pass));
			}
			
			void Save (Action<bool> callback, EntryElement userElement, EntryElement passwordElement)
			{
				userElement.FetchValue ();
				passwordElement.FetchValue ();
				
				hud = new ProgressHud (Locale.GetText ("Authenticating"), Locale.GetText ("Cancel"));
				hud.ButtonPressed += delegate {
					DestroyHud ();
					Close (callback, false);
				};
				dvc.View.AddSubview (hud);
				
				// Send an incomplete request with login/password, if this returns 403, we got the wrong password
				ThreadPool.QueueUserWorkItem ((x) => {
					try {
						var req = (HttpWebRequest) WebRequest.Create (instapaperAddUri);
						req.Method = "POST";
						req.Headers.Add ("Authorization", MakeAuth (userElement.Value, passwordElement.Value));
						try {
							req.GetResponse ();
							nav.BeginInvokeOnMainThread (delegate { hud.Progress = 0.5f; });
						} catch (WebException we){
							var response = we.Response as HttpWebResponse;
							if (response != null){
								if (response.StatusCode == HttpStatusCode.Forbidden){
									nav.BeginInvokeOnMainThread (delegate {
										DestroyHud ();
										alert = new UIAlertView (Locale.GetText ("Login error"), Locale.GetText ("Invalid password"), null, Locale.GetText ("Close"));
										alert.WillDismiss += delegate { 
											userElement.BecomeFirstResponder (true); 
											alert = null; 
										};
										alert.Show ();
									});
									return;
								}
								Console.WriteLine ("Pased");
							}
						}
						// We got a valid password
						Util.Defaults.SetString (username = userElement.Value, kUser);
						Util.Defaults.SetString (password = passwordElement.Value, kPass);
						passwordError = false;
					} catch (Exception e){
						Console.WriteLine ("Error: {0}", e);
					}
					nav.BeginInvokeOnMainThread (delegate {
						DestroyHud ();
						Close (callback, false);
					});
				});
			}
		}
	}
}

