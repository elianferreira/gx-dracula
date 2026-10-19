# Gera o GxDracula.isl a partir do estilo oficial da IDE (GenexusOffice2010Silver.isl, Infragistics AppStyling).
# O .isl é o canal de estilo da própria IDE (GeneXusMain carrega via StyleManager.Load no startup): trocar as cores
# aqui pinta docking, abas MDI, toolbars, menus, grids, árvores, scrollbars e MessageBox sem reflection nem timer.
param(
    [string]$GeneXusPath = 'C:\Program Files (x86)\GeneXus\GeneXus18',
    [string]$Out = (Join-Path $env:LOCALAPPDATA 'GxDracula\GxDracula.isl')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$P = @{
    Background = '40, 42, 54'; CurrentLine = '68, 71, 90'; Foreground = '248, 248, 242'; Comment = '98, 114, 164'
    Darker = '33, 34, 44'; Darkest = '25, 26, 33'; Mid = '52, 55, 70'
    Purple = '189, 147, 249'; Pink = '255, 121, 198'; Cyan = '139, 233, 253'; Green = '80, 250, 123'
    Orange = '255, 184, 108'; Red = '255, 85, 85'; Yellow = '241, 250, 140'
}

function Parse([string]$v) {
    if ($v -match '^\s*(\d+),\s*(\d+),\s*(\d+)\s*$') { return [System.Drawing.Color]::FromArgb([int]$matches[1], [int]$matches[2], [int]$matches[3]) }
    if ($v -match '^\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+)\s*$') { return [System.Drawing.Color]::FromArgb([int]$matches[1], [int]$matches[2], [int]$matches[3], [int]$matches[4]) }
    $c = [System.Drawing.Color]::FromName($v)
    if ($c.IsKnownColor) { return $c }
    return $null
}

function Accent($c) {
    $h = $c.GetHue()
    if ($h -lt 20 -or $h -ge 340) { return $P.Red }
    if ($h -lt 45) { return $P.Orange }
    if ($h -lt 70) { return $P.Yellow }
    if ($h -lt 160) { return $P.Green }
    if ($h -lt 200) { return $P.Cyan }
    if ($h -lt 260) { return $P.Purple }
    return $P.Pink
}

function MapColor([string]$attr, [string]$v) {
    if ($v -eq 'Transparent' -or $v -like '{*') { return $v }
    $c = Parse $v
    if ($null -eq $c -or $c.A -eq 0) { return $v }
    $l = $c.GetBrightness(); $s = $c.GetSaturation()
    $vivid = $s -gt 0.35 -and $l -gt 0.2 -and $l -lt 0.9
    switch -regex ($attr) {
        '^foreColor' {
            if ($vivid) { return (Accent $c) }
            if ($l -lt 0.6) { return $P.Foreground }
            return $P.Comment
        }
        '^borderColor' {
            if ($vivid) { return $P.Purple }
            if ($l -ge 0.6) { return $P.Darkest }
            return $P.CurrentLine
        }
        default {
            # Fundos: amarelos/azuis de destaque (hot-track, seleção) viram a linha atual; cinzas claros viram faixas escuras.
            if ($vivid -or $s -gt 0.5) { return $P.CurrentLine }
            if ($l -ge 0.97) { return $P.Background }
            if ($l -ge 0.85) { return $P.Darker }
            if ($l -ge 0.6) { return $P.Mid }
            if ($l -ge 0.3) { return $P.CurrentLine }
            return $P.Darkest
        }
    }
}

$src = Join-Path $GeneXusPath 'GenexusOffice2010Silver.isl'
[xml]$x = Get-Content -LiteralPath $src -Raw -Encoding UTF8

$x.styleLibrary.SetAttribute('office2007ColorScheme', 'Black')
$x.styleLibrary.SetAttribute('office2010ColorScheme', 'Black')
foreach ($set in $x.styleLibrary.styleSets.styleSet) {
    # A IDE grava cores explícitas nos controles (grid da Select Objects em White, CellAppearance por linha):
    # com ApplicationThenControl o estilo da aplicação vence essas cores.
    $set.SetAttribute('resolutionOrder', 'ApplicationThenControl')
    $set.SetAttribute('useOsThemes', 'False')
}

$n = 0
foreach ($el in $x.SelectNodes('//*[@*]')) {
    foreach ($a in @($el.Attributes)) {
        if ($a.Name -notmatch '^(backColor2?|foreColor|borderColor(2|3DBase)?)$') { continue }
        $nv = MapColor $a.Name $a.Value
        if ($nv -ne $a.Value) { $a.Value = $nv; $n++ }
    }
}
# Áreas de controle sem cor no Silver herdam o Appearance que a IDE grava (Select Objects: grid vazia em White).
# Com cor no estilo + ApplicationThenControl o fundo vazio fica escuro sem tocar no controle.
foreach ($role in 'GridControlArea', 'TreeControlArea', 'ListViewControlArea') {
    $st = $x.SelectSingleNode("//*[local-name()='style'][@role='$role']")
    if ($null -eq $st) { continue }
    $states = $st.SelectSingleNode("*[local-name()='states']")
    if ($null -eq $states) { $states = $st.AppendChild($x.CreateElement('states', $st.NamespaceURI)) }
    $normal = $states.SelectSingleNode("*[local-name()='state'][@name='Normal']")
    if ($null -eq $normal) { $normal = $states.AppendChild($x.CreateElement('state', $st.NamespaceURI)); $normal.SetAttribute('name', 'Normal') }
    if (-not $normal.GetAttribute('backColor')) { $normal.SetAttribute('backColor', $P.Background); $n++ }
    if (-not $normal.GetAttribute('foreColor')) { $normal.SetAttribute('foreColor', $P.Foreground) }
}

# As imagens dos recursos são gradientes claros do Office 2010: sem elas vale o backColor mapeado.
$imgs = @($x.SelectNodes('//*[local-name()="imageBackground"]'))
foreach ($i in $imgs) { [void]$i.ParentNode.RemoveChild($i) }

New-Item -ItemType Directory -Force (Split-Path $Out) | Out-Null
$x.Save($Out)
Write-Output ("{0}: {1} cores trocadas, {2} imagens removidas" -f $Out, $n, $imgs.Count)
