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
  \brief Encapsulation of a product in Bugzilla

  Bugs are associated with products and components. This is a class
  that describes the methods and properties of a product.

  You cannot directly create a \b Product object. You should get a \b Product
  object from Server.GetProduct or Server.GetProducts.
 
  In order to actually instantiate a Product, a Server instance must
  be created first.
*/

using System;
using CookComputing.XmlRpc;
using Bugzproxy.ProxyStructs;

namespace Bugzproxy {

  /// <summary>Encapsulation of a product in Bugzilla</summary>
  /// <remarks>
  /// <para>Currently there are no public constructors. You should get a <b>Product</b>
  /// object from <see cref="Server.GetProduct"/> or <see cref="Server.GetProducts"/>.
  /// </para>
  /// </remarks>
  public class Product {
    private Server server;

    private ProductInfo pi;
    
    /*! \name Constructors 

    Currently there is no way to create an entirely new product. Only
    product instances that reflect an already existing product on the
    server can be created.

    You can obtain a Product object from Server.GetProduct or Server.GetProducts.
     */
    //@{

    /// <summary>Initialize a new instance of the <see cref="Product"/> class with
    /// the specified Bugzilla server and <see cref="ProxyStructs.ProductInfo"/>
    /// struct.</summary>
    /// <param name="server">A Server instance that is associated with this product.</param>
    /// <param name="pi">Information about the product, as retrieved from the
    /// server</param>
    internal Product( Server server, ProductInfo pi ) {
      this.server = server;
      this.pi     = pi;
    }
    //@}

    /// <summary>Get the ID of the product.</summary>
    /// <value>The numeric ID of the product</value>
    public int Id {
      get {
        return pi.id;
      }
    }

    /// <summary>Get the name of the product.</summary>
    /// <value>The name of the product</value>
    public string Name {
      get {
        return pi.name;
      }
    }

    /// <summary>Get the description of the product</summary>
    /// <value>The description of the product</value>
    public string Description {
      get {
        return pi.description;
      }
    }

    /// <summary>Get list of legal values for a bug field</summary>
    /// <param name="fieldName">The name of a field.</param>
    /// <remarks>This can be used to retrieve a list of legal values
    /// for both product specific fields, as well as non-product
    /// specific fields of a bug, such as status, severity, component,
    /// and so on. When applicable, you should prefer using one of
    /// <see cref="Server.OperatingSystem"/>, <see cref="Server.AssignedTo"/>,
    /// <see cref="Server.QaContact"/> or <see cref="Server.TargetMilestone"/>.
    /// For other fields, including your own custom fields, you may use the Bugzilla
    /// original naming (such as <c>op_sys</c>).</remarks>
    /// <returns>A list of legal values for the field</returns>
    public string[] GetLegalFieldValues(string fieldName) {
      return server.GetLegalFieldValues(fieldName, Id);
    }

    /// <summary>Get a list of the components of this product.</summary>
    /// <remarks>This just calls <see cref="GetLegalFieldValues(string)"/> with
    /// <c>"component"</c> as parameter value.</remarks>
    /// <returns>A list of the components of the product.</returns>
    public string[] GetComponents() {
      return GetLegalFieldValues("component");
    }

    /*! \example CreateBug.cs
     * This is an example on how to use the Bugzproxy.Product.CreateBug call */

