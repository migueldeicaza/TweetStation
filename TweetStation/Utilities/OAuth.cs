//
// OAuth framework for TweetStation
//
// Author;
//   Miguel de Icaza (miguel@gnome.org)
//
// Possible optimizations:
//   Instead of sorting every time, keep things sorted
//   Reuse the same dictionary, update the values
//

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Net;
using System.Web;
using System.Security.Cryptography;
using MonoTouch.UIKit;
using MonoTouch.Foundation;

namespace TweetStation
{
	public class OAuth {
		// OAuth Configuartion options.
		public string ConsumerKey, Callback, ConsumerSecret;
		public string xAuthUsername, xAuthPassword;
		
		// The urls we use for communicating with OAuth.
		string requestTokenUrl, accessTokenUrl, authorizeUrl;

		string RequestToken, RequestTokenSecret;
		string AuthorizationToken, AuthorizationVerifier;
		string AccessToken, AccessTokenSecret, AccessScreenname;
		long AccessId;
		
		public OAuth (string requestTokenUrl, string accessTokenUrl, string authorizeUrl)
		{
			this.requestTokenUrl = requestTokenUrl;
			this.accessTokenUrl = accessTokenUrl;
			this.authorizeUrl = authorizeUrl;
		}
		
		static Random random = new Random ();
		static DateTime UnixBaseTime = new DateTime (1970, 1, 1);
		HMACSHA1 hmacsha1 = new HMACSHA1 ();

		// 16-byte lower-case or digit string
		static string MakeNonce ()
		{
			var ret = new char [16];
			for (int i = 0; i < ret.Length; i++){
				int n = random.Next (35);
				if (n < 10)
					ret [i] = (char) (n + '0');
				else
					ret [i] = (char) (n-10 + 'a');
			}
			return new string (ret);
		}
		
		static string MakeTimestamp ()
		{
			return ((long) (DateTime.UtcNow - UnixBaseTime).TotalSeconds).ToString ();
		}
		
		// works around the incredibly lame OAuth spec that assumed UrlEncode used *uppercase* letters
		static string OAuthUrlEncode (string source)
		{
			var bytes = HttpUtility.UrlEncodeToBytes (source);
			
			// "upgrade" to OAuth lame requirement
			for (int i = 0; i < bytes.Length; i++){
				if (bytes [i] == '%' && i + 2 < bytes.Length){
					i++;
					bytes [i] = (byte) Char.ToUpper ((char) bytes [i]);
					i++;
					bytes [i] = (byte) Char.ToUpper ((char) bytes [i]);
				}
			}
			return Encoding.ASCII.GetString (bytes);
		}
			
		// Makes an OAuth signature out of the HTTP method, the base URI and the headers
		string MakeSignature (string method, string base_uri, Dictionary<string,string> headers)
		{
			var items = from k in headers.Keys orderby k 
				select k + "%3D" + OAuthUrlEncode (headers [k]);

			return method + "&" + OAuthUrlEncode (base_uri) + "&" + 
				string.Join ("%26", items.ToArray ());
		}
		
		string MakeSigningKey (string consumerSecret, string oauthTokenSecret)
		{
			return OAuthUrlEncode (consumerSecret) + "&" + (oauthTokenSecret != null ? OAuthUrlEncode (oauthTokenSecret) : "");
		}
		
		string MakeOAuthSignature (string compositeSigningKey, string signatureBase)
		{
			hmacsha1.Key = Encoding.UTF8.GetBytes (compositeSigningKey);
			return Convert.ToBase64String (hmacsha1.ComputeHash (Encoding.UTF8.GetBytes (signatureBase)));
		}
		
		public bool AcquireRequestToken ()
		{
			var headers = new Dictionary<string,string> () {
				{ "oauth_callback", OAuthUrlEncode (Callback) },
				{ "oauth_consumer_key", ConsumerKey },
				{ "oauth_nonce", MakeNonce () },
				{ "oauth_signature_method", "HMAC-SHA1" },
				{ "oauth_timestamp", MakeTimestamp () },
				{ "oauth_version", "1.0" }};
				
			string signature = MakeSignature ("POST", requestTokenUrl, headers);
			var compositeSigningKey = MakeSigningKey (ConsumerSecret, null);
			
			// Compute oauth_signature
			//hmacsha1.Key = Encoding.UTF8.GetBytes (compositeSigningKey);
			//Convert.ToBase64String (hmacsha1.ComputeHash (Encoding.UTF8.GetBytes (signature)));
			var oauth_signature = MakeOAuthSignature (compositeSigningKey, signature);
			
			var wc = new WebClient ();
			headers.Add ("oauth_signature", OAuthUrlEncode (oauth_signature));
			var oheaders = String.Join (",", (from x in headers.Keys select String.Format ("{0}=\"{1}\"", x, headers [x])).ToArray ());
			wc.Headers [HttpRequestHeader.Authorization] = "OAuth " + oheaders;
			
			try {
				var result = HttpUtility.ParseQueryString (wc.UploadString (new Uri (requestTokenUrl), ""));

				if (result ["oauth_callback_confirmed"] != null){
					RequestToken = result ["oauth_token"];
					RequestTokenSecret = result ["oauth_token_secret"];
					
					return true;
				}
			} catch (Exception e) {
				Console.WriteLine (e);
				// fallthrough for errors
			}
			return false;
		}
		
