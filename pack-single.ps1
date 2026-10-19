# Gera dist\GxDracula-Instalador.ps1: um arquivo só, com o zip embutido em base64 e as instruções
# para quem (pessoa ou agente de IA) for instalar. Rode pack.ps1 antes.
$ErrorActionPreference = 'Stop'
$zip = Join-Path $PSScriptRoot 'dist\GxDracula.zip'
if (-not (Test-Path $zip)) { throw 'rode pack.ps1 antes' }
$b64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($zip), [Base64FormattingOptions]::InsertLineBreaks)
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
$out = Join-Path $PSScriptRoot 'dist\GxDracula-Instalador.ps1'

$head = @'
<#
================================================================================================
 GxDracula - tema Dracula para a IDE do GeneXus 18 - INSTALADOR EM ARQUIVO ÚNICO
================================================================================================

 INSTRUÇÕES PARA O AGENTE DE IA (ou pessoa) QUE VAI INSTALAR
 ------------------------------------------------------------
 1. Pré-requisitos: Windows, GeneXus 18 instalado, PowerShell 5.1+ ou 7+.
 2. FECHE a IDE do GeneXus antes (confira: Get-Process GeneXus -ErrorAction SilentlyContinue
    deve vir vazio). Peça ao usuário para salvar e fechar; não mate o processo sem aval.
 3. Rode, NA SESSÃO DO USUÁRIO QUE VAI USAR A IDE (não como outro admin):
        powershell -ExecutionPolicy Bypass -File .\GxDracula-Instalador.ps1
    Simulação sem alterar nada:
        powershell -ExecutionPolicy Bypass -File .\GxDracula-Instalador.ps1 -DryRun
 4. Vai aparecer UM aviso do UAC (admin só para copiar para a pasta do GeneXus e rodar
    "GeneXus.exe /install"). O usuário precisa aceitar.
 5. Sucesso = a mensagem "Instalado" e, ao abrir o GeneXus, a IDE escura. Log do tema:
    %TEMP%\GxDracula.log (deve ter linhas recentes depois de abrir a IDE).

 PARÂMETROS
   -GeneXusPath 'D:\GeneXus18'  GeneXus fora de Program Files (x86)\GeneXus\GeneXus18
   -LiberarPreviewWWP           permissão de escrita na pasta de preview do WorkWithPlus
                                (corrige "Access denied ... .css" / preview em branco no Patterns)
   -DryRun                      só mostra o que faria
   -Desinstalar                 remove o tema (IDE fechada)
   -ExtrairPara <pasta>         só extrai o pacote (DLLs, fontes, MANUAL.md) e sai

 O QUE O INSTALADOR FAZ
   - copia GxDracula.dll para <GeneXus18>\Packages e roda GeneXus.exe /install;
   - copia o tema (GxDracula.Theme.dll + ícones Codicons) para %LOCALAPPDATA%\GxDracula;
   - instala a fonte Inter (OFL) só para o usuário atual.
   Não altera KB nem objetos.

 DESLIGAR SEM DESINSTALAR
   arquivo vazio %APPDATA%\GxDracula.disabled (tema) ou %APPDATA%\GxDracula.icons.disabled (ícones).

 Integridade do pacote embutido (SHA256 do zip): __HASH__
================================================================================================
#>
param(
    [string]$GeneXusPath,
    [switch]$LiberarPreviewWWP,
    [switch]$DryRun,
    [switch]$Desinstalar,
    [string]$ExtrairPara
)
$ErrorActionPreference = 'Stop'
# O pacote (zip em base64) está no comentário do fim deste arquivo, depois do marcador.
$self = [IO.File]::ReadAllText($PSCommandPath)
$i = $self.LastIndexOf('#' + 'PAYLOAD#'); $j = $self.LastIndexOf('#>')
$Payload = $self.Substring($i + 9, $j - $i - 9)

$dest = if ($ExtrairPara) { $ExtrairPara } else { Join-Path $env:TEMP 'GxDracula-setup' }
$zipPath = Join-Path $env:TEMP 'GxDracula-setup.zip'
[IO.File]::WriteAllBytes($zipPath, [Convert]::FromBase64String($Payload))
if ((Get-FileHash $zipPath -Algorithm SHA256).Hash -ne '__HASH__') { throw 'Pacote embutido corrompido (SHA256 não confere).' }
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Expand-Archive $zipPath $dest -Force
Remove-Item $zipPath -Force
$root = Get-ChildItem $dest -Recurse -Filter install.ps1 | Select-Object -First 1 | ForEach-Object { $_.DirectoryName }
if (-not $root) { throw 'install.ps1 não encontrado no pacote.' }
if ($ExtrairPara) { Write-Host "Extraído em $root" -ForegroundColor Cyan; return }

if ($Desinstalar) { & (Join-Path $root 'uninstall.ps1') ; return }

$a = @{}
if ($GeneXusPath) { $a.GeneXusPath = $GeneXusPath }
if ($LiberarPreviewWWP) { $a.LiberarPreviewWWP = $true }
if ($DryRun) { $a.DryRun = $true }
# A pasta extraída fica em %TEMP%\GxDracula-setup: a auto-elevação do install.ps1 reabre o script de lá.
& (Join-Path $root 'install.ps1') @a
'@
$head = $head.Replace('__HASH__', $hash)
$text = $head + "`r`nreturn`r`n<#`r`n#PAYLOAD#`r`n" + $b64 + "`r`n#>`r`n"
[IO.File]::WriteAllText($out, $text, (New-Object Text.UTF8Encoding $true))
"OK: $out ($([math]::Round((Get-Item $out).Length/1KB)) KB)"
