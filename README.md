### Overview
This repo contains a port of core iLand components from C++ to C#. iLand's user interface and plugins are not included.

* These source directories are included: abe, core, output, tests, 3rdparty (replaced with Mersenne twister nuget), tools
* These directories were not ported: apidoc, fonstudio, iland, ilandc, inits, plugins/{barkbeetle, fire, wind}

The agent-based scripting engine (abe directory) compiles against a mock JavaScript engine but does not run and therefore hasn't been tested. The mock
interfaces could presumably be replaced with the MsieJavaScriptEngine or an equivalent nuget if needed.

### Dependencies
This port of iLand is a .NET Core 3.1 assembly. Use of .NET Core 3.1 also motivates the use of Microsoft.Data.Sqlite rather than System.Data.Sqlite
due to a smaller set of dependencies.

### Relationship to iLand 1.0 (2016)
Code in this repo derives from the [iLand](http://iland.boku.ac.at/) 1.0 spatial growth and yield model. The primary changes
are

* Conversion to managed code, replacing pointers with indices and structured exception handling with C#'s exception model. GridRunner<T> shifts towards
  IEnumerator<T>.
* SQL data layer rewritten to managed Sqlite API.
