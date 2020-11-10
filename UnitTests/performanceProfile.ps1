# $env:COREHOST_TRACE=1
#Set-Location -Path ([System.IO.Path]::Combine($env:USERPROFILE, "source\\repos\\iLand\\UnitTests\\bin\\x64\\Debug"))
#$buildDirectory = ([System.IO.Path]::Combine($env:USERPROFILE, "source\\repos\\iLand\\UnitTests\\bin\\x64\\Debug\\net5.0"))
Set-Location -Path ([System.IO.Path]::Combine($env:USERPROFILE, "source\\repos\\iLand\\UnitTests\\bin\\x64\\Release"))
$buildDirectory = ([System.IO.Path]::Combine($env:USERPROFILE, "source\\repos\\iLand\\UnitTests\\bin\\x64\\Release\\net5.0"))
Import-Module -Name ([System.IO.Path]::Combine($buildDirectory, "iLand.dll"));
Get-Trajectory -Project ([System.IO.Path]::Combine($env:USERPROFILE, "OSU\\iLand\\Malcolm Knapp\\Nelder 1.xml")) -Years 34 -Verbose
