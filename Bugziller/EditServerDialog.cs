// 
// EditServerDialog.cs
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

namespace Bugziller
{
	public partial class EditServerDialog : Gtk.Dialog
	{
		BugzillaServer server;
		
		public EditServerDialog (BugzillaServer server, bool isNew)
		{
			this.Build ();
			this.server = server;
			
			entryName.Text = server.Name;
			entryHost.Text = server.Host;
			entryProduct.Text = server.Product;
			entryUser.Text = server.User;
			entryPassword.Text = server.Password;
			checkSSL.Active = server.UseSSL;
			
			if (!isNew) {
				entryHost.Sensitive = false;
				entryProduct.Sensitive = false;
			}
		}
		
		public void Save ()
		{
			server.Name = entryName.Text;
			server.Host = entryHost.Text;
			server.Product = entryProduct.Text;
			server.User = entryUser.Text;
			server.Password = entryPassword.Text;
			server.UseSSL = checkSSL.Active;
		}
	}
}

