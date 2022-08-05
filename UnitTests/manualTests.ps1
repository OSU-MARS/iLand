$buildDirectory = ([System.IO.Path]::Combine($PSScriptRoot, "bin\\x64\\Debug\\net6.0"))
#$buildDirectory = ([System.IO.Path]::Combine($PSScriptRoot, "bin\\x64\\Release\\net6.0"))
Import-Module -Name ([System.IO.Path]::Combine($buildDirectory, "iLand.dll"));

Export-LightStamps -ProjectLip ([System.IO.Path]::Combine($PSScriptRoot, "Kalkalpen\\lip")) -CsvDirectory ([System.IO.Path]::Combine($PSScriptRoot, "..\\R\\stamps\\Kalkalpen"))
Export-LightStamps -ProjectLip ([System.IO.Path]::Combine($PSScriptRoot, "Elliott\\lip")) -CsvDirectory ([System.IO.Path]::Combine($PSScriptRoot, "..\\R\\stamps\\Pacific Northwest"))
