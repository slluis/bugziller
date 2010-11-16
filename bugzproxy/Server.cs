/* Bugzilla C# Proxy Library
   Copyright (C) 2006, Dansk BiblioteksCenter A/S
   Mads Bondo Dydensborg, <mbd@dbc.dk>
   
   This library is free software; you can redistribute it and/or
   modify it under the terms of the GNU Lesser General Public
   License as published by the Free Software Foundation; either
   version 2.1 of the License, or (at your option) any later version.
   
   This library is distributed in the hope that it will be useful,
   but WITHOUT ANY WARRANTY; without even the implied warranty of
   MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
   Lesser General Public License for more details.
   
   You should have received a copy of the GNU Lesser General Public
   License along with this library; if not, write to the Free Software
   Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
*/


/*! \file 
  \brief Encapsulation of the Bugzilla server.

  This class implements the server-related web services exposed by Bugzilla.

  In order to use a %Server object, some properties will have to be set,
  such as Hostname, Port, and so on.
  
  Tracing of the requests and responses, can be achieved by assigning
  a TextWriter to the TraceWriter property.

  Methods exists to get some general information about the server,
  handle authentification, and get products and bugs.

  Authentication by Bugzilla is handled using cookies. In order to
  obtain a set of cookies, you call the Login method. The cookies are
  handled automatically by the xml-rpc.net assembly. In order to store
  the cookies between sessions, methods to obtain and set the cookies
  are provided.

  All methods are synchronous, and either succeed, or throw some kind
  of exception on error.

*/

using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using CookComputing.XmlRpc;
using Bugzproxy.ProxyStructs;
using System.Text;
using System.Collections.Generic;
using System.Collections;
using System.Threading;

namespace Bugzproxy
{

	/// <summary>This class encapsulates a Bugzilla server.</summary>
	/// <remarks>
	/// <para>Access is done through http/xmlrpc.</para>
	/// <para>Properties reflect settings that will not cause network traffic, while
	/// methods typically involves the server on the other end.</para>
	/// </remarks>
	public class Server
	{

		////////////////////////////////////////////////////////////////////////////////
		// Internal tracer class
		/// <summary>Trace data sent and received using the XML-RPC framework</summary>
		private class TextWriterTracer : XmlRpcLogger
		{
			// Stream to dump to
			private TextWriter writer;

			/// <summary>
			/// Get or set a <c>TextWriter</c> stream to dump to.
			/// </summary>
			/// <value>A <c>TextWriter</c> object</value>
			public TextWriter Writer {
				get { return writer; }
				set { writer = value; }
			}

			/// <summary>Called when a request is made</summary>
			/// <param name="sender">The <c>XmlRpcClientProtocol</c> from the XML-RPC
			/// library, who raised this event.</param>
			/// <param name="e">The Event arguments</param>
			protected override void OnRequest (object sender, XmlRpcRequestEventArgs e)
			{
				if (Writer != null) {
					Writer.WriteLine ("Sending =====>");
				}
				DumpStream (e.RequestStream);
				if (Writer != null) {
					Writer.WriteLine ("=====");
					Writer.Flush ();
				}
			}

			/// <summary>Called when a response is received</summary>
			/// <param name="sender">The <c>XmlRpcClientProtocol</c> from the XML-RPC
			/// library, who raised this event.</param>
			/// <param name="e">The Event arguments</param>
			protected override void OnResponse (object sender, XmlRpcResponseEventArgs e)
			{
				if (Writer != null)
					Writer.WriteLine ("Receiving <=====");
				DumpStream (e.ResponseStream);
				if (Writer != null) {
					Writer.WriteLine ("=====");
					Writer.Flush ();
				}
			}

			private void DumpStream (Stream stm)
			{
				if (Writer != null) {
					stm.Position = 0;
					TextReader trdr = new StreamReader (stm);
					String s = trdr.ReadLine ();
					while (s != null) {
						Writer.WriteLine (s);
						s = trdr.ReadLine ();
					}
				}
			}
		}
		// class TextWriterTracer
		//////////////////////////////////////////////////////////////////////

		/// <summary>This is where we direct our xmlrpc request. This could
		/// be an option, but for now it is hardcoded, as I do not expect
		/// the Bugzilla server to change this.</summary>
		private const string rpcname = "xmlrpc.cgi";

		/// <summary>Our xml-rpc.net proxy instance</summary>
		private IProxy bugzillaProxy;