    /// <summary>Create a new bug on this product</summary>
    /// <remarks>
    /// <para>The parameters for this call can be marked "optional"
    /// or "defaulted". Optional parameters can be left out in all
    /// Bugzilla installations (i.e. receive <b>null</b>), and a default value
    /// from the server
    /// will be substituted. Defaulted parameters can be left out of
    /// some installations, while other installations may require
    /// these parameters to be present. This is decided in the
    /// Bugzilla preferences. If you wish to make sure that the call
    /// works with all Bugzilla installations, you should supply
    /// values for all "defaulted" parameters. Use <see cref="GetLegalFieldValues"/>
    /// to get a list of the legal values for a given
    /// field. Parameters not marked optional or defaulted are
    /// required.</para>
    /// <para>It is recommended that you use <see cref="GetLegalFieldValues"/> to
    /// retrieve legal values for parameters such as <paramref name="component"/>,
    /// <paramref name="version"/>, <see cref="operatingSystem"/>, and other parameters
    /// that have pre-configured values on the server.</para>
    /// </remarks> 
    /// <param name="alias">(Optional) If aliases are enabled for the Bugzilla
    /// server, you can supply an unique identifier (no spaces or
    /// weird characters) to identify the bug with, in
    /// addition to the id.</param>
    /// <param name="component">The name of the component that the bug
    /// will be created under.</param>
    /// <param name="version">The version of the product, that the bug
    /// was found in.</param>
    /// <param name="operatingSystem">(Defaulted) The operating system
    /// the bug was discovered on.</param>
    /// <param name="platform">(Defaulted) What type of hardware the
    /// bug was experienced on.</param>
    /// <param name="summary">Summary of the bug.</param>
    /// <param name="description">(Defaulted) Description of the
    /// bug.</param>
    /// <param name="priority">(Defaulted) What order the bug will be
    /// fixed in by the developer, compared to the developer's other
    /// bugs.</param>
    /// <param name="severity">(Defaulted) How severe the bug
    /// is.</param>
    /// <param name="status">(Optional) The status that this bug
    /// should start out as. Note that only certain statuses can be
    /// set on bug creation.</param>
    /// <param name="targetMilestone">(Optional) A valid target
    /// milestone for this product.</param>
    /// <param name="assignedTo">(Optional) A user to assign this bug
    /// to, if you don't want it to be assigned to the component
    /// owner.</param>
    /// <param name="cc">(Optional) An array of usernames to CC on
    /// this bug.</param>
    /// <param name="qaContact">(Optional) If this installation has QA
    /// Contacts enabled, you can set the QA Contact here if you don't
    /// want to use the components default QA Contact. </param>
    /// <returns>The newly created bug.</returns>
    public Bug CreateBug(string alias, string component,
         string version, string operatingSystem, string platform,
         string summary, string description,
         string priority, string severity, string status,
         string targetMilestone, string assignedTo, string[] cc,
         string qaContact) {
      CreateBugParam param;
      param.product = pi.name;
      param.alias = alias;
      param.component = component;
      param.version = version;
      param.operatingSystem = operatingSystem;
      param.platform = platform;
      param.summary = summary;
      param.description = description;
      param.priority = priority;
      param.severity = severity;
      param.status = status;
      param.targetMilestone = targetMilestone;
      param.assignedTo = assignedTo;
      param.cc = cc;
      param.qaContact = qaContact;
      return server.GetBug(server.Proxy.CreateBug(param).id);
    }


    /// <summary>
    /// Create a new bug on this product
    /// </summary>
    /// <param name="component">The name of the component that the bug
    /// will be created under.</param>
    /// <param name="version">The version of the product, that the bug
    /// was found in.</param>
    /// <param name="operatingSystem">(Defaulted) The operating system
    /// the bug was discovered on.</param>
    /// <param name="platform">(Defaulted) What type of hardware the
    /// bug was experienced on.</param>
    /// <param name="summary">Summary of the bug.</param>
    /// <param name="description">(Defaulted) Description of the
    /// bug.</param>
    /// <param name="priority">(Defaulted) What order the bug will be
    /// fixed in by the developer, compared to the developer's other
    /// bugs.</param>
    /// <param name="severity">(Defaulted) How severe the bug
    /// is.</param>
    /// <returns>The newly created bug.</returns>
    /// <remarks>
    /// <para>Parameters marked as Defaulted can be left out in
    /// some installations, while other installations may require
    /// these parameters to be present. This is decided in the
    /// Bugzilla preferences. If you wish to make sure that the call
    /// works with all Bugzilla installations, you should supply
    /// values for all "defaulted" parameters.</para>
    /// <para>It is recommended that you use <see cref="GetLegalFieldValues"/> to
    /// retrieve legal values for parameters such as <paramref name="component"/>,
    /// <paramref name="version"/>, <see cref="operatingSystem"/>, and other parameters
    /// that have pre-configured values on the server.</para>
    /// </remarks>
    public Bug CreateBug(
      string component, string version,
      string operatingSystem, string platform,
      string summary, string description,
      string priority, string severity) {
      
      return CreateBug(null, component, version, operatingSystem, platform, summary,
        description, priority, severity, null, null, null, null, null);
    }

