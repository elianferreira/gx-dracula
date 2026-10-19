using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace GxDracula
{
    // Estilo Infragistics pelo canal da própria IDE: o GeneXusMain carrega GenexusOffice2010Silver.isl no startup
    // (StyleManager.Load); carregar o GxDracula.isl por cima repinta todo controle Infragistics sem reflection.
    public static partial class Theme
    {
        // %APPDATA%\GxDracula.islonly: só o .isl, sem a varredura de controles (para comparar).
        private static readonly string islOnly = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GxDracula.islonly");
        private static readonly bool islOnlyOn = File.Exists(islOnly);
        private static readonly string islPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"GxDracula\GxDracula.isl");

        // isl <caminho> | isl dracula | isl silver
        private static void LoadIsl(string arg)
        {
            string path = arg == "dracula" ? islPath
                : arg == "silver" ? Path.Combine(Application.StartupPath, "GenexusOffice2010Silver.isl") : arg;
            if (!File.Exists(path)) { Log("isl: arquivo nao existe " + path, null); return; }
            // Usa o carregador da IDE (ajusta tamanhos ao DPI antes do StyleManager.Load); sem ele, Load direto.
            Type gm = FindType("Genexus.Win.GeneXusMain");
            MethodInfo ide = null;
            if (gm != null)
                foreach (MethodInfo m in gm.GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
                {
                    ParameterInfo[] ps = m.GetParameters();
                    if (m.ReturnType != typeof(void) || ps.Length != 1 || ps[0].ParameterType != typeof(string) || m.GetMethodBody() == null) continue;
                    foreach (LocalVariableInfo v in m.GetMethodBody().LocalVariables)
                        if (v.LocalType == typeof(System.Xml.XmlDocument)) ide = m;
                    if (ide != null) break;
                }
            if (ide != null) ide.Invoke(null, new object[] { path });
            else
            {
                Type sm = FindType("Infragistics.Win.AppStyling.StyleManager");
                sm.GetMethod("Load", new[] { typeof(string) }).Invoke(null, new object[] { path });
            }
            foreach (Form f in Application.OpenForms) f.Refresh();
            Log("isl carregado " + path + (ide != null ? " (carregador da IDE " + ide.Name + ")" : " (StyleManager.Load)"), null);
        }
    }
}
