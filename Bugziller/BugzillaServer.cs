// 
// BugzillaServer.cs
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
using System.Linq;
using System.Collections.Generic;
using Bugzproxy;
using MonoDevelop.Core;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using MonoDevelop.Ide;
using Bugzproxy.ProxyStructs;
using System.Runtime.Serialization;

namespace Bugziller
{
	[Serializable]
	public class BugzillaServer
	{
		[NonSerialized]
		object connectionLock = new object ();
		
		[NonSerialized]
		Server server;
		
		Dictionary<int,BugInfo> bugs = new Dictionary<int, BugInfo> ();
		List<BugInfo> orderedBugs = new List<BugInfo> ();
		DateTime lastUpdate;
		[OptionalField]
		bool loaded;
		List<BugTag> tags = new List<BugTag> ();
		
		[NonSerialized]
		string oldName;
		
		int id;
		string name;
		string product = "";
		string host = "";
		bool useSSL = false;
		string user = "";
		string password = "";
		
		public BugzillaServer ()
		{
			HighPriorityLevel = 20;
			MedPriorityLevel = 40;
			LowPriorityLevel = 60;
			
			System.Net.ServicePointManager.ServerCertificateValidationCallback = delegate {
				return true;
			};
			
			Tags.Add (new BugTag ("OnHold", new Gdk.Color (140, 140, 140)));
			Tags.Add (new BugTag ("MacBug", new Gdk.Color (140, 140, 140)));
			Tags.Add (new BugTag ("NeedInfo", new Gdk.Color (140, 140, 223)));
		}
		
		public static BugzillaServer Load (int id)
		{
			string file = BugzillaService.BugzillaDataPath.Combine (id.ToString ());
			if (File.Exists (file)) {
				using (Stream fs = File.OpenRead (file)) {
					BinaryFormatter f = new BinaryFormatter ();
					BugzillaServer data = (BugzillaServer) f.Deserialize (fs);
					data.oldName = data.Name;
					data.connectionLock = new object ();
					if (data.id == 0)
						data.id = 1;
					return data;
				}
			}
			
			throw new Exception ("Bugzilla data not found");
		}
		
		public string Host {
			get { return host; }
			set { host = value; }
		}
		
		public string User {
			get { return user; }
			set { user = value; }
		}
		
		public string Password {
			get {
				return this.password;
			}
			set {
				password = value;
			}
		}
		
		public bool UseSSL {
			get {
				return this.useSSL;
			}
			set {
				useSSL = value;
			}
		}
		
		public int HighPriorityLevel { get; set; }
		
		public int MedPriorityLevel { get; set; }
		
		public int LowPriorityLevel { get; set; }
		
		public int Id {
			get {
				return this.id;
			}
			set {
				id = value;
			}
		}

		public string Name {
			get {
				return this.name;
			}
			set {
				name = value;
			}
		}
		
		
		public List<BugTag> Tags {
			get { return tags; }
		}
		
		public Gdk.Color GetTagColor (string tag)
		{
			foreach (var b in tags) {
				if (b.Name == tag)
					return b.Color;
			}
			return Gdk.Color.Zero;
		}
		
		public void Save ()
		{
			if (Id == 0)
				throw new InvalidOperationException ("Server not registered in BugzillaService");
			if (!Directory.Exists (BugzillaService.BugzillaDataPath))
				Directory.CreateDirectory (BugzillaService.BugzillaDataPath);
			string file = BugzillaService.BugzillaDataPath.Combine (Id.ToString ());
			file = Path.GetFullPath (file);
			string tmpFile = file + ".tmp";
			using (Stream fs = File.OpenWrite (tmpFile)) {
				BinaryFormatter f = new BinaryFormatter ();
				f.Serialize (fs, this);
			}
			FileService.SystemRename (tmpFile, file);
			if (oldName != Name)
				BugzillaService.SaveIndex ();
			oldName = Name;
		}
		
