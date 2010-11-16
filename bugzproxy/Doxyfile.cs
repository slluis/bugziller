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
  \brief This file only contains doxygen comments/documentation. */

/*! \namespace Bugzproxy
  \brief The namespace of all the code in this assembly. 

  The namespace contains the interfaces and classes that makes up the assembly. 
*/

/*! \namespace Bugzproxy::ProxyStructs

  \brief Structs passed to the proxy stub created by the xml-rpc.net
  framework. 

  The structs in this namespace are used internally.
*/

/*! \mainpage C# Bugzilla WebService Access Assembly
  \section mp_intro_sec Introduction

  This is the documentation for %BugzillaProxy, the C# %Bugzilla
  WebService Access Assembly. For more information about what it is,
  and why to use it, consult http://oss.dbc.dk/bugzproxy .

  \section mp_getting_started Getting Started

  Either check out the Bugzproxy.Server class, or the example
  page. 

  All examples are compilable (they are part of the source
  distribution) and take a common set of options. Use the "--help"
  option to each to get the options. By default all programs use the
  Bugzilla "landfill" server, but you can change this using the
  options.

  All example programs also accept a "--trace" option, that will dump
  the communication with the server to std.out. Please note, that some
  issues, such as supplying a non-existant server, will not be
  indicated in the trace, but by exceptions.

  \section mp_error Error Handling
  
  In general, all methods throw exceptions on errors.

*/

/*! \example Test.cs

* This is a rather simple program, that simply tests that all
* functions can actually be called, but does not actually check that
* the results are meaningful (xml-rpc.net does check that the
* responses can be cast to the strucs that are expected though). As
* such is it not a great example of anything in particular, but you
* may wish to consult it to get a feel for the API. It is mostly used
* by the developers to check that nothing is really broken, when
* changing the library code.
*
*/
