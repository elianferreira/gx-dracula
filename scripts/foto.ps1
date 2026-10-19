# Fotografa a janela principal do GeneXus por PrintWindow (funciona sem foco e com a tela bloqueada).
param([string]$Saida, [double]$Escala = 0.6)
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices;
public class PW {
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, int f);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int L, T, R, B; }
}
'@
[PW]::SetProcessDPIAware() | Out-Null
$h = (Get-Process GeneXus | Where-Object MainWindowHandle -ne 0 | Select-Object -First 1).MainWindowHandle
$r = New-Object PW+RECT; [PW]::GetWindowRect($h, [ref]$r) | Out-Null
$bmp = New-Object Drawing.Bitmap ($r.R - $r.L), ($r.B - $r.T)
$g = [Drawing.Graphics]::FromImage($bmp); $dc = $g.GetHdc()
[PW]::PrintWindow($h, $dc, 2) | Out-Null; $g.ReleaseHdc($dc)
(New-Object Drawing.Bitmap $bmp, ([int]($bmp.Width * $Escala)), ([int]($bmp.Height * $Escala))).Save($Saida)
$Saida
