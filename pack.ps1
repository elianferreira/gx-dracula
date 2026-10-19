# Gera dist\GxDracula\ e dist\GxDracula.zip (roda build.ps1 antes).
$ErrorActionPreference = 'Stop'
try {
    & (Join-Path $PSScriptRoot 'build.ps1') | Out-Null

    $dist = Join-Path $PSScriptRoot 'dist'
    $pasta = Join-Path $dist 'GxDracula'
    $zip = Join-Path $dist 'GxDracula.zip'
    $bin = Join-Path $PSScriptRoot 'bin'
    $fontes = Join-Path $env:LOCALAPPDATA 'GxDracula\codicons'

    if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
    New-Item -ItemType Directory -Force (Join-Path $pasta 'codicons') | Out-Null

    foreach ($f in 'GxDracula.dll', 'GxDracula.Theme.dll') {
        if (-not (Test-Path "$bin\$f")) { throw "não achei $bin\$f" }
        Copy-Item "$bin\$f" $pasta
    }
    foreach ($f in 'codicon.ttf', 'codicon.css') {
        if (-not (Test-Path "$fontes\$f")) { throw "não achei $fontes\$f" }
        Copy-Item "$fontes\$f" (Join-Path $pasta 'codicons')
    }
    New-Item -ItemType Directory -Force (Join-Path $pasta 'fonts') | Out-Null
    Copy-Item (Join-Path $env:LOCALAPPDATA 'GxDracula\fonts\*') (Join-Path $pasta 'fonts')
    Copy-Item "$PSScriptRoot\installer\fonts.ps1", "$PSScriptRoot\isl\make-isl.ps1" $pasta
    Copy-Item "$PSScriptRoot\installer\install.ps1", "$PSScriptRoot\installer\uninstall.ps1", "$PSScriptRoot\MANUAL.md" $pasta

    Compress-Archive -Path $pasta -DestinationPath $zip -Force

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $z = [IO.Compression.ZipFile]::OpenRead($zip)
    try { $z.Entries | Select-Object FullName, Length } finally { $z.Dispose() }
    Write-Host "OK: $zip"
} catch {
    Write-Host "ERRO: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
