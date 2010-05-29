//
// Conversation.cs: UI elements for showing conversations
//
// Author: 
//    Miguel de Icaza (miguel@gnome.org)
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
		Section convSection;
		
		public ConversationViewController (Tweet source) : base (null, true)
		{
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

