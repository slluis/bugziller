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
		TreeStore bugsStore;
		BugzillaServer server;
		List<BugInfo> draggedBugs;
		CommandEntrySet menuSet;
		bool updatingServers;
		GroupCommand lastGroupCommand = GroupCommand.GroupByNothing;
		TreeViewColumn groupColumn;
		TreeViewColumn lastGroupColumn;
		TreeViewColumn[] groupColumns;
		
		Gdk.Color HighPrioColor = new Gdk.Color (235, 160, 160);
		Gdk.Color MedPrioColor = new Gdk.Color (235, 203, 145);
		Gdk.Color LowPrioColor = new Gdk.Color (235, 228, 160);
		
		const int ColBug = 0;
		const int ColGroup = 1;
		const int ColId = 2;
		const int ColPriority = 3;
		const int ColStatus = 4;
		const int ColSeverity = 5;
		const int ColTargetMilestone = 6;
		const int ColAge = 7;
		const int ColAssignee = 8;
		const int ColOS = 9;
		const int ColComponent = 10;
		const int ColSummary = 11;
		const int ColWeight = 12;
		const int ColBackColor = 13;
		const int ColFontColor = 14;
		
		public BugsViewWidget (BugzillaServer server)
		{
			this.Build ();
			this.server = server;
			
			bugsStore = new TreeStore (
			                           typeof (BugInfo), 
			                           typeof(string), // Group
			                           typeof(int), // Id
			                           typeof(int), // Local priority
			                           typeof(string), // Status
			                           typeof(string), // Severity
			                           typeof(string), // Target Milestone
			                           typeof(int), // Age
			                           typeof(string), // Assignee
			                           typeof(string), // ColOS
			                           typeof(string), // Component
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
			
			groupColumns = new TreeViewColumn [Enum.GetValues (typeof(GroupCommand)).Length - 1];
			
			groupColumn = bugsList.AppendColumn ("Category", new CellRendererText (), "text", ColGroup);
			bugsList.AppendColumn ("Id", new CellRendererText (), "text", ColId);
			bugsList.AppendColumn ("Prio", spin, "text", ColPriority);
			groupColumns [(int)GroupCommand.GroupByStatus] = bugsList.AppendColumn ("Status", new CellRendererText (), "text", ColStatus);
			groupColumns [(int)GroupCommand.GroupBySeverity] = bugsList.AppendColumn ("Severity", new CellRendererText (), "text", ColSeverity);
			groupColumns [(int)GroupCommand.GroupByMilestone] = bugsList.AppendColumn ("Milestone", new CellRendererText (), "text", ColTargetMilestone);
			bugsList.AppendColumn ("Age", new CellRendererText (), "text", ColAge);
			groupColumns [(int)GroupCommand.GroupByOwner] = bugsList.AppendColumn ("Assigned", new CellRendererText (), "text", ColAssignee);
			bugsList.AppendColumn ("OS", new CellRendererText (), "text", ColOS);
			groupColumns [(int)GroupCommand.GroupByComponent] = bugsList.AppendColumn ("Component", new CellRendererText (), "text", ColComponent);
			CellRendererText ct = new CellRendererText ();
			bugsList.AppendColumn ("Summary", ct, "text", ColSummary);
			
			int n = 1;
			foreach (TreeViewColumn col in bugsList.Columns) {
				col.SortColumnId = n++;
				col.Clickable = true;
				col.Resizable = true;
				col.Reorderable = true;
				CellRendererText crt = (CellRendererText) col.CellRenderers[0];
				col.AddAttribute (crt, "weight", ColWeight);
				col.AddAttribute (crt, "background-gdk", ColBackColor);
				col.AddAttribute (crt, "foreground-gdk", ColFontColor);
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
			
			ActionCommand setPrioHigh = new ActionCommand (LocalCommands.SetPriorityHigh, GettextCatalog.GetString ("Set High Priority (Bottom)"));
			ActionCommand setPrioMed = new ActionCommand (LocalCommands.SetPriorityMed, GettextCatalog.GetString ("Set Medium Priority (Bottom)"));
			ActionCommand setPrioLow = new ActionCommand (LocalCommands.SetPriorityLow, GettextCatalog.GetString ("Set Low Priority (Bottom)"));
			ActionCommand setPrioHighTop = new ActionCommand (LocalCommands.SetPriorityHighTop, GettextCatalog.GetString ("Set High Priority (Top)"));
			ActionCommand setPrioMedTop = new ActionCommand (LocalCommands.SetPriorityMedTop, GettextCatalog.GetString ("Set Medium Priority (Top)"));
			ActionCommand setPrioLowTop = new ActionCommand (LocalCommands.SetPriorityLowTop, GettextCatalog.GetString ("Set Low Priority (Top)"));
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
			menuSet.Add (setPrioHighTop);
			menuSet.Add (setPrioHigh);
			menuSet.Add (setPrioMedTop);
			menuSet.Add (setPrioMed);
			menuSet.Add (setPrioLowTop);
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
			
			// Edit button
			
			MenuButton editButton = new MenuButton ();
			editButton.Relief = ReliefStyle.None;
			editButton.Label = GettextCatalog.GetString ("Manage");
			editButton.MenuCreator = delegate {
				return IdeApp.CommandService.CreateMenu (adminMenuSet);
			};
			hboxHeader.PackStart (editButton, false, false, 0);
			Box.BoxChild ch = (Box.BoxChild) hboxHeader [editButton];
			ch.Position = 1;
			editButton.ShowAll ();
			
			// Group by button

			CommandEntrySet groupByMenuSet = new CommandEntrySet ();
			groupByMenuSet.Add (new ActionCommand (GroupCommand.GroupByNothing, GettextCatalog.GetString ("Don't group")));
			groupByMenuSet.AddSeparator ();
			groupByMenuSet.Add (new ActionCommand (GroupCommand.GroupByComponent, GettextCatalog.GetString ("Component")));
			groupByMenuSet.Add (new ActionCommand (GroupCommand.GroupByMilestone, GettextCatalog.GetString ("Target Milestone")));
			groupByMenuSet.Add (new ActionCommand (GroupCommand.GroupByOwner, GettextCatalog.GetString ("Assigned To")));
			groupByMenuSet.Add (new ActionCommand (GroupCommand.GroupBySeverity, GettextCatalog.GetString ("Severity")));
			groupByMenuSet.Add (new ActionCommand (GroupCommand.GroupByStatus, GettextCatalog.GetString ("Status")));
			
			MenuButton groupButton = new MenuButton ();
			groupButton.Relief = ReliefStyle.None;
			groupButton.Label = GettextCatalog.GetString ("Group By");
			groupButton.MenuCreator = delegate {
				return IdeApp.CommandService.CreateMenu (groupByMenuSet);
			};
			hboxHeader.PackStart (groupButton, false, false, 0);
			ch = (Box.BoxChild) hboxHeader [groupButton];
			ch.Position = 4;
			groupButton.ShowAll ();
			
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
			SetPriorityHighTop,
			SetPriorityHigh,
			SetPriorityMedTop,
			SetPriorityMed,
			SetPriorityLowTop,
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
		
		enum GroupCommand
		{
			GroupByComponent,
			GroupByOwner,
			GroupByStatus,
			GroupByMilestone,
			GroupBySeverity,
			GroupByNothing
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
				int order = (int) bugsStore.GetValue (it, ColPriority);
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
					BugInfo bi = (BugInfo) bugsStore.GetValue (it, ColBug);
					if (bi != null) {
						bugsStore.SetValue (it, ColPriority, val);
						bi.LocalPriority = val;
					}
				}
			}
		}
		
		List<BugInfo> GetSelection ()
		{
			List<BugInfo> bugs = new List<BugInfo> ();
			foreach (var p in bugsList.Selection.GetSelectedRows ()) {
				TreeIter it;
				bugsStore.GetIter (out it, p);
				BugInfo bi = (BugInfo) bugsStore.GetValue (it, ColBug);
				if (bi != null)
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
				if (lastGroupColumn != null) {
					lastGroupColumn.Visible = true;
					lastGroupColumn = null;
				}
				
				if (lastGroupCommand == GroupCommand.GroupByNothing) {
					groupColumn.Visible = false;
					foreach (BugInfo bi in server.GetBugs ()) {
						if (IsFiltered (bi))
							continue;
						AppendBug (TreeIter.Zero, bi);
						nbugs++;
					}
				} else {
					lastGroupColumn = groupColumns [(int)lastGroupCommand];
					groupColumn.Title = lastGroupColumn.Title;
					groupColumn.Visible = true;
					lastGroupColumn.Visible = false;
					foreach (var bg in GroupBugs ()) {
						string name = string.IsNullOrEmpty (bg.Key) ? "(None)" : bg.Key;
						TreeIter it = AppendGroup (name + " (" + bg.Value.Count () + ")");
						foreach (var b in bg.Value) {
							AppendBug (it, b);
							nbugs++;
						}
					}
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

		void AppendBug (TreeIter pi, BugInfo bug)
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
			if (pi.Equals (TreeIter.Zero))
				bugsStore.AppendValues (bug, GrepGroupValue (bug), bug.Id, bug.LocalPriority, bug.Status, bug.Severity, bug.TargetMilestone, age, bug.Assignee, bug.OperatingSystem, bug.Component, bug.Summary, we, bg, fg);
			else
				bugsStore.AppendValues (pi, bug, GrepGroupValue (bug), bug.Id, bug.LocalPriority, bug.Status, bug.Severity, bug.TargetMilestone, age, bug.Assignee, bug.OperatingSystem, bug.Component, bug.Summary, we, bg, fg);
		}
		
		TreeIter AppendGroup (string name)
		{
			return bugsStore.AppendValues (null, name, 0, 0, "", "", "", 0, "", "", "", "", 400, null, null);
		}
		
		string GrepGroupValue (BugInfo b)
		{
			switch (lastGroupCommand) {
			case GroupCommand.GroupByComponent: return b.Component;
			case GroupCommand.GroupByMilestone: return b.TargetMilestone;
			case GroupCommand.GroupByOwner: return b.Assignee;
			case GroupCommand.GroupBySeverity: return b.Severity;
			case GroupCommand.GroupByStatus: return b.Status;
			}
			return "";
		}
		
		bool IsFiltered (BugInfo bi)
		{
			if (bi.Status == "NEEDINFO" || bi.Status == "CLOSED" || bi.Status == "RESOLVED" || bi.Status == "VERIFIED")
				return true;
			string filter = entryFilter.Text.ToLower ();
			if (filter.Length > 0) {
				if (bi.Summary.ToLower ().IndexOf (filter) == -1) {
					if (bi.Comments.All (ci => ci.Text.ToLower ().IndexOf (filter) == -1))
						return true;
				}
			}
			return false;
		}
		
		IEnumerable<KeyValuePair<string,IEnumerable<BugInfo>>> GroupBugs ()
		{
			return from b in server.GetBugs () where !IsFiltered(b) group b by GrepGroupValue(b) into g select new KeyValuePair<string,IEnumerable<BugInfo>> (g.Key, g);
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
			server.SetPriority (Priority.High, false, sel);
			Fill ();
			server.Save ();
		}
		
		[CommandHandler (LocalCommands.SetPriorityMed)]
		protected void OnSetPriorityMed ()
		{
			List<BugInfo> sel = GetSelection ();
			server.SetPriority (Priority.Medium, false, sel);
			Fill ();
			server.Save ();
		}
		
		[CommandHandler (LocalCommands.SetPriorityLow)]
		protected void OnSetPriorityLow ()
		{
			List<BugInfo> sel = GetSelection ();
			server.SetPriority (Priority.Low, false, sel);
			Fill ();
			server.Save ();
		}
		
		[CommandHandler (LocalCommands.SetPriorityHighTop)]
		protected void OnSetPriorityHighTop ()
		{
			List<BugInfo> sel = GetSelection ();
			server.SetPriority (Priority.High, true, sel);
			Fill ();
			server.Save ();
		}
		
		[CommandHandler (LocalCommands.SetPriorityMedTop)]
		protected void OnSetPriorityMedTop ()
		{
			List<BugInfo> sel = GetSelection ();
			server.SetPriority (Priority.Medium, true, sel);
			Fill ();
			server.Save ();
		}
		
		[CommandHandler (LocalCommands.SetPriorityLowTop)]
		protected void OnSetPriorityLowTop ()
		{
			List<BugInfo> sel = GetSelection ();
			server.SetPriority (Priority.Low, true, sel);
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
		
		[CommandHandler (GroupCommand.GroupByComponent)] protected void OnGroupByComponent () { OnGroupBy (GroupCommand.GroupByComponent); }
		[CommandHandler (GroupCommand.GroupByMilestone)] protected void OnGroupByMilestone () { OnGroupBy (GroupCommand.GroupByMilestone); }
		[CommandHandler (GroupCommand.GroupByOwner)] protected void OnGroupByOwner () { OnGroupBy (GroupCommand.GroupByOwner); }
		[CommandHandler (GroupCommand.GroupBySeverity)] protected void OnGroupBySeverity () { OnGroupBy (GroupCommand.GroupBySeverity); }
		[CommandHandler (GroupCommand.GroupByStatus)] protected void OnGroupByStatus () { OnGroupBy (GroupCommand.GroupByStatus); }
		[CommandHandler (GroupCommand.GroupByNothing)] protected void OnGroupByNothing () { OnGroupBy (GroupCommand.GroupByNothing); }
		
		void OnGroupBy (GroupCommand c)
		{
			lastGroupCommand = c;
			Fill ();
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