    /// <summary>
    /// Create a new bug on this product
    /// </summary>
    /// <param name="alias">If aliases are enabled for the Bugzilla server, you
    /// can supply an unique identifier (no spaces or weird characters) to identify
    /// the bug with, in addition to the id.</param>
    /// <param name="component">The name of the component that the bug
    /// will be created under.</param>
    /// <param name="version">The version of the product, that the bug
    /// was found in.</param>
    /// <param name="operatingSystem">(Defaulted) The operating system
    /// the bug was discovered on.</param>
    /// <param name="platform">(Defaulted) What type of hardware the
    /// bug was experienced on.</param>
    /// <param name="summary">Summary of the bug.</param>
    /// <param name="description">(Defaulted) Description of the
    /// bug.</param>
    /// <param name="priority">(Defaulted) What order the bug will be
    /// fixed in by the developer, compared to the developer's other
    /// bugs.</param>
    /// <param name="severity">(Defaulted) How severe the bug
    /// is.</param>
    /// <returns>The newly created bug.</returns>
    /// <remarks>
    /// <para>Parameters marked as Defaulted can be left out in
    /// some installations, while other installations may require
    /// these parameters to be present. This is decided in the
    /// Bugzilla preferences. If you wish to make sure that the call
    /// works with all Bugzilla installations, you should supply
    /// values for all "defaulted" parameters.</para>
    /// <para>It is recommended that you use <see cref="GetLegalFieldValues"/> to
    /// retrieve legal values for parameters such as <paramref name="component"/>,
    /// <paramref name="version"/>, <see cref="operatingSystem"/>, and other parameters
    /// that have pre-configured values on the server.</para>
    /// </remarks>
    public Bug CreateBug(
      string alias, string component,
      string version, string operatingSystem,
      string platform, string summary,
      string description, string priority,
      string severity) {

      return CreateBug(alias, component, version, operatingSystem, platform, summary,
        description, priority, severity, null, null, null, null, null);
    }

    /// <summary>
    /// Create a new bug on this product
    /// </summary>
    /// <param name="alias">(Optional) If aliases are enabled for the Bugzilla
    /// server, you can supply an unique identifier (no spaces or weird characters)
    /// to identify the bug with, in addition to the id.</param>
    /// <param name="component">The name of the component that the bug
    /// will be created under.</param>
    /// <param name="version">The version of the product, that the bug
    /// was found in.</param>
    /// <param name="operatingSystem">(Defaulted) The operating system
    /// the bug was discovered on.</param>
    /// <param name="platform">(Defaulted) What type of hardware the
    /// bug was experienced on.</param>
    /// <param name="summary">Summary of the bug.</param>
    /// <param name="description">(Defaulted) Description of the
    /// bug.</param>
    /// <param name="priority">(Defaulted) What order the bug will be
    /// fixed in by the developer, compared to the developer's other
    /// bugs.</param>
    /// <param name="severity">(Defaulted) How severe the bug
    /// is.</param>
    /// <param name="status">(Optional) The status that this bug
    /// should start out as. Note that only certain statuses can be
    /// set on bug creation.</param>
    /// <returns>The newly created bug.</returns>
    /// <remarks>
    /// <para>The parameters for this call can be marked "optional"
    /// or "defaulted". Optional parameters can be left out in all
    /// Bugzilla installations (i.e. receive <b>null</b>), and a default value
    /// from the server will be substituted.
    /// Parameters marked as Defaulted can be left out in
    /// some installations, while other installations may require
    /// these parameters to be present. This is decided in the
    /// Bugzilla preferences. If you wish to make sure that the call
    /// works with all Bugzilla installations, you should supply
    /// values for all "defaulted" parameters.</para>
    /// <para>It is recommended that you use <see cref="GetLegalFieldValues"/> to
    /// retrieve legal values for parameters such as <paramref name="component"/>,
    /// <paramref name="version"/>, <see cref="operatingSystem"/>, and other parameters
    /// that have pre-configured values on the server.</para>
    /// </remarks>
    public Bug CreateBug(
          string alias, string component,
          string version, string operatingSystem,
          string platform, string summary,
          string description, string priority,
          string severity, string status) {

      return CreateBug(alias, component, version, operatingSystem, platform, summary,
        description, priority, severity, status, null, null, null, null);
    }

