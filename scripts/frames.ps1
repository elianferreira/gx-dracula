# Grava quadros de uma janela da IDE desde o instante em que ela aparece (diagnóstico de flash branco).
# Uso: frames.ps1 -Titulo 'Select Objects' -Saida <pasta> [-Segundos 3]
param([string]$Titulo = 'Select', [string]$Saida = "$env:TEMP\GxDracula.frames", [int]$Segundos = 3, [int]$EsperaSegundos = 120)
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System; using System.Runtime.InteropServices; using System.Text;
public static class FW {
  public delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc f, IntPtr l);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowThreadProcessId(IntPtr h, out int pid);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  public struct RECT { public int L, T, R, B; }
  public static IntPtr Find(int pid, string t) {
    IntPtr found = IntPtr.Zero;
    EnumWindows(delegate(IntPtr h, IntPtr l) {
      int p; GetWindowThreadProcessId(h, out p);
      if (p != pid || !IsWindowVisible(h)) return true;
      var sb = new StringBuilder(256); GetWindowText(h, sb, 256);
      if (sb.ToString().Contains(t)) { found = h; return false; }
      return true; }, IntPtr.Zero);
    return found;
  }
}
'@
[FW]::SetProcessDPIAware() | Out-Null
$gx = (Get-Process GeneXus | Select-Object -First 1).Id
Remove-Item $Saida -Recurse -Force -ErrorAction SilentlyContinue; New-Item -ItemType Directory $Saida | Out-Null
$limite = [DateTime]::Now.AddSeconds($EsperaSegundos)
while ([FW]::Find($gx, $Titulo) -ne [IntPtr]::Zero) { if ([DateTime]::Now -gt $limite) { 'janela nunca fechou'; exit 1 } }  # janela já aberta: espera fechar
while (($h = [FW]::Find($gx, $Titulo)) -eq [IntPtr]::Zero) { if ([DateTime]::Now -gt $limite) { 'janela nao apareceu'; exit 1 } }
$sw = [Diagnostics.Stopwatch]::StartNew(); $i = 0
"apareceu $(Get-Date -Format HH:mm:ss.fff)" | Set-Content "$Saida\inicio.txt"
while ($sw.ElapsedMilliseconds -lt $Segundos * 1000) {
  $r = New-Object FW+RECT; [FW]::GetWindowRect($h, [ref]$r) | Out-Null
  $w = $r.R - $r.L; $hh = $r.B - $r.T; if ($w -le 0) { continue }
  $bmp = New-Object Drawing.Bitmap $w, $hh
  $g = [Drawing.Graphics]::FromImage($bmp); $g.CopyFromScreen($r.L, $r.T, 0, 0, $bmp.Size); $g.Dispose()
  $bmp.Save(('{0}\{1:D3}_{2:D5}ms.png' -f $Saida, $i, $sw.ElapsedMilliseconds)); $bmp.Dispose(); $i++
}
"$i quadros em $Saida"
