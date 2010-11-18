// 
// BugsViewWidget.cs
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
using Gtk;
using MonoDevelop.Core;
using MonoDevelop.Ide;
using System.Collections.Generic;
using MonoDevelop.Components.Commands;
using System.Linq;
using MonoDevelop.Components;

namespace Bugziller
{
	[System.ComponentModel.ToolboxItem(true)]
	public partial class BugsViewWidget : Gtk.Bin
	{
		ServerInfo[] servers;
		ListStore bugsStore;
		BugzillaServer server;
		List<BugInfo> draggedBugs;
		CommandEntrySet menuSet;
		bool updatingServers;
		
		Gdk.Color HighPrioColor = new Gdk.Color (235, 160, 160);
		Gdk.Color MedPrioColor = new Gdk.Color (235, 203, 145);
		Gdk.Color LowPrioColor = new Gdk.Color (235, 228, 160);
		
		public BugsViewWidget (BugzillaServer server)
		{
			this.Build ();
			this.server = server;
			
			bugsStore = new ListStore (
			                           typeof (BugInfo), 
			                           typeof(int), // Id
			                           typeof(int), // Local priority
			                           typeof(int), // Auto priority
			                           typeof(string), // Status
			                           typeof(string), // Severity
			                           typeof(string), // Target Milestone
			                           typeof(int), // Age
			                           typeof(string), // Assignee
			                           typeof(string), // Summary
			                           typeof(int), // Weight
			                           typeof(Gdk.Color), // Back color
			                           typeof(Gdk.Color) // Font color
			                           );
			bugsList.Model = bugsStore;
			
/*			CellRendererSpin spin = new CellRendererSpin ();
			spin.Digits = 1;
						 */
			CellRendererText spin = new CellRendererText ();
			
			spin.Editable = true;
			spin.Edited += HandleSpinEdited;
			
			bugsList.AppendColumn ("Id", new CellRendererText (), "text", 1);
			bugsList.AppendColumn ("Prio", spin, "text", 2);
			bugsList.AppendColumn ("A Prio", new CellRendererText (), "text", 3);
			bugsList.AppendColumn ("Status", new CellRendererText (), "text", 4);
			bugsList.AppendColumn ("Severity", new CellRendererText (), "text", 5);
			bugsList.AppendColumn ("Milestone", new CellRendererText (), "text", 6);
			bugsList.AppendColumn ("Age", new CellRendererText (), "text", 7);
			bugsList.AppendColumn ("Assigned", new CellRendererText (), "text", 8);
			CellRendererText ct = new CellRendererText ();
			bugsList.AppendColumn ("Summary", ct, "text", 9);
			
			int n = 1;
			foreach (TreeViewColumn col in bugsList.Columns) {
				col.SortColumnId = n++;
				col.Clickable = true;
				col.Resizable = true;
				col.Reorderable = true;
				CellRendererText crt = (CellRendererText) col.CellRenderers[0];
				col.AddAttribute (crt, "weight", 10);
				col.AddAttribute (crt, "background-gdk", 11);
				col.AddAttribute (crt, "foreground-gdk", 12);
			}
			
			bugsList.DragBegin += HandleBugsListDragBegin;
			bugsList.DragDataReceived += HandleBugsListDragDataReceived;
			bugsList.DragEnd += HandleBugsListDragEnd;
			bugsList.DragMotion += HandleBugsListDragMotion;
			
			bugsList.Selection.Mode = SelectionMode.Multiple;
			bugsList.Selection.Changed += HandleBugsListSelectionChanged;
			
			Gtk.TargetEntry[] targets = new Gtk.TargetEntry [] { new TargetEntry ("bug", TargetFlags.Widget, 0) };
//			bugsList.EnableModelDragSource (Gdk.ModifierType.None, targets, Gdk.DragAction.Move);
			Gtk.Drag.SourceSet (bugsList, Gdk.ModifierType.Button1Mask, targets, Gdk.DragAction.Move);
			bugsList.EnableModelDragDest (targets, Gdk.DragAction.Move);
			
			ActionCommand setPrioHigh = new ActionCommand (LocalCommands.SetPriorityHigh, GettextCatalog.GetString ("Set High Priority"));
			ActionCommand setPrioMed = new ActionCommand (LocalCommands.SetPriorityMed, GettextCatalog.GetString ("Set Medium Priority"));
			ActionCommand setPrioLow = new ActionCommand (LocalCommands.SetPriorityLow, GettextCatalog.GetString ("Set Low Priority"));
			ActionCommand toggleRead = new ActionCommand (LocalCommands.ToggleNewMarker, GettextCatalog.GetString ("Mark as Changed"));
			ActionCommand openInBrowser = new ActionCommand (LocalCommands.OpenInBrowser, GettextCatalog.GetString ("Open in Browser"));
			ActionCommand refreshBugInfo = new ActionCommand (LocalCommands.RefreshFromSever, GettextCatalog.GetString ("Refresh From Server"));
			ActionCommand setTagCommand = new ActionCommand (LocalCommands.TagsList, GettextCatalog.GetString ("Set tag"));
			ActionCommand clearTagsCommand = new ActionCommand (LocalCommands.ClearTags, GettextCatalog.GetString ("Clear Tags"));
			setTagCommand.CommandArray = true;
			setTagCommand.ActionType = ActionType.Check;
			
			menuSet = new CommandEntrySet ();
			menuSet.Add (openInBrowser);
			menuSet.AddSeparator ();
			menuSet.Add (setPrioHigh);
			menuSet.Add (setPrioMed);
			menuSet.Add (setPrioLow);
			menuSet.AddSeparator ();
			
			CommandEntrySet tagsSet = menuSet.AddItemSet (GettextCatalog.GetString ("Tags"));
			tagsSet.Add (setTagCommand);
			tagsSet.AddSeparator ();
			tagsSet.Add (clearTagsCommand);
			
			menuSet.Add (toggleRead);
			menuSet.AddSeparator ();
			menuSet.Add (refreshBugInfo);
			
			// Manage menu
			
			ActionCommand newServer = new ActionCommand (LocalCommands.NewServer, GettextCatalog.GetString ("Add Server..."));
			ActionCommand deleteServer = new ActionCommand (LocalCommands.DeleteServer, GettextCatalog.GetString ("Remove Server"));
			ActionCommand editServer = new ActionCommand (LocalCommands.EditServer, GettextCatalog.GetString ("Edit Server"));
			CommandEntrySet adminMenuSet = new CommandEntrySet ();
			adminMenuSet.Add (newServer);
			adminMenuSet.Add (deleteServer);
			adminMenuSet.Add (editServer);

			MenuButton editButton = new MenuButton ();
			editButton.Relief = ReliefStyle.None;
			editButton.Label = GettextCatalog.GetString ("Manage");
			editButton.MenuCreator = delegate {
				return IdeApp.CommandService.CreateMenu (adminMenuSet);
			};
			
			hboxHeader.PackStart (editButton, false, false, 0);
			Box.BoxChild ch = (Box.BoxChild) hboxHeader [editButton];
			ch.Position = 1;
			editButton.Show ();
			
			// Load data
			
			FillServers ();
			FillServer (null);
		}
		
