// Pacote de UI do GeneXus 18 que só carrega o tema. A DLL do tema mora em
// %LOCALAPPDATA%\GxDracula\GxDracula.Theme.dll e é recarregada a quente quando muda,
// então ajustar cor não exige reinstalar o pacote nem reiniciar a IDE.
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Artech.Architecture.Common.Packages;
using Artech.Architecture.Common.Services;
using Artech.Architecture.UI.Framework.Packages;

[assembly: AssemblyTitle("GxDracula")]
[assembly: AssemblyDescription("Tema escuro Dracula para a IDE do GeneXus 18")]
[assembly: AssemblyCompany("Univali")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: ComVisible(false)]
[assembly: Guid("3f6c1d2e-8b7a-4c55-9d1e-6a2b0c9e7f41")]
[assembly: PackageCompatibility(Version = 143920)]
[assembly: Package(typeof(GxDracula.Package), IsUIPackage = true, IsCore = false)]

namespace GxDracula
{
    [Guid("b1e0a7c4-2d3f-4e6a-9c8b-5f7e1d2a3b90")]
    public class Package : AbstractPackageUI
    {
        private static readonly string themePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"GxDracula\GxDracula.Theme.dll");
        private Timer watcher;
        private DateTime loadedStamp;
        private Type theme;

        public override string Name { get { return "GxDracula"; } }

        public override void Initialize(IGxServiceProvider services)
        {
            base.Initialize(services);
            try
            {
                Load();
                watcher = new Timer();
                watcher.Interval = 2000;
                watcher.Tick += delegate { if (File.Exists(themePath) && File.GetLastWriteTime(themePath) != loadedStamp) Load(); };
                watcher.Start();
            }
            catch (Exception ex) { Log(ex); }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(themePath)) return;
                if (theme != null) theme.GetMethod("Stop").Invoke(null, null);
                loadedStamp = File.GetLastWriteTime(themePath);
                Assembly a = Assembly.Load(File.ReadAllBytes(themePath));
                theme = a.GetType("GxDracula.Theme");
                theme.GetMethod("Start").Invoke(null, null);
            }
            catch (Exception ex) { Log(ex); }
        }

        private static void Log(Exception ex)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "GxDracula.log"), DateTime.Now.ToString("s") + " Package " + ex + Environment.NewLine); }
            catch { }
        }
    }
}