		/// <summary>Assembly members can access the bugzillaProxy</summary>
		/// <value>The internal <see cref="IProxy"/> object</value>
		internal IProxy Proxy {
			get { return bugzillaProxy; }
			private set { bugzillaProxy = value; }
		}

		/// <summary>Get or set PreAuthenticate</summary>
		/// <value><b>true</b> or <b>false</b>.</value>
		/// <remarks>This property exposes the <b>PreAuthenticate</b> property of the
		/// underlying XML-RPC library, in case it may be of use to you.</remarks>
		public bool PreAuthenticate {
			get { return Proxy.PreAuthenticate; }
			set { Proxy.PreAuthenticate = value; }
		}

		/// <summary>
		/// Get or set credentials.
		/// </summary>
		/// <value>An <b>ICredentials</b> implementation.</value>
		/// <remarks>This property exposes the <b>Credentials</b> property of the underlying
		/// XML-RPC library, in case it may be of use to you.</remarks>
		public ICredentials Credentials {
			get { return Proxy.Credentials; }
			set { Proxy.Credentials = value; }
		}

		/// <summary>Our Tracer.</summary>
		private TextWriterTracer tracer;

		/// <summary>The hostname.</summary>
		private string hostname;

		/// <summary>The port to use on the server.</summary>
		private uint port;

		/// <summary>The path to use on the server.</summary>
		private string path;

		/// <summary>Whether or not to connect via SSL.</summary>
		private bool ssl;

		/// <summary>Wheter we are logged in or not.</summary>
		private bool loggedIn;


		/* You can construct a Server instance, supplying optional
      information about hostname, port, path, ssl support, and
      tracer. */


		/*! \name Constructors  */
		//@{

		//////////////////////////////////////////////////////////////////////
		/// <summary>Initialize a new instance of the <see cref="Server"/> class
		/// with the specified parameters.</summary>
		/// <param name="hostname">The name of the server to use</param>
		/// <param name="port">The port to use</param>
		/// <param name="path">The path to use</param>
		/// <param name="ssl">If <b>true</b>, use https for connections, otherwise use http</param>
		/// <param name="traceWriter">A <b>TextWriter</b> instance to trace to</param>
		/// <remarks>The <paramref name="path"/> paremeter denotes the path to Bugzilla
		/// on the server. E.g. if Bugzilla is installed on <c>http://example.com/bugs</c>,
		/// then <paramref name="path"/> is <c>"bugs"</c>.</remarks>
		public Server (string hostname, uint port, string path, bool ssl, TextWriter traceWriter)
		{
			// Create the bugzillaproxy instance, associate it with our tracer
			this.Proxy = XmlRpcProxyGen.Create<IProxy> ();
			this.tracer = new TextWriterTracer ();
			this.tracer.Attach (Proxy);
			this.Hostname = hostname;
			this.Port = port;
			this.Path = path;
			this.ssl = ssl;
			this.tracer.Writer = traceWriter;
			LoggedIn = false;
			UpdateUrl ();
		}

		/*! \overload */
		/// <summary>Initialize a new instance of the <see cref="Server"/> class
		/// with the specified host name, port and path.</summary>
		/// <remarks>This constructs with a <b>null</b> <see cref="TraceWriter"/>,
		/// empty <see cref="Path"/>, and http as protocol (scheme).</remarks>
		/// <param name="hostname">The name of the server to use</param>
		/// <param name="port">The port to use</param>
		/// <param name="path">The path to use</param>
		public Server (string hostname, uint port, string path) : this(hostname, port, path, false, null)
		{
		}

		/*! \overload */
		/// <summary>Initialize a new instance of the <see cref="Server"/> class
		/// with the specified parameters.</summary>
		/// <remarks>This constructs with a <b>null</b> <see cref="TraceWriter"/> and
		/// empty <see cref="Path"/>.</remarks>
		/// <param name="hostname">The name of the server to use</param>
		/// <param name="port">The port to use</param>
		/// <param name="ssl">If <b>true</b>, use https for connections, otherwise use
		/// http</param>
		public Server (string hostname, uint port, bool ssl) : this(hostname, port, String.Empty, ssl, null)
		{
		}