		void Connect ()
		{
			lock (connectionLock) {
				if (server != null)
					return;
				server = new Server (host, useSSL);
				server.Login (user, password, false);
			}
		}
		
		public string Product {
			get { return product; }
			set { product = value; }
		}
		
		public void OpenBug (BugInfo b)
		{
			string url = useSSL ? "https" : "http";
			url += "://" + host + "/show_bug.cgi?id=" + b.Id;
			DesktopService.ShowUrl (url);
		}
		
		public IAsyncOperation UpdateBugs (IProgressMonitor monitor, IEnumerable<BugInfo> bugs)
		{
			System.Threading.ThreadPool.QueueUserWorkItem (delegate {
				monitor.BeginTask ("Updating from bugzilla server", 1);
				try {
					Connect ();
					List<int> ids = new List<int>();
					foreach (var b in bugs)
						ids.Add (b.Id);
					Bug[] data = server.GetBugs (ids.ToArray ());
					int nb, mb;
					UpdateBugData (data, out nb, out mb);
					Save ();
					monitor.ReportSuccess (string.Format ("Bug list updated ({0} added, {1} modified)", nb, mb));
				} catch (Exception ex) {
					monitor.ReportError ("Update failed", ex);
				} finally {
					monitor.Dispose ();
				}
			});
			return monitor.AsyncOperation;
		}
		
		public IAsyncOperation Update (IProgressMonitor monitor)
		{
			System.Threading.ThreadPool.QueueUserWorkItem (delegate {
				string[] statuses;
				if (InitialUpdate)
					statuses = new string [] { "NEW", "ASSIGNED" , "NEEDINFO"};
				else
					statuses = new string [] { "NEW", "ASSIGNED" , "NEEDINFO", "RESOLVED", "CLOSED", "VERIFIED" };
				string[] severities = new string [] { "Critical", "Major", "Normal", "Minor", "Enhancement" };
				DateTime t = DateTime.Now;
				monitor.BeginTask ("Updating from bugzilla server", 3);
				try {
					Connect ();
					int nb, mb;
					
					// Bugs
					int lastWork = -1;
					Bug[] data = server.GetBugsForProduct (Product, statuses, severities, lastUpdate, delegate (int total, int current) {
						if (lastWork == -1) {
							monitor.BeginStepTask ("Getting bug data", total, 2);
							lastWork = 0;
						}
						monitor.Step (current - lastWork);
						lastWork = current;
					});
					monitor.EndTask ();
					UpdateBugData (data, out nb, out mb);
					lastUpdate = t;
					monitor.Step (1);
					Save ();
					monitor.Step (1);
					monitor.ReportSuccess (string.Format ("Bug list updated ({0} added, {1} modified)", nb, mb));
				} catch (Exception ex) {
					monitor.ReportError ("Update failed", ex);
				} finally {
					monitor.Dispose ();
				}
			});
			return monitor.AsyncOperation;
		}
		
		void UpdateBugData (Bug[] data, out int newCount, out int modCount)
		{
			newCount = 0;
			modCount = 0;
			bool wasInitialUpdate = InitialUpdate;
			
			Dictionary<int, BugComment[]> comments = null;
			if (!InitialUpdate) {
				int[] ids = new int [data.Length];
				for (int n=0; n<data.Length; n++)
					ids [n] = data[n].Id;
				try {
					comments = server.GetComments (ids, true);
				} catch (Exception ex) {
					Console.WriteLine (ex);
				}
				BugHistory[] history = server.GetBugHistory (ids);
				ProcessHistory (data, history);
			} else {
				comments = null;
			}
			
			lock (bugs) {
				foreach (Bug b in data) {
					BugInfo bi;
					if (bugs.TryGetValue (b.Id, out bi))
						modCount++;
					else {
						newCount++;
						Console.WriteLine ("ppnwe:" + b.Id);
					}
					BugComment[] coms = null;
					if (comments != null)
						comments.TryGetValue (b.Id, out coms);
					UpdateBug (b, coms);
				}
				orderedBugs.Sort (0, newCount, new BugComparer ());
				UpdateIndexes ();
			}
			if (wasInitialUpdate) {
				HighPriorityLevel = 20;
				MedPriorityLevel = 40;
				LowPriorityLevel = 60;
			}
		}

