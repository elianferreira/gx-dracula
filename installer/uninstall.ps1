<#
.SYNOPSIS
  Remove o tema Dracula da IDE do GeneXus 18.
.PARAMETER Force
  Apaga %LOCALAPPDATA%\GxDracula sem pedir confirmação.
.PARAMETER DryRun
  Só mostra o que seria feito.
#>
param(
    [string]$GeneXusPath,
    [switch]$Force,
    [switch]$DryRun,
    [string]$LocalAppData = $env:LOCALAPPDATA,
    [switch]$Elevado
)
$ErrorActionPreference = 'Stop'

function Info($m) { Write-Host $m -ForegroundColor Cyan }
function Acao($m) { if ($DryRun) { Write-Host "[DryRun] $m" -ForegroundColor Yellow } else { Write-Host $m } }
function Encerrar($codigo) {
    if ($Elevado -and -not $DryRun) { Read-Host 'Pressione Enter para fechar' | Out-Null }
    exit $codigo
}

try {
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $admin -and -not $DryRun) {
        Info 'Pedindo permissão de administrador (UAC)...'
        $args2 = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-LocalAppData', "`"$LocalAppData`"", '-Elevado')
        if ($GeneXusPath) { $args2 += @('-GeneXusPath', "`"$GeneXusPath`"") }
        if ($Force) { $args2 += '-Force' }
        $p = Start-Process powershell -Verb RunAs -ArgumentList $args2 -Wait -PassThru
        exit $p.ExitCode
    }

    if (-not $GeneXusPath) {
        $GeneXusPath = Join-Path ${env:ProgramFiles(x86)} 'GeneXus\GeneXus18'
        if (-not (Test-Path "$GeneXusPath\GeneXus.exe")) {
            $chaves = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
                      'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
            $achado = Get-ItemProperty $chaves -ErrorAction SilentlyContinue |
                Where-Object { $_.DisplayName -like '*GeneXus*18*' -and $_.InstallLocation -and (Test-Path (Join-Path $_.InstallLocation 'GeneXus.exe')) } |
                Select-Object -First 1
            if ($achado) { $GeneXusPath = $achado.InstallLocation.TrimEnd('\') }
        }
    }
    if (-not (Test-Path "$GeneXusPath\GeneXus.exe")) {
        throw "GeneXus 18 não encontrado. Rode de novo com -GeneXusPath 'C:\caminho\GeneXus18'."
    }
    Info "GeneXus 18: $GeneXusPath"

    $rodando = Get-Process -Name GeneXus -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -like "$GeneXusPath\*" }
    if ($rodando) {
        if ($DryRun) { Write-Host '[DryRun] ATENÇÃO: a IDE está aberta; na desinstalação real o script recusaria.' -ForegroundColor Yellow }
        else { Write-Host 'A IDE do GeneXus está aberta. Feche-a e rode de novo.' -ForegroundColor Red; Encerrar 2 }
    }

    $dll = Join-Path $GeneXusPath 'Packages\GxDracula.dll'
    if (Test-Path $dll) {
        Acao "Remover $dll"
        if (-not $DryRun) { Remove-Item $dll -Force }
    } else { Write-Host 'GxDracula.dll já não está em Packages.' }

    Acao "Executar: $GeneXusPath\GeneXus.exe /install (e esperar)"
    if (-not $DryRun) {
        $p = Start-Process "$GeneXusPath\GeneXus.exe" -ArgumentList '/install' -Wait -PassThru
        if ($p.ExitCode) { throw "GeneXus.exe /install terminou com código $($p.ExitCode)" }
    }

    $dest = Join-Path $LocalAppData 'GxDracula'
    if (Test-Path $dest) {
        $apagar = $true
        if (-not $Force -and -not $DryRun) {
            $r = Read-Host "Apagar $dest ? (s/N)"
            $apagar = $r -match '^(s|sim|y|yes)$'
        }
        if ($apagar) {
            Acao "Apagar $dest"
            if (-not $DryRun) { Remove-Item $dest -Recurse -Force }
        } else { Write-Host "Mantido: $dest" }
    }

    if ($DryRun) { Write-Host 'DryRun concluído: nada foi alterado.' -ForegroundColor Yellow }
    else { Write-Host 'Desinstalado.' -ForegroundColor Green }
    Encerrar 0
} catch {
    Write-Host "ERRO: $($_.Exception.Message)" -ForegroundColor Red
    Encerrar 1
}
