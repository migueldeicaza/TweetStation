using System;
using MonoTouch.Dialog;
using MonoTouch.UIKit;

namespace TweetStation
{
	public class Settings : DialogViewController
	{
		public Settings () : base (UITableViewStyle.Grouped, null)
		{
			Root = new RootElement ("Settings"){
				new Section ("Accounts"){
					new RootElement ("Current")
				},
				new Section (){
					new RootElement ("Display"),
					new RootElement ("Services")
				},
				new Section (){
					new RootElement ("About"){
						new Section (){
							new StringElement ("Version")
						},
						new Section (){
							new RootElement ("@migueldeicaza", delegate { return new FullProfileView ("migueldeicaza"); }),
							new RootElement ("@icluck", delegate { return new FullProfileView ("icluck"); }),
						},
						new Section (){
							new StringElement ("Credits")
						},
						new Section () {
							new HtmlElement ("Web site", "http://tirania.org/tweetstation")
						}
					}
				}
			};
		}
	}
}