		void ProcessHistory (Bug[] data, BugHistory[] history)
		{
			Dictionary<int,Bug> dict = new Dictionary<int, Bug>();
			foreach (Bug b in data)
				dict [b.Id] = b;
			foreach (BugHistory h in history) {
				Bug b;
				if (dict.TryGetValue (h.id, out b)) {
					foreach (BugHistoryData hd in h.history) {
						foreach (BugChange bc in hd.changes) {
							if (bc.field_name == "target_milestone") {
								b.TargetMilestone = bc.added;
							}
						}
					}
				}
			}
		}

		
		void UpdateBug (Bug b, BugComment[] comments)
		{
			BugInfo bi;
			if (!bugs.TryGetValue (b.Id, out bi)) {
				bi = new BugInfo ();
				bi.Id = b.Id;
				bugs [b.Id] = bi;
				int i = 0;
				for (; i<orderedBugs.Count && orderedBugs[i].IsNew; i++);
				orderedBugs.Insert (i, bi);
				HighPriorityLevel++;
				MedPriorityLevel++;
				LowPriorityLevel++;
			}
			bi.IsNew = true;
			bi.Summary = b.Summary;
			bi.Assignee = b.AssignedTo;
			bi.Severity = b.Severity;
			bi.Status = b.Status;
			bi.TargetMilestone = b.TargetMilestone;
			bi.DateCreated = b.Created;
			bi.Component = b.Component;
			bi.OperatingSystem = b.OperatingSystem;
			bi.Comments.Clear ();
			
			if (comments != null) {
				foreach (BugComment c in comments) {
					CommentInfo ci = new CommentInfo () {
						Author = c.author,
						Text = c.text,
						Time = c.time,
						IsPrivate = c.is_private
					};
					
					if (c.attachment_id != 0) {
						AttachmentInfo at = new AttachmentInfo () {
							Attacher = c.attachment.attacher,
							ContentType = c.attachment.content_type,
							CreationTime = c.attachment.creation_time,
							Description = c.attachment.description,
							FileName =  c.attachment.file_name,
							IsObsolete = c.attachment.is_obsolete,
							IsPatch = c.attachment.is_patch,
							IsPrivate = c.attachment.is_private,
							IsUrl = c.attachment.is_url,
							LastChangeTime = c.attachment.last_change_time
						};
						ci.Attachment = at;
					}
					
					bi.Comments.Add (ci);
				}
			} else
				bi.RequiresRefresh = true;
		}
		
		bool InitialUpdate {
			get { return bugs.Count == 0; }
		}
		
		class BugComparer: IComparer<BugInfo>
		{
			public int Compare (BugInfo x, BugInfo y)
			{
				return x.AutoPriority.CompareTo (y.AutoPriority);
			}
		}
		
		public ICollection<BugInfo> GetBugs ()
		{
			lock (bugs) {
				return new List<BugInfo> (orderedBugs);
			}
		}
		