    /// <summary>
    /// Create a new bug on this product
    /// </summary>
    /// <param name="alias">(Optional) If aliases are enabled for the Bugzilla
    /// server, you can supply an unique identifier (no spaces or weird characters)
    /// to identify the bug with, in addition to the id.</param>
    /// <param name="component">The name of the component that the bug
    /// will be created under.</param>
    /// <param name="version">The version of the product, that the bug
    /// was found in.</param>
    /// <param name="operatingSystem">(Defaulted) The operating system
    /// the bug was discovered on.</param>
    /// <param name="platform">(Defaulted) What type of hardware the
    /// bug was experienced on.</param>
    /// <param name="summary">Summary of the bug.</param>
    /// <param name="description">(Defaulted) Description of the
    /// bug.</param>
    /// <param name="priority">(Defaulted) What order the bug will be
    /// fixed in by the developer, compared to the developer's other
    /// bugs.</param>
    /// <param name="severity">(Defaulted) How severe the bug
    /// is.</param>
    /// <param name="status">(Optional) The status that this bug
    /// should start out as. Note that only certain statuses can be
    /// set on bug creation.</param>
    /// <param name="targetMilestone">(Optional) A valid target
    /// milestone for this product.</param>
    /// <returns>The newly created bug.</returns>
    /// <remarks>
    /// <para>The parameters for this call can be marked "optional"
    /// or "defaulted". Optional parameters can be left out in all
    /// Bugzilla installations (i.e. receive <b>null</b>), and a default value
    /// from the server will be substituted.
    /// Parameters marked as Defaulted can be left out in
    /// some installations, while other installations may require
    /// these parameters to be present. This is decided in the
    /// Bugzilla preferences. If you wish to make sure that the call
    /// works with all Bugzilla installations, you should supply
    /// values for all "defaulted" parameters.</para>
    /// <para>It is recommended that you use <see cref="GetLegalFieldValues"/> to
    /// retrieve legal values for parameters such as <paramref name="component"/>,
    /// <paramref name="version"/>, <see cref="operatingSystem"/>, and other parameters
    /// that have pre-configured values on the server.</para>
    /// </remarks>
    public Bug CreateBug(
          string alias, string component,
          string version, string operatingSystem,
          string platform, string summary,
          string description, string priority,
          string severity, string status,
          string targetMilestone) {

      return CreateBug(alias, component, version, operatingSystem, platform, summary,
        description, priority, severity, status, targetMilestone, null, null, null);
    }

