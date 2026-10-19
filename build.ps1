# Compila o carregador (bin\GxDracula.dll, vai para GeneXus18\Packages) e o tema
# (bin\GxDracula.Theme.dll, copiado para %LOCALAPPDATA%\GxDracula e recarregado a quente pela IDE).
$ErrorActionPreference = 'Stop'
$g = 'C:\Program Files (x86)\GeneXus\GeneXus18'
$csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe'
$bin = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

$sdk = 'Artech.Architecture.Common', 'Artech.Architecture.UI.Framework', 'Artech.Common', 'Artech.Common.Framework',
    'Artech.Udm.Framework', 'Artech.Common.Properties', 'Artech.Architecture.Interfaces', 'Artech.Common.Helpers' |
    Where-Object { Test-Path "$g\$_.dll" } | ForEach-Object { "/r:$g\$_.dll" }

& $csc /nologo /target:library "/out:$bin\GxDracula.dll" @sdk /r:System.Windows.Forms.dll "$PSScriptRoot\src\Package.cs"
if ($LASTEXITCODE) { throw 'falhou o carregador' }
& $csc /nologo /target:library "/out:$bin\GxDracula.Theme.dll" /r:System.Windows.Forms.dll /r:System.Drawing.dll "$PSScriptRoot\src\Theme.cs" "$PSScriptRoot\src\Tabs.cs" "$PSScriptRoot\src\Fonts.cs" "$PSScriptRoot\src\DrawFilter.cs" "$PSScriptRoot\src\Extra.cs" "$PSScriptRoot\src\Isl.cs" /r:System.Xml.dll "/r:$g\Infragistics2.Win.v11.1.dll" "/r:$g\Infragistics2.Shared.v11.1.dll"
if ($LASTEXITCODE) { throw 'falhou o tema' }

$dest = Join-Path $env:LOCALAPPDATA 'GxDracula'
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item "$bin\GxDracula.Theme.dll" $dest -Force
Get-ChildItem "$bin\*.dll", "$dest\*.dll" | Select-Object FullName, Length, LastWriteTime
