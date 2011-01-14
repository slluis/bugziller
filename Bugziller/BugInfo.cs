// 
// Bug.cs
//  
// Author:
//       Lluis Sanchez Gual <lluis@novell.com>
// 
// Copyright (c) 2010 Novell, Inc (http://www.novell.com)
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
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Bugziller
{
	[Serializable]
	public class BugInfo
	{
		public static string[] Severities = new string [] { "Critical", "Major", "Normal", "Minor", "Enhancement" };
		static string[] emptyTags = new string [0];
		
		List<CommentInfo> comments;
		string[] tags;
		
		[OptionalField]
		bool requiresRefresh;

		public BugInfo ()
		{
		}
		
		public int Id { get; set; }
		
		public string Summary { get; set; }
		
		public string Assignee { get; set; }
		
		public string Status { get; set; }
		
		public string Severity { get; set; }
		
		public int LocalPriority { get; set; }
		
		public DateTime DateCreated { get; set; }
		
		public string Component { get; set; }
		
		public string OperatingSystem { get; set; }
		
		public int AutoPriority {
			get {
				return Array.IndexOf (Severities, Severity);
			}
		}
		
		public string TargetMilestone { get; set; }
		
		public bool IsNew { get; set; }
		
		public bool RequiresRefresh {
			get {
				return this.requiresRefresh;
			}
			set {
				requiresRefresh = value;
			}
		}
		
		public string[] Tags {
			get {
				return tags ?? emptyTags;
			}
		}
		
		public void AddTag (string tag)
		{
			if (tags == null)
				tags = new string [] { tag };
			else {
				Array.Resize (ref tags, tags.Length + 1);
				tags [tags.Length - 1] = tag;
			}
		}
		
		public void RemoveTag (string tag)
		{
			if (tags == null)
				return;
			List<string> newTags = new List<string> (Tags);
			newTags.Remove (tag);
			if (newTags.Count > 0)
				tags = newTags.ToArray ();
			else
				tags = null;
		}
		
		public bool HasTag (string tag)
		{
			return tags != null && ((IList<string>)tags).Contains (tag);
		}
		
		public void ClearTags ()
		{
			tags = null;
		}
		
		public List<CommentInfo> Comments {
			get {
				if (comments == null)
					comments = new List<CommentInfo> ();
				return comments; 
			}
		}
		
	}
	
	[Serializable]
	public class CommentInfo
	{
		public string Text { get; set; }
		public string Author { get; set; }
		public DateTime Time { get; set; }
		public bool IsPrivate { get; set; }
		
		public AttachmentInfo Attachment { get; set; }
	}
	
	[Serializable]
	public class AttachmentInfo
	{
		public DateTime CreationTime { get; set; }
		public DateTime LastChangeTime { get; set; }
		public string FileName { get; set; }
		public string Description { get; set; }
		public string ContentType { get; set; }
		public bool IsPrivate { get; set; }
		public bool IsObsolete { get; set; }
		public bool IsUrl { get; set; }
		public bool IsPatch { get; set; }
		public string Attacher { get; set; }
	}
}

