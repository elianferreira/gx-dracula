<#
.SYNOPSIS
  Instala o tema Dracula na IDE do GeneXus 18.
.PARAMETER GeneXusPath
  Pasta do GeneXus 18 (padrão: Program Files (x86)\GeneXus\GeneXus18, ou a achada no registro).
.PARAMETER LiberarPreviewWWP
  Dá permissão Modify ao usuário em Packages\Patterns\WorkWithPlus\PreviewImages.
.PARAMETER DryRun
  Só mostra o que seria feito; não copia, não eleva, não roda /install.
.PARAMETER LocalAppData
  Uso interno: LOCALAPPDATA do usuário que invocou (repassado na auto-elevação).
.PARAMETER Usuario
  Uso interno: usuário que invocou (DOMINIO\nome), repassado na auto-elevação.
#>
param(
    [string]$GeneXusPath,
    [switch]$LiberarPreviewWWP,
    [switch]$DryRun,
    [string]$LocalAppData = $env:LOCALAPPDATA,
    [string]$Usuario = "$env:USERDOMAIN\$env:USERNAME",
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
    # Fonte Inter: por usuário (HKCU), então roda antes de elevar.
    if (-not $Elevado -and (Test-Path (Join-Path $PSScriptRoot 'fonts.ps1'))) {
        if ($DryRun) { & (Join-Path $PSScriptRoot 'fonts.ps1') -DryRun } else { & (Join-Path $PSScriptRoot 'fonts.ps1') }
    }

    # Auto-elevação (UAC)
    $admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $admin -and -not $DryRun) {
        Info 'Pedindo permissão de administrador (UAC)...'
        $args2 = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
            '-LocalAppData', "`"$LocalAppData`"", '-Usuario', "`"$Usuario`"", '-Elevado')
        if ($GeneXusPath) { $args2 += @('-GeneXusPath', "`"$GeneXusPath`"") }
        if ($LiberarPreviewWWP) { $args2 += '-LiberarPreviewWWP' }
        $p = Start-Process powershell -Verb RunAs -ArgumentList $args2 -Wait -PassThru
        exit $p.ExitCode
    }

    # Origem dos arquivos (pasta deste script)
    $origem = $PSScriptRoot
    foreach ($f in 'GxDracula.dll', 'GxDracula.Theme.dll', 'codicons\codicon.ttf', 'codicons\codicon.css') {
        if (-not (Test-Path (Join-Path $origem $f))) { throw "Arquivo ausente no pacote: $f. Extraia o zip inteiro antes de instalar." }
    }

    # Detecta o GeneXus 18
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
    Info "Usuário: $Usuario  (dados em $LocalAppData\GxDracula)"

    # IDE precisa estar fechada
    $rodando = Get-Process -Name GeneXus -ErrorAction SilentlyContinue | Where-Object { $_.Path -and $_.Path -like "$GeneXusPath\*" }
    if ($rodando) {
        if ($DryRun) { Write-Host '[DryRun] ATENÇÃO: a IDE está aberta; na instalação real o script recusaria.' -ForegroundColor Yellow }
        else { Write-Host 'A IDE do GeneXus está aberta. Feche-a (salve o trabalho) e rode o instalador de novo.' -ForegroundColor Red; Encerrar 2 }
    }

    # Carregador no Packages
    $packages = Join-Path $GeneXusPath 'Packages'
    Acao "Copiar GxDracula.dll -> $packages"
    if (-not $DryRun) { Copy-Item (Join-Path $origem 'GxDracula.dll') $packages -Force }

    # Tema + fontes no LOCALAPPDATA do usuário que invocou
    $dest = Join-Path $LocalAppData 'GxDracula'
    Acao "Copiar GxDracula.Theme.dll e codicons -> $dest"
    if (-not $DryRun) {
        New-Item -ItemType Directory -Force (Join-Path $dest 'codicons') | Out-Null
        Copy-Item (Join-Path $origem 'GxDracula.Theme.dll') $dest -Force
        Copy-Item (Join-Path $origem 'codicons\*') (Join-Path $dest 'codicons') -Force
    }

    # Estilo Infragistics (GxDracula.isl): gerado do GenexusOffice2010Silver.isl desta instalação; o tema carrega no Start.
    $isl = Join-Path $dest 'GxDracula.isl'
    if (Test-Path (Join-Path $origem 'make-isl.ps1')) {
        Acao "Gerar $isl a partir de $GeneXusPath\GenexusOffice2010Silver.isl"
        if (-not $DryRun) { & (Join-Path $origem 'make-isl.ps1') -GeneXusPath $GeneXusPath -Out $isl }
    }
    else { Write-Host 'Aviso: make-isl.ps1 ausente no pacote; sem o .isl parte da IDE fica clara.' -ForegroundColor Yellow }

    # Preview do WorkWithPlus
    if ($LiberarPreviewWWP) {
        $prev = Join-Path $GeneXusPath 'Packages\Patterns\WorkWithPlus\PreviewImages'
        if (-not (Test-Path $prev)) { Write-Host "Aviso: $prev não existe; permissão ignorada." -ForegroundColor Yellow }
        else {
            Acao "icacls: Modify para $Usuario em $prev"
            if (-not $DryRun) {
                & icacls $prev /grant "${Usuario}:(OI)(CI)M" /T | Out-Null
                if ($LASTEXITCODE) { throw "icacls falhou (código $LASTEXITCODE)" }
            }
        }
    }

    # Registra o package
    Acao "Executar: $GeneXusPath\GeneXus.exe /install (e esperar)"
    if (-not $DryRun) {
        Info 'Registrando o package (GeneXus.exe /install)...'
        $p = Start-Process "$GeneXusPath\GeneXus.exe" -ArgumentList '/install' -Wait -PassThru
        if ($p.ExitCode) { throw "GeneXus.exe /install terminou com código $($p.ExitCode)" }
    }

    if ($DryRun) { Write-Host 'DryRun concluído: nada foi alterado.' -ForegroundColor Yellow }
    else { Write-Host 'Instalado. Abra o GeneXus 18.' -ForegroundColor Green }
    Encerrar 0
} catch {
    Write-Host "ERRO: $($_.Exception.Message)" -ForegroundColor Red
    Encerrar 1
}