		/*! \overload */
		/// <summary>Initialize a new instance of the <see cref="Server"/> class
		/// with the specified host name and port number.</summary>
		/// <remarks>This constructs with a <b>null</b> <see cref="TraceWriter"/>, 
		/// empty <see cref="Path"/>, and http as protocol (scheme).</remarks>
		/// <param name="hostname">The name of the server to use</param>
		/// <param name="port">The port to use</param>
		public Server (string hostname, uint port) : this(hostname, port, String.Empty, false, null)
		{
		}

		/*! \overload */
		/// <summary>Initialize a new instance of the <see cref="Server"/> class
		/// with the specified host name and path.</summary>
		/// <remarks>This constructs with a <b>null</b> <see cref="TraceWriter"/> at
		/// port 80, with empty <see cref="Path"/> and http as protocol (scheme)</remarks>
		/// <param name="hostname">The name of the server to use</param>
		/// <param name="path">The path to use</param>
		public Server (string hostname, string path) : this(hostname, 80, path, false, null)
		{
		}

		/*! \overload */
		/// <summary>Initialize a new instance of the <see cref="Server"/> class
		/// with the specified host name and protocol.</summary>
		/// <remarks>This constructs with a <b>null</b> <see cref="TraceWriter"/>, 
		/// and empty <see cref="Path"/>. Depending on the setting of ssl, the port
		/// will be either 80 (<b>false</b>) or 443 (<b>true</b>).</remarks>
		/// <param name="hostname">The name of the server to use</param>
		/// <param name="ssl">If <b>true</b>, use https for connections at port 443,
		/// otherwise use http at port 80.</param>
		public Server (string hostname, bool ssl) : this(hostname, (ssl ? 443u : 80u), String.Empty, ssl, null)
		{
		}

		/*! \overload */
		/// <summary>Initialize a new instance of the <see cref="Server"/> class
		/// with the specified host name</summary>
		/// <remarks>This constructs using port 80, with an empty <see cref="Path"/>,
		/// with a <b>null</b> <see cref="TraceWriter"/>, and using http.</remarks>
		/// <param name="hostname">The name of the server to use</param>
		public Server (string hostname) : this(hostname, 80, String.Empty, false, null)
		{
		}



		/*! \overload */
		/// <summary>Initialize a new instance of the <see cref="Server"/> class</summary>
		/// <remarks>This constructs using an empty <see cref="Hostname"/>, an empty
		/// <see cref="Path"/>, port 80, using http, and with a <b>null</b> <see cref="TraceWriter"/>.
		/// </remarks>
		public Server () : this(String.Empty, 80, String.Empty, false, null)
		{
		}

		//@}

		//////////////////////////////////////////////////////////////////////
		/// <summary>Update the URL on the proxy object from hostname and
		/// port</summary>
		private void UpdateUrl ()
		{
			if (Path != String.Empty) {
				Proxy.Url = (ssl ? "https://" : "http://") + Hostname + ":" + Port + "/" + Path + "/" + rpcname;
			} else {
				Proxy.Url = (ssl ? "https://" : "http://") + Hostname + ":" + Port + "/" + rpcname;
			}
		}

		//////////////////////////////////////////////////////////////////////
		// Properties
		//////////////////////////////////////////////////////////////////////
		/// <summary>Get or set the server's host name.</summary>
		/// <remarks>If you set it while logged in, an exception will be thrown</remarks>
		/// <exception cref="InvalidOperationException">This exception will be
		/// thrown if trying to set the host name while logged in.</exception>
		public string Hostname {
			get { return hostname; }
			set {
				if (LoggedIn) {
					throw new InvalidOperationException ("Bugzilla.Hostname: Tried to change the hostname while logged in");
				}
				hostname = value;
				UpdateUrl ();
			}
		}

		/// <summary>Get or set the port of the web server.</summary>
		/// <remarks>If you set the port while logged in, an exception will be thrown.
		/// By default the port will be set to 80 for HTTP connections (the standard http
		/// port), and 443 for HTTPS connections.</remarks>
		/// <exception cref="InvalidOperationException">This exception will be
		/// thrown if trying to set the property while logged in.</exception>
		public uint Port {
			get { return port; }
			set {
				if (LoggedIn) {
					throw new InvalidOperationException ("Bugzilla.Port: Tried to change the port while logged in");
				}
				port = value;
				UpdateUrl ();
			}
		}