		void FillServers ()
		{
			int current = comboServers.Active;
			updatingServers = true;
			servers = BugzillaService.GetServers ();
			((ListStore)comboServers.Model).Clear ();
			
			foreach (ServerInfo s in servers)
				comboServers.AppendText (s.Name);
			
			if (current < servers.Length)
				comboServers.Active = current;
			else {
				updatingServers = false;
				comboServers.Active = servers.Length - 1;
			}
			
			updatingServers = false;
		}
		
		void FillServer (ServerInfo s)
		{
			if (s != null) {
				vpaned1.Sensitive = true;
				server = BugzillaService.LoadServer (s);
				Fill ();
			} else {
				server = null;
				bugsStore.Clear ();
				countLabel.Text = string.Empty;
				vpaned1.Sensitive = false;
			}
		}

		enum LocalCommands
		{
			SetPriorityHigh,
			SetPriorityMed,
			SetPriorityLow,
			ToggleNewMarker,
			OpenInBrowser,
			RefreshFromSever,
			TagsList,
			ClearTags,
			NewServer,
			DeleteServer,
			EditServer
		}

		bool CheckAndDrop (int x, int y, bool drop, Gdk.DragContext ctx)
		{
			Gtk.TreePath path;
			Gtk.TreeViewDropPosition pos;
			if (!bugsList.GetDestRowAtPos (x, y, out path, out pos)) return false;
			
			Gtk.TreeIter iter;
			if (!bugsStore.GetIter (out iter, path)) return false;
			return pos == TreeViewDropPosition.IntoOrAfter || pos == TreeViewDropPosition.IntoOrBefore;
		}
		
		void HandleBugsListDragMotion (object o, DragMotionArgs args)
		{
			if (draggedBugs != null) {
				if (!CheckAndDrop (args.X, args.Y, false, args.Context)) {
					Gdk.Drag.Status (args.Context, (Gdk.DragAction)0, args.Time);
					args.RetVal = true;
				}
			}
		}