		public void SetOrder (int order, List<BugInfo> list)
		{
			BugInfo ch = FindPreviousNotIncluded (HighPriorityLevel, list);
			BugInfo cm = FindPreviousNotIncluded (MedPriorityLevel, list);
			BugInfo cl = FindPreviousNotIncluded (LowPriorityLevel, list);
			
			BugInfo refb = orderedBugs [order];
			if (list.Contains (refb))
				return;
			foreach (var b in list) {
				b.IsNew = false;
				orderedBugs.Remove (b);
			}
			int i = orderedBugs.IndexOf (refb);
			orderedBugs.InsertRange (i, list);
			UpdateIndexes ();
			
			HighPriorityLevel = ch != null ? ch.LocalPriority : -1;
			MedPriorityLevel = cm != null ? cm.LocalPriority : -1;
			LowPriorityLevel = cl != null ? cl.LocalPriority : -1;
			
			if (MedPriorityLevel < HighPriorityLevel)
				MedPriorityLevel = HighPriorityLevel;
			if (LowPriorityLevel < MedPriorityLevel)
				LowPriorityLevel = MedPriorityLevel;
		}
		
		int GetLevel (Priority p)
		{
			switch (p) {
			case Priority.High: return HighPriorityLevel;
			case Priority.Medium: return MedPriorityLevel;
			case Priority.Low: return LowPriorityLevel;
			}
			throw new NotSupportedException ();
		}
		
		void SetLevel (Priority p, int i)
		{
			switch (p) {
			case Priority.High: HighPriorityLevel = i; break;
			case Priority.Medium: MedPriorityLevel = i; break;
			case Priority.Low: LowPriorityLevel = i; break;
			}
		}
		
		public void SetPriority (Priority priority, bool atTop, List<BugInfo> list)
		{
			BugInfo[] cbugs = new BugInfo [3];
			for (int n=0; n<3; n++)
				cbugs [n] = FindPreviousNotIncluded (GetLevel ((Priority)n), list);
			
			foreach (var b in list) {
				b.IsNew = false;
				orderedBugs.Remove (b);
			}
			
			foreach (BugInfo b in list) {
				
				if (atTop) {
					int n = (int)priority;
					int pos = 0;
					while (--n >= 0) {
						BugInfo refb = cbugs [n];
						if (refb != null) {
							int i = orderedBugs.IndexOf (refb);
							pos = i + 1;
							break;
						}
					}
					orderedBugs.Insert (pos, b);
				} else {
					BugInfo refb = cbugs [(int)priority];
					int i = orderedBugs.IndexOf (refb);
					orderedBugs.Insert (i + 1, b);
					cbugs [(int)priority] = b;
				}
			}
			UpdateIndexes ();
			
			for (int n=0; n<3; n++)
				SetLevel ((Priority)n, cbugs[n] != null ? cbugs[n].LocalPriority : -1);
			
			if (MedPriorityLevel < HighPriorityLevel)
				MedPriorityLevel = HighPriorityLevel;
			if (LowPriorityLevel < MedPriorityLevel)
				LowPriorityLevel = MedPriorityLevel;
		}
		
		BugInfo FindPreviousNotIncluded (int pos, List<BugInfo> list)
		{
			while (pos >= 0 && list.Contains (orderedBugs[pos]))
				pos--;
			return pos != -1 ? orderedBugs[pos] : null;
		}
		
		void UpdateIndexes ()
		{
			lock (bugs) {
				for (int i = 0; i < orderedBugs.Count; i++)
					orderedBugs [i].LocalPriority = i;
			}
		}
	}
	
	[Serializable]
	public class BugTag: ISerializable
	{
		public BugTag (string name, Gdk.Color color)
		{
			this.Name = name;
			this.Color = color;
		}
		
		internal BugTag (SerializationInfo info, StreamingContext context)
		{
			Name = info.GetString ("name");
			Color = new Gdk.Color (info.GetByte ("r"), info.GetByte ("g"), info.GetByte ("b"));
		}
		
		public void GetObjectData (SerializationInfo info, StreamingContext context)
		{
			info.AddValue ("name", Name);
			info.AddValue ("r", (byte)(Color.Red >> 8));
			info.AddValue ("g", (byte)(Color.Green >> 8));
			info.AddValue ("b", (byte)(Color.Blue >> 8));
		}
		
		public string Name { get; set; }
		public Gdk.Color Color { get; set; }
	}
}

