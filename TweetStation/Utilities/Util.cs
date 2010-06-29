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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using MonoTouch.Dialog;
using MonoTouch.CoreLocation;
using System.Globalization;

namespace TweetStation
{
	public static class Util
	{
		/// <summary>
		///   A shortcut to the main application
		/// </summary>
		public static UIApplication MainApp = UIApplication.SharedApplication;
		public static AppDelegate MainAppDelegate = UIApplication.SharedApplication.Delegate as AppDelegate;
		
		public readonly static string BaseDir = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), "..");

		//
		// Since we are a multithreaded application and we could have many
		// different outgoing network connections (api.twitter, images,
		// searches) we need a centralized API to keep the network visibility
		// indicator state
		//
		static object networkLock = new object ();
		static int active;
		
		public static void PushNetworkActive ()
		{
			lock (networkLock){
				active++;
				MainApp.NetworkActivityIndicatorVisible = true;
			}
		}
		
		public static void PopNetworkActive ()
		{
			lock (networkLock){
				active--;
				if (active == 0)
					MainApp.NetworkActivityIndicatorVisible = false;
			}
		}
		
		public static DateTime LastUpdate (string key)
		{
			var s = Defaults.StringForKey (key);
			if (s == null)
				return DateTime.MinValue;
			long ticks;
			if (Int64.TryParse (s, out ticks))
				return new DateTime (ticks, DateTimeKind.Utc);
			else
				return DateTime.MinValue;
		}
		
		public static bool NeedsUpdate (string key, TimeSpan timeout)
		{
			return DateTime.UtcNow - LastUpdate (key) > timeout;
		}
		
		public static void RecordUpdate (string key)
		{
			Defaults.SetString (key, DateTime.UtcNow.Ticks.ToString ());
		}
			
		
		public static NSUserDefaults Defaults = NSUserDefaults.StandardUserDefaults;
				
		const long TicksOneDay = 864000000000;
		const long TicksOneHour = 36000000000;
		const long TicksMinute = 600000000;
		
		static string s1 = Locale.GetText ("1 sec");
		static string sn = Locale.GetText (" secs");
		static string m1 = Locale.GetText ("1 min");
		static string mn = Locale.GetText (" mins");
		static string h1 = Locale.GetText ("1 hour");
		static string hn = Locale.GetText (" hours");
		static string d1 = Locale.GetText ("1 day");
		static string dn = Locale.GetText (" days");
		
		public static string FormatTime (TimeSpan ts)
		{
			int v;
			
			if (ts.Ticks < TicksMinute){
				v = ts.Seconds;
				if (v <= 1)
					return s1;
				else
					return v + sn;
			} else if (ts.Ticks < TicksOneHour){
				v = ts.Minutes;
				if (v == 1)
					return m1;
				else
					return v + mn;
			} else if (ts.Ticks < TicksOneDay){
				v = ts.Hours;
				if (v == 1)
					return h1;
				else
					return v + hn;
			} else {
				v = ts.Days;
				if (v == 1)
					return d1;
				else
					return v + dn;
			}
		}
		
		public static string StripHtml (string str)
		{
			if (str.IndexOf ('<') == -1)
				return str;
			var sb = new StringBuilder ();
			for (int i = 0; i < str.Length; i++){
				char c = str [i];
				if (c != '<'){
					sb.Append (c);
					continue;
				}
				
				for (i++; i < str.Length; i++){
					c =  str [i];
					if (c == '"' || c == '\''){
						var last = c;
						for (i++; i < str.Length; i++){
							c = str [i];
							if (c == last)
								break;
							if (c == '\\')
								i++;
						}
					} else if (c == '>')
						break;
				}
			}
			return sb.ToString ();
		}
		
		public static string CleanName (string name)
		{
			if (name.Length == 0)
				return "";
			
			bool clean = true;
			foreach (char c in name){
				if (Char.IsLetterOrDigit (c) || c == '_')
					continue;
				clean = false;
				break;
			}
			if (clean)
				return name;
			
			var sb = new StringBuilder ();
			foreach (char c in name){
				if (!Char.IsLetterOrDigit (c))
					break;
				
				sb.Append (c);
			}
			return sb.ToString ();
		}
		
		public static RootElement MakeProgressRoot (string caption)
		{
			return new RootElement (caption){
				new Section (){
					new ActivityElement ()
				}
			};
		}
		
		public static RootElement MakeError (string diagMsg)
		{
			return new RootElement ("Error"){
				new Section ("Error"){
					new MultilineElement ("Unable to retrieve the information")
				}
			};
		}
		
		static long lastTime;
		[Conditional ("TRACE")]
		public static void ReportTime (string s)
		{
			long now = DateTime.UtcNow.Ticks;
			
			Console.WriteLine ("[{0}] ticks since last invoke: {1}", s, now-lastTime);
			lastTime = now;
		}
		
		[Conditional ("TRACE")]
		public static void Log (string format, params object [] args)
		{
			Console.WriteLine (String.Format (format, args));
		}
		
		static UIActionSheet sheet;
		public static UIActionSheet GetSheet (string title)
		{
			sheet = new UIActionSheet (title);
			return sheet;
		}
		
		static CultureInfo americanCulture;
		public static CultureInfo AmericanCulture {
			get {
				if (americanCulture == null)
					americanCulture = new CultureInfo ("en-US");
				return americanCulture;
			}
		}
		#region Location
		
		internal class MyCLLocationManagerDelegate : CLLocationManagerDelegate {
			Action<CLLocation> callback;
			
			public MyCLLocationManagerDelegate (Action<CLLocation> callback)
			{
				this.callback = callback;
			}
			
			public override void UpdatedLocation (CLLocationManager manager, CLLocation newLocation, CLLocation oldLocation)
			{
				manager.StopUpdatingLocation ();
				locationManager = null;
				callback (newLocation);
			}
			
			public override void Failed (CLLocationManager manager, NSError error)
			{
				callback (null);
			}
			
		}

		static CLLocationManager locationManager;
		static public void RequestLocation (Action<CLLocation> callback)
		{
			locationManager = new CLLocationManager () {
				DesiredAccuracy = CLLocation.AccuracyBest,
				Delegate = new MyCLLocationManagerDelegate (callback),
				DistanceFilter = 1000f
			};
			if (CLLocationManager.LocationServicesEnabled)
				locationManager.StartUpdatingLocation ();
		}	
		#endregion
	}
}