		void HandleBugsListDragEnd (object o, DragEndArgs args)
		{
			draggedBugs = null;
		}

		void HandleBugsListDragDataReceived (object o, DragDataReceivedArgs args)
		{
			if (CheckAndDrop (args.X, args.Y, false, args.Context)) {
				Gtk.TreePath path;
				Gtk.TreeViewDropPosition pos;
				bugsList.GetDestRowAtPos (args.X, args.Y, out path, out pos);
				TreeIter it;
				bugsStore.GetIter (out it, path);
				int order = (int) bugsStore.GetValue (it, 2);
				SetOrder (order, draggedBugs);
				Gtk.Drag.Finish (args.Context, true, true, args.Time);
			} else {
				Gtk.Drag.Finish (args.Context, false, true, args.Time);
			}
		}

		void HandleBugsListDragBegin (object o, DragBeginArgs args)
		{
			draggedBugs = GetSelection ();
		}

		void HandleSpinEdited (object o, EditedArgs args)
		{
			TreeIter it;
			if (bugsStore.GetIterFromString (out it, args.Path)) {
				int val;
				if (int.TryParse (args.NewText, out val)) {
					if (val < 0) val = 0;
					if (val > 10) val = 10;
					BugInfo bi = (BugInfo) bugsStore.GetValue (it, 0);
					bugsStore.SetValue (it, 2, val);
					bi.LocalPriority = val;
				}
			}
		}
		
		List<BugInfo> GetSelection ()
		{
			List<BugInfo> bugs = new List<BugInfo> ();
			foreach (var p in bugsList.Selection.GetSelectedRows ()) {
				TreeIter it;
				bugsStore.GetIter (out it, p);
				BugInfo bi = (BugInfo) bugsStore.GetValue (it, 0);
				bugs.Add (bi);
			}
			return bugs;
		}
		
		void SetOrder (int order, List<BugInfo> list)
		{
			server.SetOrder (order, list);
			Fill ();
			server.Save ();
		}
		
		public void Fill ()
		{
/*			TreeViewState state = new TreeViewState (bugsList, 0);
			state.Save ();*/
			double oldPosition = listScrolled.Vadjustment.Value;
			bugsList.Model = null;
			bugsStore.Clear ();
			int nbugs = 0;
			try {
				foreach (BugInfo bi in server.GetBugs ()) {
					if (IsFiltered (bi))
						continue;
					AppendBug (bi);
					nbugs++;
				}
			} catch (Exception ex) {
				MessageService.ShowException (ex);
			}
			GLib.Timeout.Add (100, delegate {
				listScrolled.Vadjustment.Value = oldPosition;
				return false;
			});
			bugsList.Model = bugsStore;
//			state.Load ();
			countLabel.Text = "Count: " + nbugs;
		}

		void AppendBug (BugInfo bug)
		{
			int age = (int) (DateTime.Now - bug.DateCreated).TotalDays;
			int we = bug.IsNew ? 700 : 400;
			object bg;
			if (bug.LocalPriority <= server.HighPriorityLevel)
				bg = HighPrioColor;
			else if (bug.LocalPriority <= server.MedPriorityLevel)
				bg = MedPrioColor;
			else if (bug.LocalPriority <= server.LowPriorityLevel)
				bg = LowPrioColor;
			else
				bg = null;
			object fg;
			if (bug.Tags != null && bug.Tags.Length > 0)
				fg = server.GetTagColor (bug.Tags[0]);
			else
				fg = null;
			bugsStore.AppendValues (bug, bug.Id, bug.LocalPriority, bug.AutoPriority, bug.Status, bug.Severity, bug.TargetMilestone, age, bug.Assignee, bug.Summary, we, bg, fg);
//			bugsStore.AppendValues (bug, 2, 1, 3, "", "", "", 1, "", "", null, null, null);
		}
		
		bool IsFiltered (BugInfo bi)
		{
			if (bi.Status == "NEEDINFO" || bi.Status == "CLOSED" || bi.Status == "RESOLVED" || bi.Status == "VERIFIED")
				return true;
			string filter = entryFilter.Text.ToLower ();
			if (filter.Length > 0) {
				if (bi.Summary.ToLower ().IndexOf (filter) == -1) {
					if (bi.Comments.Any (ci => ci.Text.ToLower ().IndexOf (filter) == -1))
						return true;
				}
			}
			return false;
		}
		