		// Invoked after the user has authorized us
		public bool AcquireAccessToken ()
		{
			var headers = new Dictionary<string,string> () {
				{ "oauth_consumer_key", ConsumerKey },
				{ "oauth_nonce", MakeNonce () },
				{ "oauth_signature_method", "HMAC-SHA1" },
				{ "oauth_timestamp", MakeTimestamp () },
				{ "oauth_version", "1.0" }};
			var content = "";
			if (xAuthUsername == null){
				headers.Add ("oauth_token", AuthorizationToken);
				headers.Add ("oauth_verifier", AuthorizationVerifier);
			} else {
				headers.Add ("x_auth_username", xAuthUsername);
				headers.Add ("x_auth_password", xAuthPassword);
				headers.Add ("x_auth_mode", "client_auth");
				content = String.Format ("x_auth_mode=client_auth&x_auth_password={0}&x_auth_username={1}", xAuthPassword, xAuthUsername);
			}
			
			string signature = MakeSignature ("POST", accessTokenUrl, headers);
			var compositeSigningKey = MakeSigningKey (ConsumerSecret, RequestTokenSecret);
			var oauth_signature = MakeOAuthSignature (compositeSigningKey, signature);
			
			var wc = new WebClient ();
			headers.Add ("oauth_signature", OAuthUrlEncode (oauth_signature));
			if (xAuthUsername != null){
				headers.Remove ("x_auth_username");
				headers.Remove ("x_auth_password");
				headers.Remove ("x_auth_mode");
			}
			var oheaders = String.Join (",", (from x in headers.Keys select String.Format ("{0}=\"{1}\"", x, headers [x])).ToArray ());
			wc.Headers [HttpRequestHeader.Authorization] = "OAuth " + oheaders;
			
			try {
				var result = HttpUtility.ParseQueryString (wc.UploadString (new Uri (accessTokenUrl), content));

				if (result ["oauth_token"] != null){
					AccessToken = result ["oauth_token"];
					AccessTokenSecret = result ["oauth_token_secret"];
					AccessScreenname = result ["screen_name"];
					AccessId = Int64.Parse (result ["user_id"]);
					
					return true;
				}
			} catch (Exception e) {
				Console.WriteLine (e);
				// fallthrough for errors
			}
			return false;
		}
			
		class AuthorizationViewController : WebViewController {
			OAuth oauth;
			string url;
			public AuthorizationViewController (OAuth oauth, string url)
			{
				this.url = url;
				this.oauth = oauth;
				NavigationItem.Title = "Login to Twitter";
				NavigationItem.LeftBarButtonItem = new UIBarButtonItem (UIBarButtonSystemItem.Cancel, delegate {
					DismissModalViewControllerAnimated (false);
				});
			}
				  
			public override void ViewWillAppear (bool animated)
			{
				SetupWeb ();
				WebView.ShouldStartLoad = LoadHook;
				WebView.LoadRequest (new NSUrlRequest (new NSUrl (url)));
				base.ViewWillAppear (animated);
			}
			
			bool LoadHook (UIWebView sender, NSUrlRequest request, UIWebViewNavigationType navType)
			{
				var requestString = request.Url.ToString ();
				if (requestString.StartsWith (oauth.Callback)){
					var results = HttpUtility.ParseQueryString (requestString.Substring (oauth.Callback.Length+1));
					
					oauth.AuthorizationToken = results ["oauth_token"];
					oauth.AuthorizationVerifier = results ["oauth_verifier"];
					DismissModalViewControllerAnimated (false);
					
					oauth.AcquireAccessToken ();
				}
				return true;
			}
		}
		
		public void AuthorizeUser (UIViewController parent)
		{
			var authweb = new AuthorizationViewController (this, authorizeUrl + "?oauth_token=" + RequestToken);
			
			parent.PresentModalViewController (authweb, true);
		}
	}

	public class TwitterOAuth : OAuth {
		public TwitterOAuth () : base ("https://api.twitter.com/oauth/request_token", "https://twitter.com/oauth/access_token", "https://twitter.com/oauth/authorize")
		{
		}
		
	}
}

