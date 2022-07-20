# $env:COREHOST_TRACE=1
$buildDirectory = ([System.IO.Path]::Combine($PSScriptRoot, "bin\\x64\\Debug\\net6.0"))
#$buildDirectory = ([System.IO.Path]::Combine($PSScriptRoot, "bin\\x64\\Release\\net6.0"))
Import-Module -Name ([System.IO.Path]::Combine($buildDirectory, "iLand.dll"));
Get-Trajectory -Project ([System.IO.Path]::Combine($PSScriptRoot, "Elliott\\Elliott.xml")) -Years 10 -Verbose