		internal void ShowPopup ()
		{
			IdeApp.CommandService.ShowContextMenu (menuSet, bugsList);
		}
		
		
		protected virtual void OnButtonUpdateClicked (object sender, System.EventArgs e)
		{
			BugzillaServer capServer = server;
			IProgressMonitor monitor = IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor ("Updating bug list", "md-template", true);
			buttonUpdate.Sensitive = false;
			IAsyncOperation oper = server.Update (monitor);
			oper.Completed += delegate {
				DispatchService.GuiDispatch (delegate {
					if (capServer == server) {
						buttonUpdate.Sensitive = true;
						Fill ();
					}
				});
			};
		}
		
		[GLib.ConnectBeforeAttribute]
		protected virtual void OnBugsListButtonPressEvent (object o, Gtk.ButtonPressEventArgs args)
		{
			if (args.Event.Button == 3) {
				ShowPopup ();
				args.RetVal = true;
			}
		}
		
		protected virtual void OnBugsListPopupMenu (object o, Gtk.PopupMenuArgs args)
		{
			ShowPopup ();
		}
		
		[CommandHandler (LocalCommands.SetPriorityHigh)]
		protected void OnSetPriorityHigh ()
		{
			List<BugInfo> sel = GetSelection ();
			server.SetOrder (server.HighPriorityLevel + 1, sel);
			server.HighPriorityLevel = server.HighPriorityLevel + sel.Count;
			Fill ();
			server.Save ();
		}
		
		[CommandHandler (LocalCommands.SetPriorityMed)]
		protected void OnSetPriorityMed ()
		{
			List<BugInfo> sel = GetSelection ();
			server.SetOrder (server.MedPriorityLevel + 1, sel);
			server.MedPriorityLevel = server.MedPriorityLevel + sel.Count;
			Fill ();
			server.Save ();
		}
		
		[CommandHandler (LocalCommands.SetPriorityLow)]
		protected void OnSetPriorityLow ()
		{
			List<BugInfo> sel = GetSelection ();
			server.SetOrder (server.LowPriorityLevel + 1, sel);
			server.LowPriorityLevel = server.LowPriorityLevel + sel.Count;
			Fill ();
			server.Save ();
		}
		
		[CommandHandler (LocalCommands.ToggleNewMarker)]
		protected void OnToggleNewMarker ()
		{
			List<BugInfo> sel = GetSelection ();
			bool newVal = !sel [0].IsNew;
			foreach (BugInfo b in sel)
				b.IsNew = newVal;
			Fill ();
			server.Save ();
		}
		
		[CommandUpdateHandler (LocalCommands.ToggleNewMarker)]
		protected void OnUpdateToggleNewMarker (CommandInfo ci)
		{
			List<BugInfo> sel = GetSelection ();
			if (sel.Count == 0) {
				ci.Enabled = false;
			} else {
				if (sel[0].IsNew)
					ci.Text = "Unmark as Changed";
				else
					ci.Text = "Mark as Changed";
			}
		}
		
		[CommandHandler (LocalCommands.OpenInBrowser)]
		protected void OnOpenInBrowser ()
		{
			foreach (var b in GetSelection ())
				server.OpenBug (b);
		}
		
		[CommandHandler (LocalCommands.RefreshFromSever)]
		protected void OnRefreshFromServer ()
		{
			IProgressMonitor monitor = IdeApp.Workbench.ProgressMonitors.GetStatusProgressMonitor ("Updating bug list", "md-template", true);
			buttonUpdate.Sensitive = false;
			IAsyncOperation oper = server.UpdateBugs (monitor, GetSelection ());
			oper.Completed += delegate {
				DispatchService.GuiDispatch (delegate {
					buttonUpdate.Sensitive = true;
					Fill ();
				});
			};
		}
		
		[CommandUpdateHandler (LocalCommands.RefreshFromSever)]
		protected void OnUpdateRefreshFromSever (CommandInfo ci)
		{
			ci.Enabled = buttonUpdate.Sensitive;
		}
		
		[CommandHandler (LocalCommands.ClearTags)]
		protected void OnClearTags ()
		{
			foreach (BugInfo bi in GetSelection ()) {
				bi.ClearTags ();
			}
			Fill ();
			server.Save ();
		}
		
		[CommandHandler (LocalCommands.TagsList)]
		protected void OnTaskList (object data)
		{
			List<BugInfo> bugs = GetSelection ();
			if (bugs.Count == 0)
				return;
			string tag = (string)data;
			bool adding = !bugs[0].HasTag (tag);
			
			foreach (BugInfo bi in GetSelection ()) {
				if (adding)
					bi.AddTag (tag);
				else
					bi.RemoveTag (tag);
			}
			Fill ();
			server.Save ();
		}
		
