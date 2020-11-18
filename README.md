### Overview
This repo contains a port of core iLand components from C++ to C# with code quality investments and feature changes. iLand's user interface and 
plugins are not included.

* These source directories are included: core, output, tests, 3rdparty (replaced with Mersenne twister nuget), tools
* These directories were not ported: apidoc, fonstudio, iland, ilandc, inits, plugins/{barkbeetle, fire, wind}
* The abe and abe/output directories were ported but are currently retained only in the feature/scripting branch.

Currently, e_sqlite3.dll needs to be copied from %OutDir%\net50\runtimes\win-x64\native to %OutDir% as a post build step
due to [Entity Framework issue 19396](https://github.com/dotnet/efcore/issues/19396). This is a one time step so is not automated.

### Dependencies
This port of iLand is a .NET 5.0 assembly whose PowerShell cmdlets therefore require Powershell Core 7.1.0 or newer. Use of .NET 5.0 also motivates 
the use of Microsoft.Data.Sqlite rather than System.Data.Sqlite due to a smaller set of dependencies.

### Relationship to iLand 1.0 (2016)
Code in this repo derives from the [iLand](http://iland.boku.ac.at/) 1.0 spatial growth and yield model. The primary changes
are

* Separation of the landscape from the model which acts upon it. Corresponding rationalization of the project file schema to group settings more naturally
  and increase naming clarity and consistency.
* Removal of static state so that multiple iLand models can be instantiated in the same app domain. As a corollary, species names are no
  longer replaced with their species set indices within expressions as this [management filtering feature](http://iland.boku.ac.at/Expression#Constants)
  relied on static lookups from deep within the parse stack.
* Replacement of the GlobalSettings::instance()->settings() and DOM XML reparsing with conventional XML deserialization which checks for schema errors
  such as typos and misplaced elements that were previously ignored. This induces improvements in state handling internal to iLand as parameter splitting 
  between global settings and the global property bag and overwriting of such settings has been removed. In particular, the EnvironmentReader class 
  (the Environment class in C++) now functions as conventional enumerator of resource unit properties rather than as a property bag manipulator. 
  Associated code defects in initialization file loading, constant nitrogen, and dynamic nitrogen have been fixed.
* Conversion to managed code, replacing pointers with indices and C# 9/.NET 5.0 nullable annotation. Structured exception handling is converted to
  C#'s exception model. The SQL data layer is rewritten using managed Sqlite and GridRunner<T> becomes the IEnumerator<T>-like GridWindowEnumerator<T>.
* Standardization on single precision for most calculations rather than retaining the mixed and variable use of single and double precision of the C++
  build.
* Deprecation of trace based debugging output in favor of debug assertions and exceptions.

### Known Issues

* The project-level switch for expression linearization is not consistently translated into calls to Linearize(). This is inherited from
  the C++ version of iLand.
