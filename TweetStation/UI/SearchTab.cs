//
// The page that shows the various search options
// Search, Nearby, User, saved searches and trending topics
//
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
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Linq;
using System.Net;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using System.Web;
using System.Drawing;
using MonoTouch.CoreLocation;

namespace TweetStation
{
	// 
	// The main entry point for searches in the application, it
	// dispatches to various nested views
	//
	public class SearchesViewController : DialogViewController {
		TwitterAccount account;
		Section savedSearches, trends, lists;
		
		public SearchesViewController () : base (null) {}

		public TwitterAccount Account {
			get {
				return account;
			}
			
			set {
				if (account == value)
					return;
				
				account = value;
				ReloadAccount ();
			}
		}
		
		void ReloadAccount ()
		{
			lists = new Section (Locale.GetText ("Lists")) {
				new StringElement (Locale.GetText ("New list"), delegate { EditList (null, new ListDefinition ()); })
			};
			
			Root = new RootElement (Locale.GetText ("Search")) {
				new Section () {
					new RootElement (Locale.GetText ("Search"), x => new TwitterTextSearch ()),
#if true
					new LoadMoreElement (Locale.GetText ("Nearby"), Locale.GetText ("Finding your position"), x => StartGeoSearch (x)) {
						Accessory = UITableViewCellAccessory.DisclosureIndicator,
						Alignment = UITextAlignment.Left
					},
#endif
					new RootElement (Locale.GetText ("Go to User"), x => new SearchUser ())
				},
				lists,
			};
		}

		CLLocation location = null;
		
		void StartGeoSearch (LoadMoreElement loading)
		{
			if (location == null){
				loading.Animating = true;
				Util.RequestLocation (newLocation => {
					loading.Animating = false;
					if (newLocation != null){
						location = newLocation;
						ActivateController (new SearchFromGeo (location));
					}
				});
			} else 
				ActivateController (new SearchFromGeo (location));
		}
		
		bool SearchResultsAreRecent {
			get {
				long lastTime;
				return Int64.TryParse (Util.Defaults.StringForKey ("searchLoadedTime"), out lastTime) && (DateTime.UtcNow.Ticks - lastTime) < TimeSpan.FromHours (24).Ticks;
			}
		}
		
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			
			if (savedSearches != null)
				return;
			
			FetchLists ();
			
			if (SearchResultsAreRecent){
				InsertCachedSearchResults ();
				FetchTrends ();
			} else {
				account.Download ("http://api.twitter.com/1/saved_searches.json", result => {
					if (result != null)
						LoadSearches (result);
				});
			}
		}
		
		void LoadSearches (byte [] result)
		{
			try {
				var json = JsonValue.Load (new MemoryStream (result));
	
				for (int i = 0; i < json.Count; i++)
					Util.Defaults.SetString (json [i]["query"], "s-" + i);
				
				Util.Defaults.SetString (DateTime.UtcNow.Ticks.ToString (), "searchLoadedTime");
				InsertCachedSearchResults ();
			} catch (Exception e){
				Console.WriteLine (e);
			}
			FetchTrends ();
		}
		
		void InsertCachedSearchResults ()
		{
			savedSearches = new Section ("Saved searches"){
				GetCachedResults ()
			};
			Root.Add (savedSearches);
		}
		
		IEnumerable<Element> GetCachedResults ()
		{
			for (int i = 0; i < 10; i++){
				var value = Util.Defaults.StringForKey ("s-" + i);
				if (value == null)
					yield break;
				yield return new SearchElement (value, value);
			}
		}		
		
		// 
		// Queues a request to fetch the trends, and adds a new section to the root
		//
		void FetchTrends ()
		{
			account.Download ("http://search.twitter.com/trends/current.json", result => {
				try {
					var json = JsonValue.Load (new MemoryStream (result));
					var jroot = (JsonObject) json ["trends"];
					var jtrends = jroot.Values.FirstOrDefault (); 
					
					trends = new Section ("Trends");
					
					for (int i = 0; i < jtrends.Count; i++)
						trends.Add (	new SearchElement (jtrends [i]["name"], jtrends [i]["query"]));
					Root.Add (trends);
				} catch (Exception e){
					Console.WriteLine (e);
				}
			});
		}
		
		//
		// Queues a request to fetch the lists, and inserts
		// the results into the existing section
		//
		void FetchLists ()
		{
			account.Download ("http://api.twitter.com/1/" + account.Username + "/lists.json", res => {
				if (res == null)
					return;
				
				var json = JsonValue.Load (new MemoryStream (res));
				var jlists = json ["lists"];
				try {
					int pos = 0;
					foreach (JsonObject list in jlists){
						string name = list ["full_name"];
						string listname = list ["name"];
						string url = "http://api.twitter.com/1/" + account.Username + "/lists/" + listname + "/statuses.json";
						lists.Insert (pos++, UITableViewRowAnimation.Fade, TimelineRootElement.MakeList (name, listname, url));
					}
				} catch (Exception e){
					Console.WriteLine (e);
				}
			});
		}		public enum Privacy {
			Public, Private
		}
		
		public class ListDefinition {
			public string Name;
			public string Description;
			public Privacy Privacy;
		}
		
		public void EditList (string originalName, ListDefinition list)
		{
			var editor = new DialogViewController (null, true);
			var name = new EntryElement ("Name", null, list.Name);
			name.Changed += delegate {
				editor.NavigationItem.RightBarButtonItem.Enabled = !String.IsNullOrEmpty (name.Value);
			};
			var description = new EntryElement ("Description", "optional", list.Description);
			var privacy = new RootElement ("Privacy", new RadioGroup ("key", (int) list.Privacy)){
				new Section () {
					new RadioElement ("Public"),
					new RadioElement ("Private")
				}
			};
			editor.NavigationItem.SetLeftBarButtonItem (new UIBarButtonItem (UIBarButtonSystemItem.Cancel, delegate {
				DeactivateController (true);
			}), false);
			                                     
			editor.NavigationItem.SetRightBarButtonItem (new UIBarButtonItem (UIBarButtonSystemItem.Save, delegate {
				string url = String.Format ("http://api.twitter.com/1/{0}/lists{1}.json?name={2}&mode={3}{4}", 
				                            TwitterAccount.CurrentAccount.Username,
				                            originalName == null ? "" : "/" + originalName,
				                            OAuth.PercentEncode (name.Value),
				                            privacy.RadioSelected == 0 ? "public" : "private",
				                            description.Value == null ? "" : "&description=" + OAuth.PercentEncode (description.Value));
				TwitterAccount.CurrentAccount.Post (url, "");
				
			}), false);

			editor.NavigationItem.RightBarButtonItem.Enabled = !String.IsNullOrEmpty (name.Value);
			editor.Root = new RootElement ("New List") {
				new Section () { name, description, privacy }
			};
			ActivateController (editor);
		}		
	}
}