		/// <summary>Get or set the path.</summary>
		/// <remarks>
		/// <para>Denotes the path to Bugzilla on the server. E.g. if Bugzilla
		/// is installed on <c>http://example.com/bugs</c>, then the path is <c>"bugs"</c>.</para>
		/// <para>If you set the path while logged in, an exception will be thrown.
		/// By default the path will be set to the empty string.</para>
		/// </remarks>
		/// <exception cref="System.InvalidOperationException">This exception will be
		/// thrown if trying to set the property while logged in</exception>
		public string Path {
			get { return path; }
			set {
				if (LoggedIn) {
					throw new InvalidOperationException ("Bugzilla.Path: Tried to change the path while logged in");
				}
				path = value;
				UpdateUrl ();
			}
		}

		/// <summary>Get the login status.</summary>
		/// <remarks>If the user is logged in, returns <b>true</b>. Otherwise, return
		/// <b>false</b>.</remarks>
		public bool LoggedIn {
			get { return loggedIn; }
			private set { loggedIn = value; }
		}

		/// <summary>Get or set a <b>TextWriter</b> for debug
		/// traces.</summary>
		/// <remarks>If set, the HTTP request and response will be
		/// written to this <b>TextWriter</b></remarks>
		public TextWriter TraceWriter {
			get { return tracer.Writer; }
			set { tracer.Writer = value; }
		}

		//////////////////////////////////////////////////////////////////////
		// Server Methods
		//////////////////////////////////////////////////////////////////////

/*! \name General Methods
      Methods to get information about the server, and log in/out.*/		
		//@{
		
				/*! \example ServerInfo.cs
     * This is an example on how to use the Bugproxy.Server.GetVersion and
     * Bugzproxy.Server.GetTimezone call */

		/// <summary>Get the version of the server.</summary> 
		/// <remarks>This requires that at least the <see cref="Hostname"/> has been
		/// set. <b>GetVersion</b> can be called without beeing logged in to the Bugzilla
		/// server.</remarks> 
		/// <exception cref="InvalidOperationException">This
		/// exception will be thrown if <see cref="Hostname"/> is empty</exception>
		public string GetVersion ()
		{
			if (Hostname == String.Empty) {
				throw new InvalidOperationException ("GetVersion: Hostname is not set");
			}
			return Proxy.GetVersion ().version;
		}

		/// <summary>Get the server's time zone.</summary>
		/// <remarks>This returns the servers timezone as a
		/// <b>string</b> in (+/-)XXXX (RFC 2822) format. All time/dates returned 
		/// from the server will be in this time zone.</remarks>
		/// <returns>The server's time zone as a <b>string</b></returns>
		public string GetTimezone ()
		{
			if (Hostname == String.Empty) {
				throw new InvalidOperationException ("GetTimezone: Hostname is not set");
			}
			return Proxy.GetTimezone ().timeZone;
		}
		//@}

		//////////////////////////////////////////////////////////////////////
		// Methods to login and out, get/set cookies
		//////////////////////////////////////////////////////////////////////
		
		public void IChainLogin (string username, string password)
		{
			string url;
			if (Path != String.Empty) {
				url = (ssl ? "https://" : "http://") + Hostname + "/" + Path;
			} else {
				url = (ssl ? "https://" : "http://") + Hostname;
			}
			url += "/ICSLogin/auth-up";
			string auth = "username=" +  (username) + "&password=" +  (password);
//			url += "?" + auth;
			Console.WriteLine (url);
			HttpWebRequest req = (HttpWebRequest) HttpWebRequest.Create (url);
			req.Method = "POST";
			Stream s = req.GetRequestStream ();
			StreamWriter sw = new StreamWriter (s);
			sw.Write (auth);
			sw.Flush ();
			sw.Dispose ();
			Console.WriteLine ("Ated1");
			WebResponse res = req.GetResponse ();
			Console.WriteLine ("Ated2");
			
		}
		
		string ToHex (string str)
		{
			StringBuilder sb = new StringBuilder ();
			foreach (char c in str) {
				sb.Append ("%" + ((int)c).ToString ("x2"));
			}
			return sb.ToString ();
		}

/*! \name Authentication Handling
      Methods to log in/out, and store/set credentials. */		
		//@{
		
				/*! \example Login.cs
     * This is an example on how to use the Bugzproxy.Server.Login call, which can also be used 
     * to test if your login works with a given server. */

