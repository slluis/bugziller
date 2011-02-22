using System;

namespace Bugziller
{
	public partial class NewTagDialog : Gtk.Dialog
	{
		public NewTagDialog ()
		{
			this.Build ();
		}
		
		public string TagName {
			get { return entry.Text; }
		}
		
		public Gdk.Color Color {
			get { return color.Color; }
		}
	}
}