		[CommandUpdateHandler (LocalCommands.TagsList)]
		protected void OnUpdateTaskList (CommandArrayInfo ci)
		{
			BugInfo bug = GetSelection ().FirstOrDefault ();
			foreach (var t in server.Tags) {
				CommandInfo c = ci.Add (t.Name, t.Name);
				c.Icon = string.Format ("#{0:x2}{1:x2}{2:x2}", t.Color.Red >> 8, t.Color.Green >> 8, t.Color.Blue >> 8);
				if (bug != null)
					c.Checked = bug.HasTag (t.Name);
				else
					c.Enabled = false;
			}
		}
		
		protected virtual void OnBugsListRowActivated (object o, Gtk.RowActivatedArgs args)
		{
			List<BugInfo> sel = GetSelection ();
			server.OpenBug (sel[0]);
		}
		
		[CommandHandler (LocalCommands.NewServer)]
		protected void OnAddServer ()
		{
			BugzillaServer newServer = new BugzillaServer ();
			EditServerDialog dlg = new EditServerDialog (newServer, true);
			if (dlg.Run () == (int) ResponseType.Ok) {
				dlg.Save ();
				BugzillaService.AddServer (newServer);
				FillServers ();
				servers = BugzillaService.GetServers ();
				comboServers.Active = servers.Length - 1;
			}
			dlg.Destroy ();
		}
		
		[CommandHandler (LocalCommands.DeleteServer)]
		protected void OnDeleteServer ()
		{
			if (MessageService.Confirm (GettextCatalog.GetString ("Are you sure you want to delete all information about this bugzilla server?"), AlertButton.Delete)) {
				BugzillaService.RemoveServer (server);
				FillServers ();
			}
		}
		
		[CommandHandler (LocalCommands.EditServer)]
		protected void OnEditServer ()
		{
			EditServerDialog dlg = new EditServerDialog (server, false);
			if (dlg.Run () == (int) ResponseType.Ok) {
				dlg.Save ();
				server.Save ();
				FillServers ();
			}
			dlg.Destroy ();
		}
		
		[CommandUpdateHandler (LocalCommands.DeleteServer)]
		[CommandUpdateHandler (LocalCommands.EditServer)]
		protected void OnUpdateEditServer (CommandInfo ci)
		{
			ci.Enabled = server != null;
		}
		
		void HandleBugsListSelectionChanged (object sender, EventArgs e)
		{
			foreach (Widget w in commentsBox.Children) {
				commentsBox.Remove (w);
				w.Destroy ();
			}
			
			var sel = GetSelection ();
			if (sel.Count == 0) {
				bugTitle.Text = string.Empty;
				return;
			}
			BugInfo bi = sel [0];
			bugTitle.Markup = "<b>" + bi.Id + " - " + GLib.Markup.EscapeText (bi.Summary) + "</b>";
			foreach (CommentInfo ci in bi.Comments) {
				Label header = new Label ();
				header.Xalign = 0;
				header.Selectable = true;
				string priv = ci.IsPrivate ? " (Private)" : "";
				header.Markup = "<b>" + ci.Time.ToShortDateString () + " " + GLib.Markup.EscapeText (ci.Author) + priv + "</b>";
				commentsBox.PackStart (header, false, false, 0);
				
				TextView text = new TextView ();
				text.Buffer.Text = ci.Text;
				text.Editable = false;
				text.WrapMode = WrapMode.WordChar;
				commentsBox.PackStart (text, false, false, 0);
			}
			commentsBox.ShowAll ();
			buttonRefresh.Visible = bi.RequiresRefresh;
		}
		
		protected virtual void OnComboServersChanged (object sender, System.EventArgs e)
		{
			if (updatingServers)
				return;
			if (comboServers.Active != -1)
				FillServer (servers [comboServers.Active]);
			else
				FillServer (null);
		}
		
		protected virtual void OnButtonRefreshClicked (object sender, System.EventArgs e)
		{
			OnRefreshFromServer ();
		}
		
		protected virtual void OnButtonFilterClicked (object sender, System.EventArgs e)
		{
			Fill ();
		}
		
		[GLib.ConnectBefore]
		protected virtual void OnEntryFilterKeyPressEvent (object o, Gtk.KeyPressEventArgs args)
		{
			if (args.Event.Key == Gdk.Key.Return || args.Event.Key == Gdk.Key.KP_Enter) {
				Fill ();
				args.RetVal = true;
			}
		}
	}
}

