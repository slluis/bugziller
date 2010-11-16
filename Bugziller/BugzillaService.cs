// 
// BugzillaService.cs
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
using MonoDevelop.Core;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using MonoDevelop.Ide;

namespace Bugziller
{
	public static class BugzillaService
	{
		static List<ServerInfo> serverIndex;
		static List<BugzillaServer> servers = new List<BugzillaServer> ();
		
		public static ServerInfo[] GetServers ()
		{
			if (serverIndex == null)
				LoadIndex ();
			return serverIndex.ToArray ();
		}
		
		public static BugzillaServer LoadServer (ServerInfo si)
		{
			BugzillaServer srv = servers.FirstOrDefault (s => s.Id == si.Id);
			if (srv != null)
				return srv;
			srv = BugzillaServer.Load (si.Id);
			servers.Add (srv);
			SaveIndex ();
			return srv;
		}
		
		public static void AddServer (BugzillaServer server)
		{
			int i = 1;
			foreach (var si in serverIndex)
				if (si.Id >= i)
					i = si.Id + 1;
			
			server.Id = i;
			servers.Add (server);
			serverIndex.Add (new ServerInfo () { Name = server.Name, Id = server.Id });
			server.Save ();
			SaveIndex ();
		}
		
		public static void RemoveServer (BugzillaServer server)
		{
			servers.Remove (server);
			serverIndex.RemoveAll (s => s.Id == server.Id);
			SaveIndex ();
		}
		
		public static FilePath BugzillaDataPath {
			get {
				return PropertyService.ConfigPath.Combine ("Bugzilla");
			}
		}
		
		static void LoadIndex ()
		{
			string file = BugzillaDataPath.Combine ("index").FullPath;
			if (File.Exists (file)) {
				try {
					using (Stream fs = File.OpenRead (file)) {
						BinaryFormatter f = new BinaryFormatter ();
						serverIndex = (List<ServerInfo>) f.Deserialize (fs);
						return;
					}
				} catch (Exception ex) {
					MessageService.ShowException (ex, "Bugzilla server index failed to load");
				}
			}
			serverIndex = new List<ServerInfo> ();
		}
		
		internal static void SaveIndex ()
		{
			foreach (var s in servers) {
				ServerInfo si = serverIndex.FirstOrDefault (i => i.Id == s.Id);
				si.Name = s.Name;
			}
			if (!Directory.Exists (BugzillaDataPath))
				Directory.CreateDirectory (BugzillaDataPath);
			string file = BugzillaDataPath.Combine ("index").FullPath;
			string tmpFile = file + ".tmp";
			using (Stream fs = File.OpenWrite (tmpFile)) {
				BinaryFormatter f = new BinaryFormatter ();
				f.Serialize (fs, serverIndex);
			}
			FileService.SystemRename (tmpFile, file);
		}
	}
	
	[Serializable]
	public class ServerInfo
	{
		public string Name { get; set; }
		public int Id { get; set; }
	}
}

