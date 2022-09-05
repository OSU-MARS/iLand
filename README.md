### Overview
This repo contains a port of core iLand 1.0 components from C++ to C# with code quality investments and feature changes. iLand's user interface and 
plugins are not included.

* These source directories are included: core, output, tests, 3rdparty (replaced with Mersenne twister nuget), tools
* These directories were not ported: apidoc, fonstudio, iland, ilandc, inits, plugins/{barkbeetle, fire, wind}
* The abe and abe/output directories were ported but are currently retained only in the feature/scripting branch.

### Dependencies
This port of iLand is a .NET 6.0 assembly whose PowerShell cmdlets require [Powershell 7.3](https://github.com/PowerShell/PowerShell) or newer. 
Feather files and SQLite databases are both input and output formats, resulting in use of Microsoft.Data.Sqlite (which, as of .NET 6.0, has
fewer dependencies than System.Data.Sqlite) and Apache Arrow. GDAL is also used for logging light and height grids to GeoTIFF. If compatible
GDAL binaries aren't included in `$env:PATH` GeoTIFF logging will fail when iLand PowerShell cmdlets are invoked.

Elements of weather and CO₂ time series must be provided in chronological order. While not required, weather files are read somewhat more 
quickly if they list time series of equal length sequentially (series ID 1: month 1, month 2..., series ID 2: month 1, month 2, ...) rather 
than listing all values for a given time before moving to the next time (month 1: series ID 1, series ID 2, ..., month 2: series ID 1, series 
ID 2, ...). As [ClimateNA](https://climatena.ca/) and related downscaling tools use the latter ordering, use of both [`dplyr::arrange()`](https://dplyr.tidyverse.org/reference/arrange.html)
and [`arrow::write_feather()`](https://arrow.apache.org/docs/r/reference/write_feather.html) (or equivalents) is suggested to lower large file 
read times. Similarly, runtimes may be slightly lower if resource units are ordered from east to west and south to north and trees are grouped
by resource unit in input files. This matches the file's spatial ordering to iLand's internal spatial ordering and, while not enough of an
advantage for it to be worth performing sorting iLand, a one time sort in R (`arrange(resourceUnitY, resourceUnitX, treeSpecies, treeY, treeX)`)
may be worthwhile.

As of Arrow 9.0.0, Apache C# bindings do not support compressed feather files and replacement dictionaries are broken. While iLand works around
these limitations as best it can supporting use of `write_feather(compression = "uncompressed")` and `factor()` may be helpful in R. Also,
`read_feather()` defaults to `mmap = TRUE` and therefore holds feather files open for the remainder of an R session. Since this prevents
rewriting the files from PowerShell after rerunning iLand it's likely convenient to use `read_feather(mmap = FALSE)`.

Like many .NET class libraries and PowerShell modules, this iLand port is operating system and processor agnostic. Development 
and use occurs on Windows but binaries compiled on Windows have been verified to be xcopyable to Linux and ran without issues. Unless disabled 
in the project file, SIMD instructions are used on processors supporting AVX2 (Intel 4<sup>th</sup> generation and newer, from 2013, and AMD
Excavator, from 2015). Performance optimizations currently target AMD Zen 3.

### Relationship to iLand 1.0 (2016)
Code in this repo derives from the [iLand 1.0](http://iland-model.org/) spatial growth and yield model. The official iLand 1.0 release has been
used primarily for modeling in Europe and contains an example model from Kalkalpen National Park in Austria. The main code changes in this 
repo are

* Separation of the landscape from the model which acts upon it. Corresponding rationalization of the project file schema to group settings 
  more naturally and increase naming clarity and consistency.
* Separation of input parsing from data objects, simplifying support for multiple input formats. Feather (Apache Arrow) is supported, within 
  Arrow 9's C# limitations, as a binary alternative to SQL databases and parsing CSV of other delimited files. Feather simplifies integration 
  with R for iLand project setup and data analysis.
* Separation of output objects and calculations from model calculations and objects, disentangling the object model and reducing unnecessary 
  calculation.
* Faster timestepping from more consistent use of synchronous thread-level parallelism (~30% reduction in step time, depending on settings and
  processor core availability). Overlapped IO threads are sometimes also used to reduce file read and write times.
* Removal of fixed light reduction around model edges. This edge correction was inaccurate for open edges, such as bodies of water, as well as
  for typical closed canopy forest. Larger trees would both stamp and and read beyond the correction width.
* Removal of static state so that multiple iLand models can be instantiated in the same app domain. As a corollary, species names are no
  longer replaced with their species set indices within expressions as [management filtering](http://iland-model.org/Expression#Constants)
  relied on static lookups from deep within the parse stack.
* Standardization on single precision rather than retaining the mixed and variable use of single and double precision of the C++ build. This 
  essentially halves the memory footprint of floating point data and reduces calculation and single precision exponentiation, logarithms, square 
  roots, and trigonometric functions evaluate more quickly than their double precision equivalents. Numeric drift remains negligible compared to 
  simulation uncertainty.
* Conversion to managed code, replacing pointers with indices and C# nullable annotation. Structured exception handling is converted to C#'s 
  exception model. The SQL data layer is rewritten using managed Sqlite and GridRunner<T> becomes the IEnumerator<T>-like GridWindowEnumerator<T>.
* Replacement of the GlobalSettings::instance()->settings() and DOM XML reparsing with conventional XML deserialization which checks for schema
  errors such as typos and misplaced elements that were previously ignored. This induces improvements in state handling internal to iLand as 
  parameter splitting between global settings and the global property bag and overwriting of such settings has been removed. In particular, the 
  ResourceUnitReader class (the Environment class in C++) now functions as conventional enumerator of resource unit properties rather than as a
  property bag manipulator. Associated code defects in initialization file loading, constant nitrogen, and dynamic nitrogen have been fixed.
* Deprecation of trace based debugging output in favor of debug assertions, exceptions, and expansion of unit test coverage. As practical, 
  interactions are refactored for increased encapsulation, dead code is removed, and duplicate code consolidated. Renaming for clarity and C# 
  style conformance is substantial.

Tree light stamps are project specific because they include shading as a function of latitude. But, since a port of [Lightroom](https://iland-model.org/Lightroom) 
is not included in this repo it is needed to use FONStudio from iLand C++ to generate project specific stamps.

### Relationship to iLand 0.8 (2014) and earlier
iLand was initially applied to simulation of the [HJ Andrews Experimental Forest in Oregon](https://andrewsforest.oregonstate.edu/) with 
publication in 2012 ([Seidl *et al*. 2012](https://doi.org/10.1016/j.ecolmodel.2012.02.015)) and and continued to ship with the HJ Andrews as 
its example project through version 0.8. Since this repo is devoted to continued modeling of Pacific Northwest sites it contains an update of the 
0.8 Pacific Northwest species parameter database to the iLand 1.0 schema and separates .lip files by region. Douglas-fir (*Pseudotsuga menziesii*)
is native to the Pacific Northwest and a European plantation species, so is supported on both continents with differing light intensity profiles 
and parameterizations.

### Known issues inherited from iLand 1.0 C++
* Leaf phenology is hard coded for northern hemisphere temperate and boreal sites, preventing support for deciduous species in the southern
  hemisphere and likely inhibiting modeling on tropical sites. Chilling day calculations for establishment of evergreen species are also likely 
  to be incorrect in these locations.
* Sun angles are aren't adjusted for leap years. Angles and solstice dates therefore accumulate up to one day of error over the four year leap 
  year cycle. Biases are also introduced by the use of fixed values for the Earth's axial tilt and neglect of latitudinal variation across the 
  simulation area, though these effects are likely small compared to those of atmospheric refraction, terrain occultation, and slope.
* The project level switch for expression linearization is not consistently translated into calls to Linearize().

### iLand 1.0 C++ issues fixed
* ~50% crash probability per run reduced to negligible risk, dramatically improving useability.
* Miscounting of tree presence in height grid cells for resource unit occupancy (very likely a Qt 6 compiler order of operations defect, possibly
  specific to toroidal resource units).
* Dominant height field establishment and light stamping were not thread safe. Both operations run in parallel at the resource unit level and, 
  when a tree's height field or light stamp reaches into an adjacent resource unit, it is possible two threads may stamp the same heihgt or
  light grid cell at the same time, resulting in a race condition where one, or possibly more, trees' heights or shading contributions are lost. 
  The 1.0 C++ implementation did not guard against either of these overlap cases and also did not guarantee establishment of the dominant 
  height field prior to beginning light stamping. The C# implementation avoids all three race conditions by completing the height field before
  beginning light stamping and taking writer locks on both the height and light grids when needed.
* Moving average of daily soil water potential for sapling establishment restarted every January 1st even though the calendar year boundary is 
  usually within the growing season on tropical and southern hemisphere sites.
* ResourceUnitSoil carbon and nitrogen inputs were off by a factor of 10,000 because resource units' in landscape areas in m² were misinterpreted as
  areas in hectares.
* Positioning of light grid origin at resource unit grid orientation lead integer truncation of light grid indexes to double stamp the x = 0 column
  and y = 0 row of the light grid, leading to competition overestimation and growth underestimation of trees in this row and column.
* The last day of a leap year was a leaf off day for evergreen species, meaning no photosynthesis would occur that day.
* State mishandling in SummaryStatistics allowed invalid median values and standard deviations.
* Due to numerical precision and interpretations of geometrical semantics, GIS tools and iLand may differ over which resource unit contains trees
  on a boundary within the resource unit grid. In many cases this is of little importance as resource units are present on both sides of the edge.
  However, edge trees may fall off the populated portion of the resource unit grid. Such trees are detected and nudged by 1 cm to move them onto
  the populated side of the edge.

Decoupling from Qt additionally exempts iLand development from Qt licensing terms (minimum US$ 302/month, as of July 2022, or Qt code contributions
in kind), reducing the cost of open source software.

### Developer Notes
Primary development is done with [Visual Studio 2022 Community](https://visualstudio.microsoft.com/) but any other .NET 6.0 toolchain is expected
to work, including [Visual Studio Code](https://dotnet.microsoft.com/en-us/platform/tools) on Linux.

After the first time a repo is built, e_sqlite3.dll needs to be copied from %OutDir%\net60\runtimes\win-x64\native to %OutDir% as a post build 
step due to [Entity Framework issue 19396](https://github.com/dotnet/efcore/issues/19396). This is a one time task so is not automated.
