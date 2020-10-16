### Overview
This repo contains a port of core iLand components from C++ to C#. iLand's user interface and plugins are not included.

* These source directories are included: core, output, tests, 3rdparty (replaced with Mersenne twister nuget), tools
* These directories were not ported: apidoc, fonstudio, iland, ilandc, inits, plugins/{barkbeetle, fire, wind}
* The abe and abe/output directories were ported but are currently retained only in the feature/scripting branch.

Currently, e_sqlite3.dll needs to be copied from %OutDir%\netcoreapp3.1\runtimes\win-x64\native to %OutDir% as a post build step
due to [Entity Framework issue 19396](https://github.com/dotnet/efcore/issues/19396).

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
* Replacement of the GlobalSettings::instance()->settings() and XmlSerializer system with conventional XML deserialization that checks
  for errors such as typos and misplaced elements which were previously ignored. This induces improvements in state handling internal to
  iLand as parameter splitting between global settings and the global property bag and overwriting of such settings has been removed. In
  particular, the Environment class now functions as conventional enumerator of resource unit properties rather than as a property bag
  manipulator. Associated code defects in initialization file loading, constant nitrogen, and dynamic nitrogen have been fixed. However,
  while input validation and parameter defaulting has been made more robust, it remains substantially incomplete. Conversion of classes
  in the iLand.Input.ProjectFile namespace from XmlSerializer to IXmlSerializable would support more specific validation at deserialiation 
  time and allow compile time enforcement of a read only contract on project files.
* Deprecation of trace based debugging output in favor of debug assertions and exceptions.

### Known Issues

* The project-level switch for expression linearization is not consistently translated into calls to Linearize(). This is inherited from
  the C++ version of iLand.
* Sgen compile time generation of serialiation assemblies is, as often the case, broken as of Visual Studio 2019 16.7.6. An XML
  deserialization start up penalty of about 250 ms results at project load time.