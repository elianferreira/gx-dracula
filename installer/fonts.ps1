# Instala por usuário (sem admin) as fontes .ttf da pasta fonts\ ao lado deste script.
# Copia para %LOCALAPPDATA%\Microsoft\Windows\Fonts, registra em HKCU e carrega na sessão (AddFontResource + WM_FONTCHANGE).
param([string]$Origem = (Join-Path $PSScriptRoot 'fonts'), [switch]$DryRun)

$ErrorActionPreference = 'Stop'
$destino = Join-Path $env:LOCALAPPDATA 'Microsoft\Windows\Fonts'
$chave = 'HKCU:\Software\Microsoft\Windows NT\CurrentVersion\Fonts'

Add-Type -Namespace GxDracula -Name FontApi -MemberDefinition @'
[DllImport("gdi32.dll", CharSet = CharSet.Unicode)] public static extern int AddFontResourceW(string file);
[DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeoutW(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
'@ -ErrorAction SilentlyContinue

$ttfs = Get-ChildItem $Origem -Filter *.ttf
if (-not $ttfs) { Write-Warning "Nenhum .ttf em $Origem"; return }
if (-not $DryRun) {
    New-Item -ItemType Directory -Force $destino | Out-Null
    if (-not (Test-Path $chave)) { New-Item $chave -Force | Out-Null }
}

foreach ($f in $ttfs) {
    # "Inter-SemiBold.ttf" -> "Inter SemiBold (TrueType)"
    $nome = ($f.BaseName -replace '-', ' ') + ' (TrueType)'
    $alvo = Join-Path $destino $f.Name
    if ($DryRun) { Write-Host "[dry-run] $($f.Name) -> $alvo ; HKCU '$nome'"; continue }
    Copy-Item $f.FullName $alvo -Force
    New-ItemProperty -Path $chave -Name $nome -Value $alvo -PropertyType String -Force | Out-Null
    $n = [GxDracula.FontApi]::AddFontResourceW($alvo)
    Write-Host "$($f.Name): instalada (AddFontResource=$n)"
}

if (-not $DryRun) {
    $r = [IntPtr]::Zero
    # HWND_BROADCAST, WM_FONTCHANGE, SMTO_ABORTIFHUNG
    [void][GxDracula.FontApi]::SendMessageTimeoutW([IntPtr]0xffff, 0x001D, [IntPtr]::Zero, [IntPtr]::Zero, 2, 1000, [ref]$r)
}
