//
// Conversation.cs: UI elements for showing conversations
//
// Author: 
//    Miguel de Icaza (miguel@gnome.org)
//
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using MonoTouch.Dialog;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace TweetStation
{
	// 
	// Displays a conversation
	//
	public class ConversationViewController : DialogViewController
	{
		Tweet source;
		Section convSection;
		
		public ConversationViewController (Tweet source) : base (null, true)
		{
			this.source = source;
		
			convSection = new Section ();
			ProcessConversation (source);
			Root = new RootElement ("Conversation") {
				convSection
			};
		}

		/// <summary>
		///   Loads as many tweets as possible from the database, and if they are missing
		///   requests them from the service.
		/// </summary>
		void ProcessConversation (Tweet previous)
		{
			convSection.Add (new TweetElement (previous));
			
			while (previous != null && previous.InReplyToStatus != 0) {
				var lookup = Tweet.FromId (previous.InReplyToStatus);
				if (lookup == null){
					QueryServer (previous.InReplyToStatus);
					return;
				} else
					convSection.Add (new TweetElement (lookup));
				previous = lookup;
			} 
			EndConversation ();
		}
		
		void QueryServer (long reply)
		{
			Tweet.LoadFullTweet (reply, tweet => {
				if (tweet == null){
					EndConversation ();
					return;
				}
				
				// Insert the results and continue processing.
				ProcessConversation (tweet);
			});
		}
		
		void EndConversation ()
		{
			convSection.Add (new StringElement ("End of conversation"));
		}			
	}
	
	// 
	// This MonoTouch.Dialog.Element will create a conversation DialogViewController.
	//
	public class ConversationRootElement : RootElement {
		Tweet source;
		
		public ConversationRootElement (string caption, Tweet source) : base (caption)
		{
			this.source = source;
		}
		
		protected override UIViewController MakeViewController ()
		{
			return new ConversationViewController (source);
		}
	}
}

