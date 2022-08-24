﻿# $env:COREHOST_TRACE=1
#$buildDirectory = ([System.IO.Path]::Combine($PSScriptRoot, "bin\\x64\\Debug\\net6.0"))
$buildDirectory = ([System.IO.Path]::Combine($PSScriptRoot, "bin\\x64\\Release\\net6.0"))
Import-Module -Name ([System.IO.Path]::Combine($buildDirectory, "iLand.dll"));

# profile unit test version of Elliott State Research Forest project
# length of available weather record controls number of years simulated: 2011 to 2100 = 89 years
Get-Trajectory -Project ([System.IO.Path]::Combine($PSScriptRoot, "Elliott\\Elliott.xml")) -Years 89 -Verbose

# profile full Elliott State Research Forest project
#$elliottPath = ([System.IO.Path]::Combine($env:USERPROFILE, "PhD\\Elliott\\iLand"))
#$elliott = Get-Trajectory -Project ([System.IO.Path]::Combine($elliottPath, "Elliott.xml")) -Years 10 -Verbose
