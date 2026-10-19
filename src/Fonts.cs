using System;
using System.Drawing;
using System.Windows.Forms;

namespace GxDracula
{
    // Fonte dos menus/toolbars. Chamado a cada tick do tema.
    public static partial class Theme
    {
        // Troque aqui a família (precisa estar instalada no Windows: o Infragistics desenha via GDI).
        private const string MenuFont = "Inter";

        private static object fontsAppliedTo;   // manager em que a fonte já foi aplicada
        private static int fontsToolbarCount = -1;
        private static bool? menuFontOk;
        private static int fontsRetry;

        [System.Runtime.InteropServices.DllImport("gdi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int AddFontResourceW(string file);

        private static bool MenuFontAvailable()
        {
            using (Font f = new Font(MenuFont, 9f)) return string.Equals(f.Name, MenuFont, StringComparison.OrdinalIgnoreCase);
        }

        private static void ThemeFontsExtra()
        {
            if (toolbars == null) return;
            object list = Get(toolbars, "Toolbars");
            int count = Convert.ToInt32(Get(list, "Count") ?? 0);
            if (ReferenceEquals(toolbars, fontsAppliedTo) && count == fontsToolbarCount) return;

            if (menuFontOk != true)
            {
                // Sem a fonte não mexe em nada. O GDI+ só relê as fontes instaladas ao abrir o processo:
                // instalar com a IDE aberta exige reabrir a IDE. Ainda assim tenta de novo a cada ~14 s.
                if (menuFontOk == false && (++fontsRetry % 20) != 0) return;
                if (menuFontOk == null)
                {
                    // Fonte instalada por usuário com a IDE aberta: carrega na sessão a partir daqui também.
                    string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Windows\Fonts");
                    try
                    {
                        if (System.IO.Directory.Exists(dir))
                            foreach (string f in System.IO.Directory.GetFiles(dir, MenuFont + "-*.ttf")) AddFontResourceW(f);
                    }
                    catch (Exception ex) { Log("AddFontResource", ex); }
                }
                bool ok = MenuFontAvailable();
                if (menuFontOk == null || ok) Log("menu font " + MenuFont + " disponível=" + ok, null);
                menuFontOk = ok;
                if (!ok) return;
            }

            try
            {
                SetFontName(Get(Get(toolbars, "ToolbarSettings"), "Appearance"));
                SetFontName(Get(Get(toolbars, "MenuSettings"), "Appearance"));
                SetFontName(Get(toolbars, "Appearance"));
                if (list is System.Collections.IEnumerable)
                    foreach (object tb in (System.Collections.IEnumerable)list)
                        SetFontName(Get(Get(tb, "Settings"), "Appearance"));

                fontsAppliedTo = toolbars;
                fontsToolbarCount = count;
                Control host = Get(toolbars, "DockWithinContainer") as Control;
                if (host != null) host.Invalidate(true);
                Log("menu font aplicada em " + count + " toolbars", null);
            }
            catch (Exception ex) { Log("ThemeFontsExtra", ex); }
        }

        private static void SetFontName(object appearance)
        {
            object fd = Get(appearance, "FontData");
            if (fd == null) return;
            if (!string.Equals(Get(fd, "Name") as string, MenuFont)) TrySet(fd, "Name", MenuFont);
        }
    }
}
