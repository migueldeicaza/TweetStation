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
// using System;
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
						
						lock (Database.Main){
							if (newAccount)
								Database.Main.Insert (account);
							else
								Database.Main.Update (account);
						}
						
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
