$unitTestPath = $PSScriptRoot
#$unitTestPath = ([System.IO.Path]::Combine($env:USERPROFILE, "PhD\\iLand\\UnitTests"))
$buildDirectory = ([System.IO.Path]::Combine($unitTestPath, "bin\\x64\\Debug\\net7.0"))
#$buildDirectory = ([System.IO.Path]::Combine($unitTestPath, "bin\\x64\\Release\\net7.0"))
Import-Module -Name ([System.IO.Path]::Combine($buildDirectory, "iLand.dll"));
$env:PATH = $env:PATH + (';' + $buildDirectory + '\runtimes\win-x64\native') # for GDAL .dll loading if GeoTIFF logging is enabled

## model instantiation, simulation, and transfer of in memory trajectories to disk
# powerShellTests.R covers reading the trajectories
$elliott = Get-Trajectory -Project ([System.IO.Path]::Combine($unitTestPath, "Elliott\\Elliott.xml")) -Years 10 -Verbose
Write-Trajectory -Trajectory $elliott -IndividualTreeFile ([System.IO.Path]::Combine($unitTestPath, "..\\TestResults\\Elliott individual tree trajectories.feather")) -ResourceUnitFile ([System.IO.Path]::Combine($unitTestPath, "..\\TestResults\\Elliott resource unit trajectories.feather")) -StandFile ([System.IO.Path]::Combine($unitTestPath, "..\\TestResults\\Elliott stand trajectories.feather")) -ThreePGFile ([System.IO.Path]::Combine($unitTestPath, "..\\TestResults\\Elliott 3-PG.feather"))

## light stamps
#Export-LightStamps -ProjectLip ([System.IO.Path]::Combine($unitTestPath, "Kalkalpen\\lip")) -CsvDirectory ([System.IO.Path]::Combine($unitTestPath, "..\\R\\stamps\\Kalkalpen"))
#Export-LightStamps -ProjectLip ([System.IO.Path]::Combine($unitTestPath, "Elliott\\lip")) -CsvDirectory ([System.IO.Path]::Combine($unitTestPath, "..\\R\\stamps\\Pacific Northwest"))