		/// <summary>Login to the server.</summary>
		/// <remarks>
		/// <para>Most servers require you to log in before you can retrieve information
		/// other than the version, let alone work with bugs, products or components.</para>
		/// <para>Currently, the <paramref name="remember"/> parameter is ignored by
		/// Bugzproxy.</para>
		/// </remarks> 
		/// <param name="username">The Bugzilla username to use</param>
		/// <param name="password">The Bugzilla password to use</param>
		/// <param name="remember">Same meaning as the remember checkbox of the web
		/// interface.</param>
		/// <returns>The server's internal ID number for this user.</returns>
		/// <exception cref="InvalidOperationException">This exception will be thrown
		/// if trying to login, while already logged in.</exception>
		//when remember is fixed, we should copy some words about it from the bugzilla
		//API documentation.
		public int Login (string username, string password, bool remember)
		{
			if (LoggedIn) {
				throw new InvalidOperationException ("Login: Already logged in");
			}
			LoginParam param = new LoginParam ();
			param.login = username;
			param.password = password;
			// param.remember = remember;
			int res = Proxy.Login (param).id;
			LoggedIn = true;
			// prev statement will throw if failed.
			return res;
			// I have no idea what the user want to do with that.
		}

		/// <summary>Logout of the server.</summary>
		/// <remarks>You must be logged in to call this function. This will invalidate
		/// the cookies set by Bugzilla.</remarks>
		/// <exception cref="InvalidOperationException">This exception will be thrown
		/// if trying to logout, without being logged in.</exception>
		public void Logout ()
		{
			if (!LoggedIn) {
				throw new InvalidOperationException ("Logout: Not logged in");
			}
			Proxy.Logout ();
			LoggedIn = false;
		}

		/// <summary>Get or set the cookies that are currently used as
		/// credentials.</summary>
		/// <value>A <b>CookieCollection</b> object with the currently used credetials
		/// cookies.</value>
		/// <remarks>By obtaining the cookies, you can store them somewhere, and use
		/// them instead of a login during a new session.</remarks>
		/// <exception cref="InvalidOperationException">This exception will be
		/// thrown if trying to set the cookies while logged in, or trying
		/// to get the cookies without beeing logged in.</exception>
		public CookieCollection Cookies {
			get {
				if (!LoggedIn) {
					throw new InvalidOperationException ("cookies.get: Not logged in");
				}
				return Proxy.CookieContainer.GetCookies (new Uri ((ssl ? "https://" : "http://") + Hostname));
			}
			set {
				if (LoggedIn) {
					throw new InvalidOperationException ("cookies.set: Already logged in");
				}
				foreach (Cookie c in value) {
					Proxy.CookieContainer.Add (c);
				}
			}
		}

		/// <summary>Write the currently used cookies to a
		/// stream</summary>
		/// <param name="stream">The stream to write the cookies to.</param>
		/// <remarks>By obtaining the cookies, you can
		/// store them somewhere, and use them instead of a login during a
		/// new session.</remarks>
		/// <exception cref="InvalidOperationException">This exception will be thrown
		/// if trying to set the cookies, without being logged in.</exception>
		public void WriteCookies (Stream stream)
		{
			CookieCollection cc = Cookies;
			BinaryFormatter b = new BinaryFormatter ();
			b.Serialize (stream, cc);
		}

		/// <summary>Read cookies from a stream.</summary>
		/// <param name="stream">The stream to read the cookies from.</param>
		/// <remarks>
		/// <para>By calling this method with a stored set of cookies, you do not
		/// need to perform a login.</para>
		/// <para>This method expects the stream to contain the cookies as they are written
		/// by <see cref="WriteCookies"/>.</para>
		/// </remarks>
		/// <exception cref="InvalidOperationException">This exception will be
		/// thrown if trying to get the Cookies, while being logged
		/// in</exception>
		public void ReadCookies (Stream stream)
		{
			BinaryFormatter b = new BinaryFormatter ();
			CookieCollection cc = (CookieCollection)b.Deserialize (stream);
			Cookies = cc;
		}

		//@}
		//////////////////////////////////////////////////////////////////////
		// Product related methods
		//////////////////////////////////////////////////////////////////////

/*! \name Product Access
      Methods to get information about the products that the server knows

      A user may not have access to all products. A user may have
      access to search and view bugs from some products, and
      additionally, to enter bugs against some other products. Methods
      for this are exposed here. Additionally, general methods to
      retrieve one or more bugs are made avaiable as well.
     */		
		//@{
		/// <summary>Get a list of the products a user can search</summary>
		/// <returns>List of product ids that the user can search against</returns>
				public int[] GetSelectableProductIds ()
		{
			return Proxy.GetSelectableProducts ().ids;
		}

