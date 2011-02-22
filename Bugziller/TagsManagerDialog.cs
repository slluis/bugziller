using System;
using Gtk;
using MonoDevelop.Ide;
using System.Collections.Generic;

namespace Bugziller
{
	public partial class TagsManagerDialog : Gtk.Dialog
	{
		ListStore store;
		List<BugTag> tags;
		List<Gdk.Pixbuf> icons = new List<Gdk.Pixbuf> ();
		
		public TagsManagerDialog (List<BugTag> tags)
		{
			this.Build ();
			
			this.tags = tags;
			store = new ListStore (typeof(string),typeof(Gdk.Pixbuf));
			list.Model = store;
			
			list.AppendColumn ("", new CellRendererPixbuf (), "pixbuf", 1);
			list.AppendColumn ("", new CellRendererText (), "text", 0);
			
			Fill ();
		}
		
		void ClearIcons ()
		{
			foreach (var p in icons)
				p.Dispose();
			icons.Clear ();
		}
		
		void Fill ()
		{
			store.Clear ();
			ClearIcons ();
			foreach (BugTag t in tags) {
				var px = GetIcon (t.Color);
				icons.Add (px);
				store.AppendValues (t.Name, px);
			}
		}
		
		public Gdk.Pixbuf GetIcon (Gdk.Color gcolor)
		{
			uint color = (((uint)gcolor.Red >> 8) << 24) | (((uint)gcolor.Green >> 8) << 16) | (((uint)gcolor.Blue >> 8) << 8) | 0xff;
			Gdk.Pixbuf icon = new Gdk.Pixbuf (Gdk.Colorspace.Rgb, true, 8, 16, 16);
			icon.Fill (color);
			return icon;
		}
		
		protected virtual void OnButtonAddClicked (object sender, System.EventArgs e)
		{
			NewTagDialog dlg = new NewTagDialog ();
			if (dlg.Run () == (int)ResponseType.Ok) {
				if (dlg.Name.Length > 0) {
					tags.Add (new BugTag (dlg.TagName, dlg.Color));
					Fill ();
				}
			}
			dlg.Destroy ();
		}
		
		protected virtual void OnButtonRemoveClicked (object sender, System.EventArgs e)
		{
			TreeIter it;
			if (list.Selection.GetSelected (out it)) {
				string tag = (string) store.GetValue (it, 0);
				tags.RemoveAll (t => t.Name == tag);
				Fill ();
			}
		}
	}
}

