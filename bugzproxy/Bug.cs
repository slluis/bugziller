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
  \brief Encapsulation of a bug in Bugzilla.

  Bugs are, of course, what Bugzilla tracks. In order to do anything with bugs,
  you need bug objects. 

  Instances of this class can be used to manipulate a Bug in a
  Bugzilla server.

  You cannot directly create a \b Bug object. You should get a \b Bug
  object from Server.GetBug or Server.GetBugs.

  In order to actually instantiate a Bug, a Server instance must be
  supplied.
*/

using CookComputing.XmlRpc;
using System;
using Bugzproxy.ProxyStructs;

namespace Bugzproxy
{
	/// <summary>This class Encapsulates a bug in Bugzilla</summary>
	/// <remarks>
	/// <para>One should assume that all operations on a <b>Bug</b> will create network
	/// traffic, unless specifically indicated they will not.</para>
	/// <para>Properties reflect settings that will not cause network traffic, except
	/// for <see cref="Resolution"/>, while methods typically involves the server
	/// on the other end.</para>
	/// <para>Currently there are no public constructors. You should get a <b>Bug</b>
	/// object from <see cref="Server.GetBug"/> or <see cref="Server.GetBugs"/>.
	/// </para>
	/// </remarks>
	public class Bug
	{
		private Server server;
		private BugInfo bi;
		// Must always be valid
/*! \name Constructors */		
		
		//@{
		
		// This assumes the bug already exists on the server side.
		/// <summary>
		/// Initialize a new instance of the <see cref="Bug"/> class.
		/// </summary>
		/// <param name="server">A <see cref="Server"/> instance that is associated with this bug</param>
		/// <param name="bi">Information about the bug, as retreived from the server</param>
				internal Bug (Server server, BugInfo bi)
		{
			this.server = server;
			this.bi = bi;
		}
		//@}

/*! \name General methods */		
		//@{ 
		
		/// <summary>Update bug from server</summary>
		/// <remarks>Get any changes to the bug from the server. This updates information
		/// such as the time/date of the last change to the bug, etc.</remarks>
				public void Update ()
		{
			int[] ids = new int[] { bi.id };
			BugIds param;
			param.ids = ids;
			this.bi = server.Proxy.GetBugs (param).bugs[0];
		}

		//@}

/*! \name Experimental 
         Experimental methods require patches. */		
		
		//@{
				/*! \example AppendComment.cs
     * This is an example on how to use the Bugzproxy.Bug.AppendComment method */

		/// <summary>Append a comment to the bug.</summary>
		/// <param name="comment">The comment to append</param>
		/// <param name="isPrivate"><b>true</b> to make this comment visible to members
		/// of Bugzilla's <c>insidergroup</c> only, <b>false</b> (or <b>null</b>) to
		/// make it visible to all members.</param>
		/// <param name="worktime">The work time of this comment. Can be <b>null</b>
		/// or 0 for no work time. Ignored if You are not in the <c>timetrackinggroup</c>.
		/// </param>
		/// <remarks>
		/// <para>This requires a patch from
		/// <a href="https://bugzilla.mozilla.org/show_bug.cgi?id=355847">Bug 355847</a>.
		/// </para>
		/// <para>If <paramref name="isPrivate"/> is <b>null</b>, the comment is assumed
		/// public.</para>
		/// </remarks>
		public void AppendComment (string comment, bool? isPrivate, double? worktime)
		{
			AppendCommentParam param = new AppendCommentParam ();
			param.id = bi.id;
			param.comment = comment;
			param.isPrivate = isPrivate;
			param.workTime = worktime;
			server.Proxy.AppendComment (param);
		}
			/*! \todo, call update? */			
		
		/// <summary>
		/// Append a comment to the bug.
		/// </summary>
		/// <param name="comment">The comment to append</param>
		/// <remarks>Works with Bugzilla trunk (3.1.2+) only</remarks>
				public void AppendComment (string comment)
		{
			AppendComment (comment, null, null);
		}

		/// <summary>
		/// Append a comment to the bug.
		/// </summary>
		/// <param name="comment">The comment to append</param>
		/// <param name="isPrivate"><b>true</b> to make this comment visible to members
		/// of Bugzilla's <c>insidergroup</c> only, <b>false</b> (or <b>null</b>) to
		/// make it visible to all members.</param>
		/// <remarks>Works with Bugzilla trunk (3.1.2+) only</remarks>
		public void AppendComment (string comment, bool? isPrivate)
		{
			AppendComment (comment, isPrivate, null);
		}

		/// <summary>
		/// Append a comment to the bug.
		/// </summary>
		/// <param name="comment">The comment to append</param>
		/// <param name="worktime">The work time of this comment. Can be <b>null</b>
		/// or 0 for no work time. Ignored if You are not in the <c>timetrackinggroup</c>.
		/// </param>
		/// <remarks>Works with Bugzilla trunk (3.1.2+) only</remarks>
		public void AppendComment (string comment, double? worktime)
		{
			AppendComment (comment, null, worktime);
		}

		/// <summary>
		/// Set the bug resolution
		/// </summary>
		/// <value>A <b>string</b> with a legal resolution value to set for this bug.</value>
		/// <remarks>This property is not implemented in Bugzilla, and requires an
		/// unpublished patch. It calls a web service named <c>Bug.set_resolution</c>
		/// (and therefore creates network traffic).</remarks>
		public string Resolution {
			set {
				SetBugResolutionParam parameters;
				parameters.bugId = bi.id;
				parameters.resolution = value;
				server.Proxy.SetBugResolution (parameters);
				Update ();
			}
		}

		//@}

		/// <summary>
		/// Get the bug id number
		/// </summary>
		/// <value>The bug id.</value>
		public int Id {
			get { return bi.id; }
		}

		/// <summary>
		/// Get the time the bug was created
		/// </summary>
		/// <value>The bug creation time.</value>
		public DateTime Created {
			get { return bi.created; }
		}

		/// <summary>
		/// Get the time the bug was last changed.
		/// </summary>
		/// <value>The bug last change time.</value>
		/// <remarks>A change may be a change in any of the bug fields, or a change
		/// in the status of an attachment of the bug.</remarks>
		public DateTime Changed {
			get { return bi.changed; }
		}

		/// <summary>
		/// Get the bug alias
		/// </summary>
		/// <value>The bug alias</value>
		public string Alias {
			get { return bi.alias; }
		}

		/// <summary>
		/// Get the bug summary
		/// </summary>
		/// <value>The bug summary</value>
		public string Summary {
			get { return bi.summary; }
		}

		public string AssignedTo {
			get { return bi.assigned_to; }
		}
		
		public string Severity {
			get { return bi.severity; }
		}
		
		public string Status {
			get { return bi.status; }
		}
		
		public string TargetMilestone {
			get; set;
		}
		
		public string Component {
			get { return bi.component; }
		}
		
		public string OperatingSystem {
			get { return bi.op_sys; }
		}
	}
	// class Bug
}
// namespace Bugzproxy