		/// <summary>Get a list of the products a user can file bugs against</summary>
		/// <returns>List of product ids that the user can file bugs against</returns>
		public int[] GetEnterableProductIds ()
		{
			return Proxy.GetEnterableProducts ().ids;
		}

		/// <summary>Get a list of the products an user can search or file bugs against</summary>
		/// <remarks>This is effectively a union of <see cref="GetSelectableProductIds"/>
		/// and <see cref="GetEnterableProductIds"/>.</remarks>
		/// <returns>List of product ids that the user can search or file bugs against</returns>
		public int[] GetAccessibleProductIds ()
		{
			return Proxy.GetAccessibleProducts ().ids;
		}

		/*! \example ListProducts.cs
     * This is an example on how to use the Bugzproxy.Server.GetProducts call */

		/// <summary>Get a list of existing products</summary>
		/// <remarks>This returns an array of products, matching the ids
		/// supplied as argument. Note, however, that if the user has
		/// specified an id for a product that the user for some reason
		/// can not access, this is silently ignored.</remarks>
		/// <param name="ids">List of product ids</param>
		/// <returns>List of products</returns>
		public Product[] GetProducts (int[] ids)
		{
			ProductIds param;
			param.ids = ids;
			ProductInfo[] pis = Proxy.GetProducts (param).products;
			Product[] res = new Product[pis.Length];
			for (int i = 0; i < pis.Length; ++i) {
				res[i] = new Product (this, pis[i]);
			}
			return res;
		}

		/// <summary>Get a single existing product</summary>
		/// <remarks>This returns a single existing product from the id</remarks>
		/// <param name="id">The id of the product to get</param>
		/// <returns>A <see cref="Product"/> object.</returns>
		/// <exception cref="ArgumentOutOfRangeException">This exception will
		/// be thrown, if the id does not exists, or can not be accessed</exception>
		public Product GetProduct (int id)
		{
			int[] ids = new int[] { id };
			Product[] ps = GetProducts (ids);
			if (ps.Length != 1) {
				throw new ArgumentOutOfRangeException ("id", id, "No product was returned" + " from the server. You probably specified an ID of a non-existing product," + " or a product you can not access");
			}
			return ps[0];
		}
		//@}

		//////////////////////////////////////////////////////////////////////
		// Bug handling methods
		//////////////////////////////////////////////////////////////////////
/*! \name Bug Access 
      Methods to get information about the bugs that the server knows.

      Note: There is currently no search support in the Bugzilla
      WebService. When this is implemented, the search facilities
      should complement these services nicely.
      
      As for products, a user may not have read/write access to all bugs.
     */		
		//@{
				/*! \example ListBug.cs
     * This is an example on how to use the Bugzproxy.Server.GetBug call */

		/// <summary>Get a list of bugs</summary>
		/// <remarks>This returns an array of bugs, matching the ids
		/// supplied as arguments. Note, that if the user has specified
		/// an id for a bug that does not exist, or that the user can not
		/// access (read), an exception will be thrown. This is different
		/// from <see cref="GetProducts"/>.</remarks>
		/// <param name="ids">List of bug ids</param>
		/// <returns>Array of <see cref="Bug"/> objects</returns>
/*! \todo Document exception, when known. */		
		public Bug[] GetBugs (int[] ids)
		{
			BugIds param;
			param.ids = ids;
			BugInfo[] bis = Proxy.GetBugs (param).bugs;
			Bug[] res = new Bug[bis.Length];
			for (int i = 0; i < bis.Length; ++i) {
				res[i] = new Bug (this, bis[i]);
			}
			return res;
		}

		public Bug[] GetBugsByAssigned (string user)
		{
			BugSearchAssignedTo param;
			param.assigned_to = user;
			BugInfo[] bis = Proxy.Search (param).bugs;
			Bug[] res = new Bug[bis.Length];
			for (int i = 0; i < bis.Length; ++i) {
				res[i] = new Bug (this, bis[i]);
			}
			return res;
		}
		
