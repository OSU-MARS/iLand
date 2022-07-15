### Overview
This repo contains a port of core iLand 1.0 components from C++ to C# with code quality investments and feature changes. iLand's user interface and 
plugins are not included.

* These source directories are included: core, output, tests, 3rdparty (replaced with Mersenne twister nuget), tools
* These directories were not ported: apidoc, fonstudio, iland, ilandc, inits, plugins/{barkbeetle, fire, wind}
* The abe and abe/output directories were ported but are currently retained only in the feature/scripting branch.

Currently, e_sqlite3.dll needs to be copied from %OutDir%\net60\runtimes\win-x64\native to %OutDir% as a post build step
due to [Entity Framework issue 19396](https://github.com/dotnet/efcore/issues/19396). This is a one time step so is not automated.

### Dependencies
This port of iLand is a .NET 6.0 assembly whose PowerShell cmdlets require [Powershell 7.2](https://github.com/PowerShell/PowerShell) or newer. Use of 
.NET 6.0 also motivates the use of Microsoft.Data.Sqlite rather than System.Data.Sqlite due to a smaller set of dependencies.

### Relationship to iLand 1.0 (2016)
Code in this repo derives from the [iLand 1.0](http://iland-model.org/) spatial growth and yield model. The official iLand 1.0 release is used primarily
for modeling in Europe and contains an example model from Kalkalpen National Park in Austria. The primary code level changes in this repo are

* Separation of the landscape from the model which acts upon it. Corresponding rationalization of the project file schema to group settings more naturally
  and increase naming clarity and consistency.
* Separation of input parsing from data objects, simplifying support for multiple input formats. Feather (Apache Arrow) is supported, within Arrow 8's 
  C# limitations, as a binary alternative to SQL databases and parsing CSV of other delimited files. Feather simplifies integration with R for iLand 
  project setup and data analysis.
* Removal of static state so that multiple iLand models can be instantiated in the same app domain. As a corollary, species names are no
  longer replaced with their species set indices within expressions as this [management filtering feature](http://iland-model.org/Expression#Constants)
  relied on static lookups from deep within the parse stack.
* Replacement of the GlobalSettings::instance()->settings() and DOM XML reparsing with conventional XML deserialization which checks for schema errors
  such as typos and misplaced elements that were previously ignored. This induces improvements in state handling internal to iLand as parameter splitting 
  between global settings and the global property bag and overwriting of such settings has been removed. In particular, the ResourceUnitReader class 
  (the Environment class in C++) now functions as conventional enumerator of resource unit properties rather than as a property bag manipulator. 
  Associated code defects in initialization file loading, constant nitrogen, and dynamic nitrogen have been fixed.
* Conversion to managed code, replacing pointers with indices and C# nullable annotation. Structured exception handling is converted to C#'s exception 
  model. The SQL data layer is rewritten using managed Sqlite and GridRunner<T> becomes the IEnumerator<T>-like GridWindowEnumerator<T>.
* Standardization on single precision for most calculations rather than retaining the mixed and variable use of single and double precision of the C++
  build.
* Deprecation of trace based debugging output in favor of debug assertions, exceptions, and expansion of unit test coverage. Where practical, dead
  code is removed and duplicate code consolidated. Renaming for clarity and C# style conformance is substantial.

### Relationship to iLand 0.8 (2014) and earlier
iLand was initially applied to simulation of the [HJ Andrews Experimental Forest in Oregon](https://andrewsforest.oregonstate.edu/) with publication in
2012 ([Seidl *et al*. 2012](https://doi.org/10.1016/j.ecolmodel.2012.02.015)) and and continued to ship with the HJ Andrews as its example project through 
version 0.8. Since this repo is devoted to continued modeling of Pacific Northwest sites it contains an update of the 0.8 Pacific Northwest species 
parameter database to the iLand 1.0 schema and separates .lip files by region. Douglas-fir (*Pseudotsuga menziesii*) is native to the Pacific Northwest 
and a European plantation species, so is supported on both continents with differing light intensity profiles and parameterizations.

### Known issues inherited from iLand 1.0 C++
* The project level switch for expression linearization is not consistently translated into calls to Linearize().
* Leaf phenology is hard coded for northern hemisphere temperate and boreal sites, preventing support for deciduous species in the southern hemisphere
and likely inhibiting modeling on tropical sites. Chilling day calculations for establishment of evergreen species are also likely to be incorrect in
these locations.

### iLand 1.0 C++ issues fixed
* ~50% crash probability per run reduced to negligible risk, dramatically improving useability.
* Miscounting of tree presence in height grid cells for resource unit occupancy (very likely a Qt 6 compiler order of operations defect, possibly specific
to toroidal resource units).
* Moving average of daily soil water potential for sapling establishment restarted every January 1st even though the calendar year boundary is usually 
within the growing season on tropical and southern hemisphere sites.
* Positioning of light grid origin at resource unit grid orientation lead integer truncation of light grid indexes to double stamp the x = 0 column and
y = 0 row of the light grid, leading to competition overestimation and growth underestimation of trees in this row and column.

Decoupling from Qt additionally exempts iLand development from Qt licensing terms (minimum US$ 302/month, as of July 2022, or Qt code contributions in 
kind), reducing the cost of open source software.