    /// <summary>
    /// Create a new bug on this product
    /// </summary>
    /// <param name="alias">(Optional) If aliases are enabled for the Bugzilla
    /// server, you can supply an unique identifier (no spaces or weird characters)
    /// to identify the bug with, in addition to the id.</param>
    /// <param name="component">The name of the component that the bug
    /// will be created under.</param>
    /// <param name="version">The version of the product, that the bug
    /// was found in.</param>
    /// <param name="operatingSystem">(Defaulted) The operating system
    /// the bug was discovered on.</param>
    /// <param name="platform">(Defaulted) What type of hardware the
    /// bug was experienced on.</param>
    /// <param name="summary">Summary of the bug.</param>
    /// <param name="description">(Defaulted) Description of the
    /// bug.</param>
    /// <param name="priority">(Defaulted) What order the bug will be
    /// fixed in by the developer, compared to the developer's other
    /// bugs.</param>
    /// <param name="severity">(Defaulted) How severe the bug
    /// is.</param>
    /// <param name="status">(Optional) The status that this bug
    /// should start out as. Note that only certain statuses can be
    /// set on bug creation.</param>
    /// <param name="targetMilestone">(Optional) A valid target
    /// milestone for this product.</param>
    /// <param name="assignedTo">(Optional) A user to assign this bug
    /// to, if you don't want it to be assigned to the component
    /// owner.</param>
    /// <returns>The newly created bug.</returns>
    /// <remarks>
    /// <para>The parameters for this call can be marked "optional"
    /// or "defaulted". Optional parameters can be left out in all
    /// Bugzilla installations (i.e. receive <b>null</b>), and a default value
    /// from the server will be substituted.
    /// Parameters marked as Defaulted can be left out in
    /// some installations, while other installations may require
    /// these parameters to be present. This is decided in the
    /// Bugzilla preferences. If you wish to make sure that the call
    /// works with all Bugzilla installations, you should supply
    /// values for all "defaulted" parameters.</para>
    /// <para>It is recommended that you use <see cref="GetLegalFieldValues"/> to
    /// retrieve legal values for parameters such as <paramref name="component"/>,
    /// <paramref name="version"/>, <see cref="operatingSystem"/>, and other parameters
    /// that have pre-configured values on the server.</para>
    /// </remarks>
    public Bug CreateBug(
          string alias, string component,
          string version, string operatingSystem,
          string platform, string summary,
          string description, string priority,
          string severity, string status,
          string targetMilestone, string assignedTo) {

      return CreateBug(alias, component, version, operatingSystem, platform, summary,
        description, priority, severity, status, targetMilestone, assignedTo, null,
        null);
    }

    /// <summary>
    /// Create a new bug on this product
    /// </summary>
    /// <param name="alias">(Optional) If aliases are enabled for the Bugzilla
    /// server, you can supply an unique identifier (no spaces or weird characters)
    /// to identify the bug with, in addition to the id.</param>
    /// <param name="component">The name of the component that the bug
    /// will be created under.</param>
    /// <param name="version">The version of the product, that the bug
    /// was found in.</param>
    /// <param name="operatingSystem">(Defaulted) The operating system
    /// the bug was discovered on.</param>
    /// <param name="platform">(Defaulted) What type of hardware the
    /// bug was experienced on.</param>
    /// <param name="summary">Summary of the bug.</param>
    /// <param name="description">(Defaulted) Description of the
    /// bug.</param>
    /// <param name="priority">(Defaulted) What order the bug will be
    /// fixed in by the developer, compared to the developer's other
    /// bugs.</param>
    /// <param name="severity">(Defaulted) How severe the bug
    /// is.</param>
    /// <param name="status">(Optional) The status that this bug
    /// should start out as. Note that only certain statuses can be
    /// set on bug creation.</param>
    /// <param name="targetMilestone">(Optional) A valid target
    /// milestone for this product.</param>
    /// <param name="assignedTo">(Optional) A user to assign this bug
    /// to, if you don't want it to be assigned to the component
    /// owner.</param>
    /// <param name="cc">(Optional) An array of usernames to CC on
    /// this bug.</param>
    /// <returns>The newly created bug.</returns>
    /// <remarks>
    /// <para>The parameters for this call can be marked "optional"
    /// or "defaulted". Optional parameters can be left out in all
    /// Bugzilla installations (i.e. receive <b>null</b>), and a default value
    /// from the server will be substituted.
    /// Parameters marked as Defaulted can be left out in
    /// some installations, while other installations may require
    /// these parameters to be present. This is decided in the
    /// Bugzilla preferences. If you wish to make sure that the call
    /// works with all Bugzilla installations, you should supply
    /// values for all "defaulted" parameters.</para>
    /// <para>It is recommended that you use <see cref="GetLegalFieldValues"/> to
    /// retrieve legal values for parameters such as <paramref name="component"/>,
    /// <paramref name="version"/>, <see cref="operatingSystem"/>, and other parameters
    /// that have pre-configured values on the server.</para>
    /// </remarks>
    public Bug CreateBug(
          string alias, string component,
          string version, string operatingSystem,
          string platform, string summary,
          string description, string priority,
          string severity, string status,
          string targetMilestone, string assignedTo,
          string[] cc) {

      return CreateBug(alias, component, version, operatingSystem, platform, summary,
        description, priority, severity, status, targetMilestone, assignedTo, cc,
        null);
    }

  }
}