		public Bug[] GetBugsForProduct (string product, string[] statuses, string[] severities, DateTime changedSince, ProgressCallback progressCallback)
		{
			List<Bug> res = new List<Bug> ();
			int queries = statuses.Length * severities.Length;
			progressCallback (queries, 0);
			int work = 0;
			Exception error = null;
			foreach (string st in statuses) {
				foreach (string sv in severities) {
					string status = st;
					string severity = sv;
					ThreadStart ts = delegate {
						BugSearchForProduct param = new BugSearchForProduct ();
						param.status = status;
						param.severity = severity;
						param.product = product;
						param.last_change_time = ToTimeString (changedSince);
						Console.WriteLine ("querying: " + severity + " " + status);
						try {
							BugInfo[] bis = Proxy.Search (param).bugs;
							lock (res) {
								foreach (BugInfo bi in bis)
									res.Add (new Bug (this, bi));
							}
						} catch (Exception ex) {
							error = ex;
						} finally {
							lock (res) {
								queries--;
								System.Threading.Monitor.Pulse (res);
								Console.WriteLine ("done querying: " + severity + " " + status + " Q:" + queries);
							}
						}
						progressCallback (queries, ++work);
					};
					Thread t = new Thread (ts);
					t.IsBackground = true;
					t.Start ();
				}
			}
			lock (res) {
				while (queries > 0)
					System.Threading.Monitor.Wait (res);
				
			}
			if (error != null)
				throw error;
			return res.ToArray ();
		}
		
		public Dictionary<int, BugComment[]> GetComments (int[] bugIds, bool withAttachments)
		{
			BugGetComments args = new BugGetComments ();
			args.ids = bugIds;
			GetCommentsResult res = Proxy.GetComments (args);
			Dictionary<int, BugComment[]> dict = new Dictionary<int, BugComment[]> ();
			List<int> attIds = new List<int>();
			Dictionary<int, BugComment> attTargets = new Dictionary<int, BugComment> ();
			
			foreach (DictionaryEntry e in res.bugs) {
				string bugId = (string) e.Key;
				XmlRpcStruct cms = (XmlRpcStruct) e.Value;
				object[] cmsArray = (object[]) cms ["comments"];
				List<BugComment> list = new List<BugComment> ();
				foreach (XmlRpcStruct cm in cmsArray) {
					BugComment c = new BugComment ();
					if (cm.Contains ("attachment_id"))
						c.attachment_id = (int) cm ["attachment_id"];
					c.author = (string) cm ["author"];
					c.is_private = (bool) cm ["is_private"];
					c.text = (string) cm ["text"];
					c.time = (DateTime) cm ["time"];
					list.Add (c);
					if (c.attachment_id != 0) {
						attIds.Add (c.attachment_id);
						attTargets [c.attachment_id] = c;
					}
				}
				dict [int.Parse (bugId)] = list.ToArray ();
			}
			
			if (withAttachments && attIds.Count > 0) {
				Dictionary<int,BugAttachment> atts = GetAttachments (attIds.ToArray ());
				foreach (BugAttachment at in atts.Values) {
					BugComment c = attTargets [at.id];
					c.attachment = at;
				}
			}
			
			return dict;
		}
		
		public Dictionary<int,BugAttachment> GetAttachments (int[] attachmentIds)
		{
			GetAttachmentsParam args = new GetAttachmentsParam ();
			args.attachment_ids = attachmentIds;
			GetAttachmentsResponse res = Proxy.GetAttachments (args);
			Dictionary<int,BugAttachment> dict = new Dictionary<int, BugAttachment> ();
			foreach (BugAttachment b in res.attachments)
				dict [b.id] = b;
			return dict;
		}
		
		public BugHistory[] GetBugHistory (int[] bugIds)
		{
			GetBugHistoryParam args = new GetBugHistoryParam ();
			args.ids = bugIds;
			GetBugHistoryResponse res = Proxy.GetHistory (args);
			return res.bugs;
		}
		
		string ToTimeString (DateTime time)
		{
			return time.ToUniversalTime ().ToString ("s");
		}
		
		/*! \example ListBugs.cs
     * This is an example on how to use the Bugzproxy.Server.GetBugs call */

