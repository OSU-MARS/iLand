### Overview
This repo contains a port of core iLand components from C++ to C#. iLand's user interface and plugins are not included.

* These source directories are included: core, output, tests, 3rdparty (replaced with Mersenne twister nuget), tools
* These directories were not ported: apidoc, fonstudio, iland, ilandc, inits, plugins/{barkbeetle, fire, wind}
* The abe and abe/output directories were ported but are currently retained only in the feature/scripting branch.

### Dependencies
This port of iLand is a .NET Core 3.1 assembly. Use of .NET Core 3.1 also motivates the use of Microsoft.Data.Sqlite rather than System.Data.Sqlite
due to a smaller set of dependencies.

### Relationship to iLand 1.0 (2016)
Code in this repo derives from the [iLand](http://iland.boku.ac.at/) 1.0 spatial growth and yield model. The primary changes
are

* Conversion to managed code, replacing pointers with indices and structured exception handling with C#'s exception model. GridRunner<T> 
  shifts towards IEnumerator<T>. The SQL data layer is rewritten using managed Sqlite.
* Removal of static state so that multiple iLand models can be instantiated in the same app domain. As a corollary, species names are no
  longer replaced with their species set indices within expressions as this [management filtering feature](http://iland.boku.ac.at/Expression#Constants)
  relied on static lookups from deep within the parse stack.
* Deprecation of trace based debugging output in favor of debug assertions and exceptions.