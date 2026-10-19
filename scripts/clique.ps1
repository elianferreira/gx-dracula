# Clica num ponto da tela em pixels físicos (o GeneXus é DPI-aware).
param([int]$X, [int]$Y)
Add-Type @"
using System; using System.Runtime.InteropServices;
public class CK { [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
 [DllImport("user32.dll")] public static extern void mouse_event(int f, int x, int y, int d, int e); }
"@
[CK]::SetProcessDPIAware() | Out-Null; [CK]::SetCursorPos($X, $Y) | Out-Null
[CK]::mouse_event(2, 0, 0, 0, 0); [CK]::mouse_event(4, 0, 0, 0, 0)