		/// <summary>Get a bug</summary>
		/// <remarks>This return a single existing bug, from the id</remarks>
		/// <param name="id">The id of a bug</param>
		/// <returns>A bug</returns>
		/// <exception cref="ArgumentOutOfRangeException">This exception will
		/// be thrown, if the id does not exists, or can not be accessed</exception>
		public Bug GetBug (int id)
		{
			int[] ids = new int[1];
			ids[0] = id;
			Bug[] bs = GetBugs (ids);
			if (bs.Length != 1) {
				throw new ArgumentOutOfRangeException ("id", id, "No bug was returned from" + " the server. You probably specified an id of a non-existing bug, or" + " a bug you can not access");
			}
			return bs[0];
		}

		/// <summary>
		/// Represents the <c>op_sys</c> field of a bug
		/// </summary>
		/// <remarks>See <see cref="GetLegalFieldValues"/> for details.</remarks>
		public const string OperatingSystem = "operatingSystem";

		/// <summary>
		/// Represetns the <c>assigned_to</c> field of a bug
		/// </summary>
		/// <remarks>See <see cref="GetLegalFieldValues"/> for details.</remarks>
		public const string AssignedTo = "assignedTo";

		/// <summary>
		/// Represents the <c>qa_contact</c> field of a bug
		/// </summary>
		/// <remarks>See <see cref="GetLegalFieldValues"/> for details.</remarks>
		public const string QaContact = "qaContact";

		/// <summary>
		/// Represents the <c>target_milestone</c> field of a bug
		/// </summary>
		/// <remarks>See <see cref="GetLegalFieldValues"/> for details.</remarks>
		public const string TargetMilestone = "targetMilestone";

		/// <summary>Get a list of legal values for a non-product specific
		/// bug field.</summary>
		/// <remarks>This can be used to retrieve a list of legal values
		/// for non-product specific fields of a bug, such as status,
		/// severity, and so on. When applicable, you should prefer using one of <see cref="OperatingSystem"/>,
		/// <see cref="AssignedTo"/>, <see cref="QaContact"/> or <see cref="TargetMilestone"/>.
		/// For other fields, including your own custom fields, you may use the Bugzilla
		/// original naming (such as <c>op_sys</c>). Note, that in order to retrieve
		/// values for product specific fields (such as component), you must use the
		/// <see cref="Product.GetLegalFieldValues"/> method.</remarks>
		/// <returns>A list of legal values for the field</returns>
		/// <param name="fieldName">The name of a field.</param>
		public string[] GetLegalFieldValues (string fieldName)
		{
			return GetLegalFieldValues (fieldName, -1);
		}

		//@}
		// Private method, used internally, also by Product.
		internal string[] GetLegalFieldValues (string field, int productId)
		{
			GetLegalValuesForBugFieldParam param;
			// Translate names used by us to bugzilla names
			switch (field) {
			case OperatingSystem:
				field = "op_sys";
				break;
			case AssignedTo:
				field = "assigned_to";
				break;
			case QaContact:
				field = "qa_contact";
				break;
			case TargetMilestone:
				field = "target_milestone";
				break;
			}
			// Setup parameters
			param.field = field;
			param.productId = productId;
			// Ignored by server if not needed.
			return Proxy.GetLegalValuesForBugField (param).values;
		}


		/*! \name Experimental 

    * These methods are experimental, and will change/move, as the API
    * stabilizes. */

		//@{
		//////////////////////////////////////////////////////////////////////
		/// <summary>Create a bug - experimental</summary>
		/// <param name="product">Product name</param>
		/// <param name="component">Component name</param>
		/// <param name="summary">Bug summary</param>
		/// <param name="description">Bug initial description</param>
		/// <returns>the number -1.</returns>
		/// <remarks>This method is obsulete and does nothing. You should use
		/// <see cref="Product.CreateBug"/> instead.</remarks>
		[Obsolete("Use Product.CreateBug() instead.", true)]
		public int CreateBug (string product, string component, string summary, string description)
		{
			return -1;
		}

		/// <summary>
		/// Change the resolution of a bug.
		/// </summary>
		/// <param name="bugId">The Bug number</param>
		/// <param name="resolution">The new resolution</param>
		/// <returns>?</returns>
		/// <remarks>This method is not supported by current versions of Bugzilla, and
		/// will require a custom patch to work.</remarks>
		public string SetBugResolution (int bugId, string resolution)
		{
			SetBugResolutionParam parameters;
			parameters.bugId = bugId;
			parameters.resolution = resolution;
			return Proxy.SetBugResolution (parameters);
		}
		
		//@}
		
	}
	// class Server
	
	public delegate void ProgressCallback (int total, int current);
}
// namespace Bugzproxy
