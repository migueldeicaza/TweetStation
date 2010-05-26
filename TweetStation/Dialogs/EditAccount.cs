using System;
using System.Net;
using MonoTouch.Dialog;
using MonoTouch.UIKit;

namespace TweetStation
{
	public class EditAccount : UINavigationController {
		class AccountInfo  {
			[Section ("Account", "If you do not have a twitter account,\nvisit http://twitter.com")]
			
			[Entry ("Twitter screename")]
			public string Login;
			
			[Password ("Twitter password")]
			public string Password;
		}
		
		void CheckCredentials (AccountInfo info, Action<string> result)
		{
			Util.PushNetworkActive ();
			var http = (HttpWebRequest) WebRequest.Create ("http://api.twitter.com/1/statuses/home_timeline.json");
			http.Credentials = new NetworkCredential (info.Login, info.Password);
			http.BeginGetResponse (delegate (IAsyncResult iar){
				HttpWebResponse response = null;
				try {
					response = (HttpWebResponse) http.EndGetResponse (iar);
				} catch (WebException we){
					BeginInvokeOnMainThread (delegate {
						result (we.Message);
					});
					return;
				} catch (Exception) {
					BeginInvokeOnMainThread (delegate {
						result ("General error");
					});
					return;
				}
				
				BeginInvokeOnMainThread (delegate {
					result (response.StatusCode == HttpStatusCode.OK ? null : "Attempted to login failed");});
			}, null);
		}
		
		UIAlertView dlg;
		
		public EditAccount (IAccountContainer container, TwitterAccount account, bool pushing)
		{
			var info = new AccountInfo ();
			bool newAccount = account == null;
			
			if (newAccount)
				account = new TwitterAccount ();
			else {
				info.Login = account.Username;
				//info.Password = account.Password;
			}
			
			var bc = new BindingContext (this, info, "Edit Account");
			var dvc = new DialogViewController (bc.Root, true);
			PushViewController (dvc, false);
			UIBarButtonItem done = null;
			done = new UIBarButtonItem (UIBarButtonSystemItem.Done, delegate {
				bc.Fetch ();
				
				done.Enabled = false;
				CheckCredentials (info, delegate (string errorMessage) { 
					Util.PopNetworkActive ();
					done.Enabled = true; 

					if (errorMessage == null){
						account.Username = info.Login;
						//account.Password = info.Password;
						
						if (newAccount)
							Database.Main.Insert (account);
						else
							Database.Main.Update (account);
						
						account.SetDefaultAccount ();
						DismissModalViewControllerAnimated (true);
						container.Account = account;
					} else {
						dlg = new UIAlertView ("Login error", errorMessage, null, "Close");
						dlg.Show ();
					}
				});
			});
			
			dvc.NavigationItem.SetRightBarButtonItem (done, false);
		}
	}	
}
