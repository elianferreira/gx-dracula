// Tema Dracula: tinge a IDE do GeneXus 18 em tempo de execução. Carregado a quente pelo GxDracula.dll.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("GxDracula.Theme")]

namespace GxDracula
{    internal static class Dracula
    {
        public static readonly Color Background = Color.FromArgb(0x28, 0x2A, 0x36);
        public static readonly Color Darker = Color.FromArgb(0x21, 0x22, 0x2C);
        public static readonly Color Darkest = Color.FromArgb(0x19, 0x1A, 0x21);
        public static readonly Color CurrentLine = Color.FromArgb(0x44, 0x47, 0x5A);
        public static readonly Color LineHighlight = Color.FromArgb(0x34, 0x37, 0x46);
        public static readonly Color Foreground = Color.FromArgb(0xF8, 0xF8, 0xF2);
        public static readonly Color Comment = Color.FromArgb(0x62, 0x72, 0xA4);
        public static readonly Color Cyan = Color.FromArgb(0x8B, 0xE9, 0xFD);
        public static readonly Color Green = Color.FromArgb(0x50, 0xFA, 0x7B);
        public static readonly Color Orange = Color.FromArgb(0xFF, 0xB8, 0x6C);
        public static readonly Color Pink = Color.FromArgb(0xFF, 0x79, 0xC6);
        public static readonly Color Purple = Color.FromArgb(0xBD, 0x93, 0xF9);
        public static readonly Color Red = Color.FromArgb(0xFF, 0x55, 0x55);
        public static readonly Color Yellow = Color.FromArgb(0xF1, 0xFA, 0x8C);
        public static readonly Color Border = Color.FromArgb(0x19, 0x1A, 0x21);

        public static bool IsLight(Color c)
        {
            return !c.IsEmpty && c.A > 0 && c.GetBrightness() > 0.55f;
        }

        public static bool IsDark(Color c)
        {
            return !c.IsEmpty && c.A > 0 && c.GetBrightness() < 0.35f;
        }

        // Cor de fundo clara vira o fundo do tema; o resto fica como está.
        public static Color MapBack(Color c, Color target)
        {
            if (c.IsEmpty || c.A == 0) return c;
            if (IsLight(c) || c.IsSystemColor) return target;
            return c;
        }

        // Texto escuro vira o texto do tema; cor saturada vai para a cor Dracula mais próxima.
        public static Color MapFore(Color c)
        {
            if (c.IsEmpty || c.A == 0) return c;
            if (c.IsSystemColor || IsDark(c) && c.GetSaturation() < 0.25f) return Foreground;
            if (c.GetSaturation() < 0.15f) return c.GetBrightness() > 0.75f ? Foreground : Comment;
            return NearestAccent(c);
        }

        public static Color NearestAccent(Color c)
        {
            float h = c.GetHue();
            if (h < 20 || h >= 340) return Red;
            if (h < 45) return Orange;
            if (h < 70) return Yellow;
            if (h < 160) return Green;
            if (h < 200) return Cyan;
            if (h < 260) return Purple;
            return Pink;
        }
    }

    public static partial class Theme
    {
        private static Timer timer;
        private static readonly HashSet<object> seen = new HashSet<object>();
        private static readonly HashSet<object> seenLanguages = new HashSet<object>();
        private static readonly List<WeakReference> editors = new List<WeakReference>();
        private static string logPath = Path.Combine(Path.GetTempPath(), "GxDracula.log");

        public static void Start()
        {
            string off = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GxDracula.disabled");
            if (File.Exists(off)) { Log("desligado por " + off, null); return; }
            Log("Start", null);
            GlobalSettings();
            timer = new Timer();
            timer.Interval = 700;
            timer.Tick += delegate { Tick(); };
            timer.Start();
            running = true;
        }

        // Chamado pelo pacote antes de carregar uma versão nova desta DLL.
        public static void Stop()
        {
            running = false;
            if (timer != null) timer.Stop();
            Log("Stop", null);
        }

        private static bool running, schemeSet;
        private static readonly string dumpTrigger = Path.Combine(Path.GetTempPath(), "GxDracula.dump");

        private static void Tick()
        {
            if (!running) return;
            try
            {
                if (File.Exists(dumpTrigger)) { File.Delete(dumpTrigger); Dump(); }
                if (File.Exists(cmdFile)) { string cmd = File.ReadAllText(cmdFile).Trim(); File.Delete(cmdFile); RunCommand(cmd); }
                if (!schemeSet) { schemeSet = true; RunCommand("scheme Black"); }
                foreach (Form f in Application.OpenForms) Apply(f);
                ThemeBrowsers();
                ThemeCrownTabs();
                ThemeTabsExtra();
                ThemeFontsExtra(); ThemeToolbarExtra(); AttachThumbs();
                if (!File.Exists(iconsOff)) ThemeToolbarIcons();
                foreach (WeakReference w in editors.ToArray())
                {
                    object e = w.Target;
                    if (e == null) { editors.Remove(w); continue; }
                    ThemeEditorLanguage(e);
                    // A IDE troca o renderer depois de criar o editor; reaplica quando não é o nosso.
                    object r = Get(e, "Renderer");
                    if (r == null || !ours.Contains(r)) ThemeSyntaxEditor((Control)e, false);
                }
            }
            catch (Exception ex) { Log("Tick", ex); }
        }

        public static void Log(string what, Exception ex)
        {
            try { File.AppendAllText(logPath, DateTime.Now.ToString("s") + " " + what + (ex == null ? "" : " " + ex) + Environment.NewLine); }
            catch { }
        }

        // ---------- configuração global das bibliotecas ----------

        private static void GlobalSettings()
        {
            // Infragistics: esquema Office2007 preto como base para o que desenha pelo color table.
            Type ct = FindType("Infragistics.Win.Office2007ColorTable");
            Type scheme = FindType("Infragistics.Win.Office2007ColorScheme");
            if (ct != null && scheme != null)
                TrySetStatic(ct, "ColorScheme", Enum.Parse(scheme, "Black"));
            // DevExpress: estilo plano, sem tema do Windows.
            Type ulf = FindType("DevExpress.LookAndFeel.UserLookAndFeel");
            if (ulf != null)
            {
                try
                {
                    object def = ulf.GetProperty("Default", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
                    TrySet(def, "UseWindowsXPTheme", false);
                    Type style = FindType("DevExpress.LookAndFeel.LookAndFeelStyle");
                    if (style != null) TrySet(def, "Style", Enum.Parse(style, "Flat"));
                }
                catch (Exception ex) { Log("DevExpress", ex); }
            }
        }

        private static bool InheritsFrom(object o, string nsPrefix)
        {
            for (Type t = o.GetType(); t != null; t = t.BaseType)
                if ((t.Namespace ?? "").StartsWith(nsPrefix)) return true;
            return false;
        }

        private static Type FindType(string name)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType(name, false);
                if (t != null) return t;
            }
            return null;
        }

        // ---------- controles ----------

        private static void Apply(Control c)
        {
            if (c == null || c.IsDisposed) return;
            if (!seen.Contains(c))
            {
                seen.Add(c);
                c.Disposed += delegate(object s, EventArgs e) { seen.Remove(s); };
                c.ControlAdded += delegate(object s, ControlEventArgs e) { SafeApply(e.Control); };
                try { ThemeControl(c); }
                catch (Exception ex) { Log("ThemeControl " + c.GetType().FullName, ex); }
            }
            Control[] kids = new Control[c.Controls.Count];
            c.Controls.CopyTo(kids, 0);
            foreach (Control k in kids) Apply(k);
        }

        private static void SafeApply(Control c)
        {
            if (!running) return;
            try { Apply(c); } catch (Exception ex) { Log("ControlAdded", ex); }
        }

        // Diagnóstico: crie %TEMP%\GxDracula.dump e a árvore de controles sai em %TEMP%\GxDracula.tree.txt.
        private static void Dump()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (Form f in Application.OpenForms) DumpControl(f, 0, sb);
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "GxDracula.tree.txt"), sb.ToString());
        }

        private static readonly string cmdFile = Path.Combine(Path.GetTempPath(), "GxDracula.cmd");

        // Diagnóstico: "tab <título>" ativa a aba de part com esse título (para fotografar sem mouse).
        private static void RunCommand(string cmd)
        {
            if (cmd.StartsWith("combo ")) { ComboCommand(cmd.Substring(6).Trim()); return; }
            if (cmd.StartsWith("shot "))
            {
                // shot <form|main> <arquivo.png>: foto da janela por dentro do processo (independe de foco/PrintWindow).
                string[] a = cmd.Substring(5).Trim().Split(new[] { ' ' }, 2);
                foreach (Form f in Application.OpenForms)
                {
                    if (!(a[0] == "main" ? f.IsMdiContainer || f == Application.OpenForms[0] : f.Name == a[0] || f.Text == a[0])) continue;
                    using (Bitmap bm = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(bm, new Rectangle(0, 0, f.Width, f.Height)); bm.Save(a[1]); }
                    Log("shot " + f.Name + " -> " + a[1], null);
                    break;
                }
                return;
            }
            if (cmd.StartsWith("node "))
            {
                // node <texto>: seleciona o nó de mesmo texto em qualquer TreeView aberto (categorias do Options).
                string want = cmd.Substring(5).Trim();
                foreach (Form f in Application.OpenForms)
                    foreach (Control c in f.Controls.Find("treeCategories", true))
                    {
                        TreeView tv = c as TreeView;
                        if (tv == null) continue;
                        tv.ExpandAll();
                        TreeNode hit = FindNode(tv.Nodes, want);
                        if (hit != null) { tv.SelectedNode = hit; hit.EnsureVisible(); }
                        Log("node " + want + " -> " + (hit != null), null);
                    }
                return;
            }
            if (cmd.StartsWith("doc "))
            {
                foreach (Form f in Application.OpenForms)
                    foreach (Form m in f.MdiChildren)
                        if (string.Equals(m.Text, cmd.Substring(4), StringComparison.OrdinalIgnoreCase)) { m.Activate(); Log("doc " + m.Text, null); }
                return;
            }
            if (cmd == "tools")
            {
                Log("tools manager=" + (toolbars != null) + " toolbars=" + Get(Get(toolbars, "Toolbars"), "Count") + " tools=" + Get(Get(toolbars, "Tools"), "Count"), null);
                foreach (Form f in Application.OpenForms)
                    foreach (Control d in f.Controls)
                        if (d.GetType().Name == "UltraToolbarsDockArea" && Get(d, "ToolbarsManager") != null) toolbars = Get(d, "ToolbarsManager");
                Log("tools dock toolbars=" + Get(Get(toolbars, "Toolbars"), "Count"), null);
                if (toolbars == null) return;
                foreach (object tb in (IEnumerable)Get(toolbars, "Toolbars"))
                    foreach (object tool in (IEnumerable)Get(tb, "Tools"))
                        Log("tool " + Get(tb, "Key") + " | " + Get(tool, "Key") + " | " + Get(Get(tool, "SharedProps"), "Caption")
                            + " | " + tool.GetType().Name, null);
                return;
            }
            if (cmd == "preview")
            {
                // Força o WorkWithPlus a regerar o preview: troca o Preview Size e volta.
                foreach (Form f in Application.OpenForms)
                    foreach (Control c in f.Controls.Find("cbxSizes", true))
                    {
                        ComboBox cb = c as ComboBox;
                        if (cb == null || !cb.Visible || cb.Items.Count < 2) continue;
                        int i = cb.SelectedIndex;
                        bool en = cb.Enabled; cb.Enabled = true;
                        cb.SelectedIndex = (i + 1) % cb.Items.Count; cb.SelectedIndex = i;
                        cb.Enabled = en;
                        Log("preview regerado via " + cb.Name, null);
                    }
                return;
            }
            if (cmd.StartsWith("click "))
            {
                // click <prefixo da Key>: dispara a ferramenta como se o usuário clicasse (OnToolClick protegido do manager).
                foreach (Form f in Application.OpenForms)
                    foreach (Control d in f.Controls)
                        if (d.GetType().Name == "UltraToolbarsDockArea" && Get(d, "ToolbarsManager") != null) toolbars = Get(d, "ToolbarsManager");
                foreach (object tool in (IEnumerable)Get(toolbars, "Tools"))
                {
                    string key = (Get(tool, "Key") as string) ?? "";
                    if (!key.StartsWith(cmd.Substring(6) + "::") && key != cmd.Substring(6)) continue;
                    Type at = FindType("Infragistics.Win.UltraWinToolbars.ToolClickEventArgs");
                    if (at == null) { Log("click: ToolClickEventArgs nao achado; manager=" + toolbars.GetType().FullName, null); return; }
                    object args = Activator.CreateInstance(at, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { tool, null }, null);
                    MethodInfo mi = toolbars.GetType().GetMethod("OnToolClick", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    Log("click " + key, null);
                    if (mi != null) { Form owner = Application.OpenForms[0]; owner.BeginInvoke((MethodInvoker)delegate { mi.Invoke(toolbars, new[] { args }); }); }
                    return;
                }
                Log("click: ferramenta nao achada " + cmd + " manager=" + (toolbars == null ? "null" : toolbars.GetType().FullName)
                    + " tools=" + Get(Get(toolbars, "Tools"), "Count") + " forms=" + Application.OpenForms.Count, null);
                return;
            }
            if (cmd.StartsWith("scheme "))
            {
                Type ct = FindType("Infragistics.Win.Office2007ColorTable"), cs = FindType("Infragistics.Win.Office2007ColorScheme");
                if (ct != null && cs != null)
                {
                    PropertyInfo p = ct.GetProperty("ColorScheme", BindingFlags.Static | BindingFlags.Public);
                    if (p != null) p.SetValue(null, Enum.Parse(cs, cmd.Substring(7)), null);
                    foreach (Form f in Application.OpenForms) f.Refresh();
                    Log("scheme " + (p == null ? "sem propriedade" : p.GetValue(null, null)), null);
                }
                else Log("scheme: tipos nao achados", null);
                return;
            }
            if (cmd.StartsWith("tbstyle "))
            {
                TrySetEnumByName(toolbars, "Style", cmd.Substring(8));
                Log("tbstyle " + Get(toolbars, "Style"), null);
                return;
            }
            if (cmd == "alltools")
            {
                foreach (object tool in (IEnumerable)Get(toolbars, "Tools")) Log("tool* " + Get(tool, "Key"), null);
                return;
            }
            if (cmd.StartsWith("theme "))
            {
                // theme <controle> <tema>: troca o tema nativo de um controle para comparar scrollbars.
                string[] a = cmd.Split(' ');
                foreach (Form f in Application.OpenForms)
                    foreach (Control c in f.Controls.Find(a[1], true))
                    {
                        SetWindowTheme(c.Handle, a[2] == "-" ? "" : a[2], null);
                        RedrawFrame(c); Log("theme " + c.Name + " " + a[2], null);
                    }
                return;
            }
            if (cmd.StartsWith("probe "))
            {
                foreach (Form f in Application.OpenForms)
                    foreach (Control c in f.Controls.Find(cmd.Substring(6), true))
                    {
                        object r = Get(c, "Renderer");
                        Log("probe " + c.Name + " type=" + c.GetType().FullName + " visible=" + c.Visible + " enabled=" + c.Enabled
                            + " renderer=" + (r == null ? "null" : r.GetType().FullName) + " nosso=" + (r != null && ours.Contains(r))
                            + " fill=" + Get(r, "TextAreaBackgroundFill") + " disFill=" + Get(r, "TextAreaDisabledBackgroundFill")
                            + " readOnly=" + Get(Get(c, "Document"), "ReadOnly") + " parent=" + (c.Parent == null ? "" : c.Parent.Name)
                            + " border=" + Get(c, "BorderStyle") + " borderColor=" + Get(c, "BorderColor") + " parentBorder=" + Get(c.Parent, "BorderStyle")
                            + " parentType=" + (c.Parent == null ? "" : c.Parent.GetType().FullName) + " pad=" + (c.Parent == null ? "" : c.Parent.Padding.ToString()), null);
                    }
                return;
            }
            if (!cmd.StartsWith("tab ")) return;
            string title = cmd.Substring(4);
            foreach (Form f in Application.OpenForms) SelectTab(f, title);
        }

        private static void SelectTab(Control c, string title)
        {
            if (c.GetType().FullName == "Crownwood.Magic.Controls.TabControl")
            {
                IList pages = Get(c, "TabPages") as IList;
                if (pages != null)
                    for (int i = 0; i < pages.Count; i++)
                        if (string.Equals(Get(pages[i], "Title") as string, title, StringComparison.OrdinalIgnoreCase))
                        { TrySet(c, "SelectedIndex", i); Log("tab " + title + " -> " + i, null); }
            }
            foreach (Control k in c.Controls) SelectTab(k, title);
        }

        private static string CrownwoodFields(Control c)
        {
            string r = "";
            foreach (FieldInfo f in c.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (f.FieldType == typeof(Color)) r += " " + f.Name + "=" + ColorTranslator.ToHtml((Color)f.GetValue(c));
                else if (f.FieldType.IsEnum) r += " " + f.Name + "=" + f.GetValue(c);
            return r;
        }

        private static void DumpControl(Control c, int depth, System.Text.StringBuilder sb)
        {
            if (!c.Visible && depth > 0) return;
            Rectangle r = c.RectangleToScreen(c.ClientRectangle);
            sb.Append(new string(' ', depth * 2)).Append(c.GetType().FullName).Append(" '").Append(c.Name).Append("' ")
              .Append(r.X).Append(',').Append(r.Y).Append(' ').Append(r.Width).Append('x').Append(r.Height)
              .Append(" bg=").Append(ColorTranslator.ToHtml(c.BackColor)).Append(" fg=").Append(ColorTranslator.ToHtml(c.ForeColor))
              .Append(c.GetType().FullName == "Crownwood.Magic.Controls.TabControl" ? CrownwoodFields(c) : "")
              .Append(" text='").Append((c.Text ?? "").Replace("\n", " ").Substring(0, Math.Min(40, (c.Text ?? "").Length))).Append("'\n");
            foreach (Control k in c.Controls) DumpControl(k, depth + 1, sb);
        }

        private static void ThemeControl(Control c)
        {
            if (c is WebBrowser) return;
            string ns = c.GetType().Namespace ?? "";
            // Fonte da UI: só troca as famílias antigas, no mesmo tamanho, para não mexer no layout.
            string fam = c.Font.FontFamily.Name;
            if (fam == "Tahoma" || fam == "Microsoft Sans Serif") c.Font = new Font(UiFont, c.Font.Size, c.Font.Style);

            if (c is Form)
            {
                c.BackColor = Dracula.Darker;
                c.ForeColor = Dracula.Foreground;
                DarkTitleBar(c);
                ThemeComponentsOf(c);
            }
            else if (c is MdiClient) c.BackColor = Dracula.Darkest;
            else if (c is TextBoxBase || c is ListBox || c is ComboBox || c is TreeView || c is ListView || c is NumericUpDown)
            {
                c.BackColor = Dracula.Background;
                c.ForeColor = Dracula.Foreground;
                if (c is TextBoxBase) ((TextBoxBase)c).BorderStyle = BorderStyle.FixedSingle;
                if (c is TreeView) { ((TreeView)c).LineColor = Dracula.Comment; DarkTreeSelection((TreeView)c); }
                // Caixa de busca de toolstrip (Options): a moldura FixedSingle sai clara.
                if (c is TextBoxBase && c.Parent is ToolStrip) ((TextBoxBase)c).BorderStyle = BorderStyle.None;
                if (c is ComboBox) ((ComboBox)c).FlatStyle = FlatStyle.Flat;
            }
            else if (c is ToolStrip)
            {
                ToolStrip ts = (ToolStrip)c;
                ts.Renderer = new DraculaRenderer();
                ts.BackColor = Dracula.Darker;
                ts.ForeColor = Dracula.Foreground;
                foreach (ToolStripItem i in ts.Items) ThemeToolStripItem(i);
                ts.ItemAdded += delegate(object s, ToolStripItemEventArgs e) { ThemeToolStripItem(e.Item); };
            }
            else if (c is DataGridView) ThemeGrid((DataGridView)c);
            else if (c is PropertyGrid) ThemePropertyGrid((PropertyGrid)c);
            else if (c is ButtonBase)
            {
                ButtonBase b = (ButtonBase)c;
                if (b is Button)
                {
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = Dracula.Comment;
                    b.BackColor = Dracula.CurrentLine;
                }
                else b.BackColor = Dracula.MapBack(b.BackColor, Dracula.Darker);
                b.ForeColor = Dracula.Foreground;
            }
            else if (InheritsFrom(c, "ActiproSoftware.SyntaxEditor")) ThemeSyntaxEditor(c);
            else if (c.GetType().FullName == "Crownwood.Magic.Controls.TabControl")
            {
                c.BackColor = Dracula.Darker;
                c.ForeColor = Dracula.Foreground;
                TrySet(c, "TextColor", Dracula.Foreground);
                TrySet(c, "TextInactiveColor", Dracula.Comment);
                TrySet(c, "HotTextColor", Dracula.Pink);
                TrySet(c, "ButtonActiveColor", Dracula.Foreground);
                TrySet(c, "ButtonInactiveColor", Dracula.Comment);
                // As cores derivadas do BackColor ficam em campos privados; a aba ativa usa a mais clara.
                SetField(c, "_backIDE", Dracula.Darker);
                SetField(c, "_backLight", Dracula.Background);
                SetField(c, "_backLightLight", Dracula.Background);
                SetField(c, "_backDark", Dracula.Darkest);
                SetField(c, "_backDarkDark", Dracula.Darkest);
                crownTabs.Add(new WeakReference(c));
                c.Invalidate();
            }
            else if (c is TabControl) ThemeWinTabs((TabControl)c);
            else if (c is TabPage) { ((TabPage)c).UseVisualStyleBackColor = false; c.BackColor = Dracula.Background; c.ForeColor = Dracula.Foreground; }
            else if (InheritsFrom(c, "VisualHint.SmartPropertyGrid")) ThemeSmartPropertyGrid(c);
            else if (c.GetType().FullName == "CefSharp.WinForms.ChromiumWebBrowser") { browsers.Add(new WeakReference(c)); Log("browser registrado", null); }
            else
            {
                c.BackColor = Dracula.MapBack(c.BackColor, c is TabPage || c is UserControl ? Dracula.Background : Dracula.Darker);
                c.ForeColor = c is Label && !Dracula.IsDark(c.ForeColor) && c.ForeColor.GetSaturation() > 0.3f
                    ? Dracula.MapFore(c.ForeColor) : Dracula.Foreground;
            }

            // Componentes (UltraToolbarsManager etc.) ficam em campos de qualquer controle da IDE, não só do Form.
            if (!(c is Form) && !ns.StartsWith("System") && !ns.StartsWith("Infragistics")) ThemeComponentsOf(c);

            if (c is StatusBar) ThemeStatusBar((StatusBar)c);
            if (InheritsFrom(c, "DevExpress.XtraTreeList")) { ThemeTreeList(c); HookTreeListDraw(c); }
            if (InheritsFrom(c, "Infragistics.Win.UltraWinGrid")) ThemeUltraGrid(c);
            if (InheritsFrom(c, "Infragistics.Win.UltraWinEditors"))
            {
                // Botão de dropdown dos editores: sem isso fica cinza claro do Office2007.
                IgApp(Get(c, "ButtonAppearance"), Dracula.Background, Dracula.Foreground);
                IgApp(Get(c, "Appearance"), Dracula.Background, Dracula.Foreground);
                TrySetEnumByName(c, "ButtonStyle", "Flat");
                TrySetEnumByName(c, "DisplayStyle", "Office2007");
                TrySetEnumByName(c, "BorderStyle", "Solid");
                SetDrawFilter(c);
                c.Invalidate();
            }
            if (c is GroupBox)
                c.Paint += delegate(object o, PaintEventArgs e)
                {
                    // Moldura do GroupBox sai em SystemColors; redesenha por cima, deixando o título.
                    GroupBox gb = (GroupBox)o;
                    int y = gb.Font.Height / 2;
                    Size t = TextRenderer.MeasureText(gb.Text, gb.Font);
                    // Apaga a moldura do sistema e a faixa do título inteira (a borda de 2 px cortava o texto ao meio).
                    using (Pen bg = new Pen(gb.BackColor, 2)) e.Graphics.DrawRectangle(bg, 1, y + 1, gb.Width - 2, gb.Height - y - 2);
                    using (SolidBrush bb = new SolidBrush(gb.BackColor)) e.Graphics.FillRectangle(bb, 0, 0, gb.Width, gb.Font.Height + 2);
                    using (Pen p = new Pen(Dracula.CurrentLine))
                    {
                        e.Graphics.DrawLine(p, 0, y, 6, y);
                        e.Graphics.DrawLine(p, 8 + t.Width, y, gb.Width - 1, y);
                        e.Graphics.DrawLine(p, 0, y, 0, gb.Height - 1);
                        e.Graphics.DrawLine(p, gb.Width - 1, y, gb.Width - 1, gb.Height - 1);
                        e.Graphics.DrawLine(p, 0, gb.Height - 1, gb.Width - 1, gb.Height - 1);
                    }
                    if (gb.Text.Length > 0)
                        TextRenderer.DrawText(e.Graphics, gb.Text, gb.Font, new Point(7, 0), Dracula.Purple, TextFormatFlags.NoPrefix);
                };

            // GxUltraTree (Document Outliner) é Artech por fora, Infragistics por dentro.
            if (InheritsFrom(c, "Infragistics") || InheritsFrom(c, "DevExpress"))
            {
                object tm = Get(c, "ToolbarsManager");
                if (tm != null && !seen.Contains(tm)) { seen.Add(tm); ThemeComponent(tm); }
                TrySetEnumByName(c, "UseOsThemes", "False");
                TrySet(c, "UseAppStyling", false);
                TrySetEnumByName(c, "UseFlatMode", "True");
                Walk(c, 0, "");
            }

            if (c is TreeView || c is ListBox || c is TextBoxBase || c is ScrollableControl && !(c is Form))
                NativeDark(c, "DarkMode_Explorer");
            if (c is TreeView) new ThumbPainter(c);
            if (c is ListView) NativeDark(c, "DarkMode_ItemsView");
            if (c is ComboBox) { ((ComboBox)c).FlatStyle = FlatStyle.Flat; NativeDark(c, "DarkMode_CFD"); new BorderPainter(c); }
            // Bordas 3D/simples dos containers são pintadas com SystemColors (contorno branco no dark).
            if (!(c is TextBoxBase) && !(c is ComboBox) && !(c is Form))
            {
                object bs = Get(c, "BorderStyle");
                if (bs is BorderStyle && (BorderStyle)bs != BorderStyle.None) TrySet(c, "BorderStyle", BorderStyle.None);
            }
        }

        private static void ThemeToolStripItem(ToolStripItem i)
        {
            i.ForeColor = Dracula.Foreground;
            ToolStripDropDownItem d = i as ToolStripDropDownItem;
            if (d != null)
            {
                d.DropDown.Renderer = new DraculaRenderer();
                d.DropDown.BackColor = Dracula.Darker;
                foreach (ToolStripItem k in d.DropDownItems) ThemeToolStripItem(k);
            }
        }

        private static void ThemeGrid(DataGridView g)
        {
            g.EnableHeadersVisualStyles = false;
            g.BackgroundColor = Dracula.Background;
            g.GridColor = Dracula.CurrentLine;
            g.DefaultCellStyle.BackColor = Dracula.Background;
            g.DefaultCellStyle.ForeColor = Dracula.Foreground;
            g.DefaultCellStyle.SelectionBackColor = Dracula.CurrentLine;
            g.DefaultCellStyle.SelectionForeColor = Dracula.Foreground;
            g.AlternatingRowsDefaultCellStyle.BackColor = Dracula.Darker;
            g.ColumnHeadersDefaultCellStyle.BackColor = Dracula.Darkest;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Dracula.Purple;
            g.RowHeadersDefaultCellStyle.BackColor = Dracula.Darkest;
            g.RowHeadersDefaultCellStyle.ForeColor = Dracula.Foreground;
        }

        private static readonly List<WeakReference> crownTabs = new List<WeakReference>();

        // A aba ativa do Crownwood (MultiBox) é pintada com TabPage.ColorTab, branco por padrão.
        private static void ThemeCrownTabs()
        {
            foreach (WeakReference w in crownTabs.ToArray())
            {
                Control c = w.Target as Control;
                if (c == null || c.IsDisposed) { crownTabs.Remove(w); continue; }
                IEnumerable pages = Get(c, "TabPages") as IEnumerable;
                if (pages == null) continue;
                bool changed = false;
                foreach (object p in pages)
                {
                    object ct = Get(p, "ColorTab");
                    if (ct is Color && (Color)ct != Dracula.Darker) { TrySet(p, "ColorTab", Dracula.Darker); changed = true; }
                }
                if (changed) c.Invalidate();
            }
        }

        private static void ThemeSmartPropertyGrid(Control p)
        {
            p.BackColor = Dracula.Background;
            p.ForeColor = Dracula.Foreground;
            TrySet(p, "GridBackColor", Dracula.Background);
            TrySet(p, "GridForeColor", Dracula.Foreground);
            TrySet(p, "GridColor", Dracula.Darker);
            TrySet(p, "GridDisabledForeColor", Dracula.Comment);
            TrySet(p, "DisabledForeColor", Dracula.Comment);
            TrySet(p, "GridHighlightColor", Dracula.CurrentLine);
            TrySet(p, "HighlightColor", Dracula.CurrentLine);
            TrySet(p, "GridSelectedHighlightColor", Dracula.CurrentLine);
            TrySet(p, "SelectedHighlightColor", Dracula.CurrentLine);
            TrySet(p, "SelectedBkgColor", Dracula.CurrentLine);
            TrySet(p, "HighlightedTextColor", Dracula.Foreground);
            TrySet(p, "CommentsBackColor", Dracula.Darker);
            TrySet(p, "CommentsForeColor", Dracula.Foreground);
            TrySet(p, "RootCategoryTextColor", Dracula.Purple);
            TrySet(p, "SubCategoryTextColor", Dracula.Cyan);
            // Faixa das categorias: o GxDrawManager (LightColorDrawManager) pinta com cores próprias, brancas por padrão.
            object dm = Get(p, "DrawManager");
            if (dm != null)
                foreach (string m in new[] { "SetCategoryBackgroundColors", "SetSubCategoryBackgroundColors" })
                {
                    MethodInfo mi = dm.GetType().GetMethod(m, new[] { typeof(Color), typeof(Color) });
                    if (mi != null) mi.Invoke(dm, new object[] { Dracula.Darker, Dracula.Darker });
                }
            p.Invalidate(true);
            Log("smartgrid " + p.GetType().FullName + " bg=" + Get(p, "GridBackColor"), null);
        }

        // ---------- Monaco dentro do CefSharp (Output e editores web) ----------

        private static readonly List<WeakReference> browsers = new List<WeakReference>();
        private static int browserTick;

        private const string MonacoScript =
            "(function(){if(!window.monaco||!monaco.editor)return;" +
            "if(!window.__gxDracula){monaco.editor.defineTheme('gx-dracula',{base:'vs-dark',inherit:true,rules:[" +
            "{token:'',foreground:'F8F8F2'},{token:'comment',foreground:'6272A4'},{token:'string',foreground:'F1FA8C'}," +
            "{token:'number',foreground:'BD93F9'},{token:'keyword',foreground:'FF79C6'},{token:'operator',foreground:'FF79C6'}," +
            "{token:'variable',foreground:'FFB86C'},{token:'type',foreground:'8BE9FD'},{token:'function',foreground:'50FA7B'}," +
            "{token:'tag',foreground:'FF79C6'},{token:'attribute.name',foreground:'50FA7B'},{token:'attribute.value',foreground:'F1FA8C'}," +
            "{token:'error',foreground:'FF5555'},{token:'warning',foreground:'FFB86C'},{token:'info',foreground:'8BE9FD'}]," +
            "colors:{'editor.background':'#282A36','editor.foreground':'#F8F8F2','editor.lineHighlightBackground':'#44475A75'," +
            "'editor.selectionBackground':'#44475A','editorCursor.foreground':'#F8F8F0','editorLineNumber.foreground':'#6272A4'," +
            "'editorLineNumber.activeForeground':'#F8F8F2','editorIndentGuide.background':'#424450','editorWidget.background':'#21222C'," +
            "'scrollbarSlider.background':'#44475A80','scrollbarSlider.hoverBackground':'#6272A480'}});window.__gxDracula=1;}" +
            "monaco.editor.setTheme('gx-dracula');document.body.style.background='#282A36';})();";

        // Start Page (start2.genexus.com): tema escuro e faixa "Bem-vindo" com o logo em %LOCALAPPDATA%\GxDracula\logo.png|svg.
        private static string startScript;
        private static string StartScript()
        {
            if (startScript != null) return startScript;
            string logo = "";
            string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GxDracula");
            foreach (string ext in new[] { "svg", "png" })
            {
                string f = System.IO.Path.Combine(dir, "logo." + ext);
                if (!System.IO.File.Exists(f)) continue;
                logo = "<img src=\"data:image/" + (ext == "svg" ? "svg+xml" : "png") + ";base64," + Convert.ToBase64String(System.IO.File.ReadAllBytes(f)) + "\" style=\"height:88px;margin-right:28px\">";
                break;
            }
            string css = "html,body{background:#282A36!important}body *{background-color:transparent!important;background-image:none!important;box-shadow:none}html,body,div,section,header,nav,table,td,th,tr,p,span,li,strong,b,label{background-color:#282A36!important;color:#F8F8F2!important}" +
                // Bordas só na tabela; molduras do site (faixa do menu, caixas) somem.
                "body *{border-color:transparent!important}table,td,th{border-color:#44475A!important}" +
                "a{color:#FF79C6!important}a:hover{color:#BD93F9!important}h1,h2,h3,h4,th{color:#BD93F9!important}" +
                "img,svg{filter:none}#gxdr-welcome{display:flex;align-items:center;margin:28px 134px 8px;padding:28px 36px;border-radius:14px;" +
                "background:linear-gradient(120deg,#44475A 0%,#282A36 70%)!important;border:1px solid #6272A4!important;box-shadow:0 8px 28px rgba(0,0,0,.35)}" +
                "#gxdr-welcome *{background:transparent!important}" +
                "#gxdr-welcome .sep{width:1px;height:72px;background:#6272A4!important;margin-right:28px}" +
                "#gxdr-welcome h1{font:600 32px 'Inter','Segoe UI',sans-serif;margin:0;color:#F8F8F2!important}" +
                "#gxdr-welcome h1 span{color:#BD93F9!important}" +
                "#gxdr-welcome p{margin:8px 0 0;color:#8BE9FD!important;font:15px 'Inter','Segoe UI',sans-serif;letter-spacing:.3px}" +
                "table{border-collapse:separate!important;border-spacing:0;border-radius:10px;overflow:hidden}" +
                "tr:hover td{background:#44475A!important}" +
                // O fundo vai só na célula: o conteúdo dela fica transparente (senão vira uma caixa escura no hover).
                "td *,th *{background-color:transparent!important}" +
                // Hover dos menus: o site troca fundo/borda no :hover; fica um realce Dracula estável.
                "nav a,nav li,ul li,ul li a{transition:none!important;border-color:transparent!important}" +
                "nav a:hover,ul li a:hover{background-color:#44475A!important;color:#BD93F9!important;border-radius:6px}" +
                "ul li:hover{background-color:transparent!important}th{background:#21222C!important}";
            string html = logo + (logo.Length > 0 ? "<div class=\"sep\"></div>" : "") +
                "<div><h1>Bem-vindo ao <span>GeneXus</span></h1><p>Univali · Sistemas — bom trabalho!</p></div>";
            startScript = "(function(){if(location.host.indexOf('genexus.com')<0||!document.body)return;" +
                "var s=document.getElementById('gxdr-css');if(!s){s=document.createElement('style');s.id='gxdr-css';document.head.appendChild(s);}s.textContent=" + JsStr(css) + ";" +
                "var o=document.getElementById('gxdr-welcome');if(o&&o.getAttribute('data-v')!='" + html.GetHashCode() + "'){o.parentNode.removeChild(o);o=null;}" +
                "if(!o){var d=document.createElement('div');d.id='gxdr-welcome';d.setAttribute('data-v','" + html.GetHashCode() + "');d.innerHTML=" + JsStr(html) + ";" +
                "var n=document.querySelector('nav,header,ul');var at=n?n.parentNode:document.body;at.insertBefore(d,n?n.nextSibling:at.firstChild);}" +
                "Array.prototype.forEach.call(document.querySelectorAll('h2,h3,div'),function(e){if(/^\\s*TOP NEWS\\s*$/i.test(e.textContent)){var p=e.parentNode;if(p&&p.textContent.replace(/\\s/g,'').length<20)p.style.display='none';else e.style.display='none';var x=p&&p.nextElementSibling;if(x&&!x.textContent.trim())x.style.display='none';}});" +
                // Fundo branco que o CSS não vence (estilo inline com !important): força inline no html, body e em quem for claro.
                "var dg=document.getElementById('gxdr-diag');if(dg)dg.parentNode.removeChild(dg);" +
                "[document.documentElement,document.body].concat(Array.prototype.slice.call(document.querySelectorAll('body *'))).forEach(function(e){if(e.id&&e.id.indexOf('gxdr')==0||e.closest&&e.closest('#gxdr-welcome')||e.matches&&e.matches(':hover'))return;" +
                "var m=/rgba?\\((\\d+), (\\d+), (\\d+)(, ([\\d.]+))?\\)/.exec(getComputedStyle(e).backgroundColor);" +
                "if(e===document.documentElement||e===document.body||m&&(+m[1]+ +m[2]+ +m[3])>600&&(!m[5]||+m[5]>0))e.style.setProperty('background','#282A36','important');});" +
                // Caixa de notícias vazia: bloco sem texto nem imagem.
                "Array.prototype.forEach.call(document.querySelectorAll('div,section'),function(e){if(e.id.indexOf('gxdr')==0||e.textContent.trim()||e.querySelector('img,svg,iframe'))return;var h=e.offsetHeight;if(h>20&&h<200)e.style.display='none';});})();";
            return startScript;
        }

        private static string JsStr(string s) { return "'" + s.Replace("\\", "\\\\").Replace("'", "\\'") + "'"; }

        private static void ThemeBrowsers()
        {
            if (++browserTick % 3 != 0) return;
            Type ext = FindType("CefSharp.WebBrowserExtensions");
            if (ext == null) return;
            MethodInfo exec = null;
            foreach (MethodInfo m in ext.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] ps = m.GetParameters();
                if (m.Name == "ExecuteScriptAsync" && ps.Length == 2 && (ps[0].ParameterType.Name == "IWebBrowser" || ps[0].ParameterType.Name == "IChromiumWebBrowserBase") && ps[1].ParameterType == typeof(string))
                { exec = m; break; }
            }
            if (exec == null) return;
            foreach (WeakReference w in browsers.ToArray())
            {
                Control b = w.Target as Control;
                if (b == null || b.IsDisposed) { browsers.Remove(w); continue; }
                object init = Get(b, "IsBrowserInitialized"); if (!probed.Contains(b)) Log("browser init=" + init, null); if (init is bool && !(bool)init) continue;
                if (!probed.Contains(b)) { probed.Add(b); Probe(ext, b); }
                try { exec.Invoke(null, new object[] { b, MonacoScript }); exec.Invoke(null, new object[] { b, StartScript() }); }
                catch (Exception ex) { Log("Monaco", ex); browsers.Remove(w); }
            }
        }

        private static readonly HashSet<object> probed = new HashSet<object>();

        // Diagnóstico: registra no log o que a página tem (monaco? iframes? url).
        private static void Probe(Type ext, Control b)
        {
            try
            {
                MethodInfo eval = null;
                foreach (MethodInfo m in ext.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    ParameterInfo[] ps = m.GetParameters();
                    if (m.Name == "EvaluateScriptAsync" && ps.Length >= 2 && (ps[0].ParameterType.Name == "IWebBrowser" || ps[0].ParameterType.Name == "IChromiumWebBrowserBase") && ps[1].ParameterType == typeof(string))
                    { eval = m; break; }
                }
                if (eval == null) { Log("Probe: sem EvaluateScriptAsync", null); return; }
                object[] args = new object[eval.GetParameters().Length];
                args[0] = b;
                args[1] = "location.href+' | monaco='+(typeof monaco)+' | iframes='+document.querySelectorAll('iframe').length+' | require='+(typeof require)";
                for (int i = 2; i < args.Length; i++) args[i] = Type.Missing;
                object task = eval.Invoke(null, args);
                System.Threading.ThreadPool.QueueUserWorkItem(delegate
                {
                    try
                    {
                        object resp = task.GetType().GetProperty("Result").GetValue(task, null);
                        Log("Probe " + Get(resp, "Success") + " " + Get(resp, "Result") + " " + Get(resp, "Message"), null);
                    }
                    catch (Exception ex) { Log("Probe result", ex); }
                });
            }
            catch (Exception ex) { Log("Probe", ex); }
        }

        private static void ThemePropertyGrid(PropertyGrid p)
        {
            p.BackColor = Dracula.Darker;
            p.ViewBackColor = Dracula.Background;
            p.ViewForeColor = Dracula.Foreground;
            p.LineColor = Dracula.Darker;
            p.CategoryForeColor = Dracula.Purple;
            p.HelpBackColor = Dracula.Darker;
            p.HelpForeColor = Dracula.Foreground;
            p.CommandsBackColor = Dracula.Darker;
            p.CommandsForeColor = Dracula.Foreground;
            TrySet(p, "CategorySplitterColor", Dracula.Darker);
            TrySet(p, "SelectedItemWithFocusBackColor", Dracula.CurrentLine);
            TrySet(p, "SelectedItemWithFocusForeColor", Dracula.Foreground);
            TrySet(p, "DisabledItemForeColor", Dracula.Comment);
            TrySet(p, "ViewBorderColor", Dracula.Darkest);
            TrySet(p, "HelpBorderColor", Dracula.Darkest);
        }

        // Componentes Infragistics (toolbars, docking, abas MDI) não são controles: vivem em campos do form.
        private static void ThemeComponentsOf(Control owner)
        {
            for (Type t = owner.GetType(); t != null && !(t.Namespace ?? "").StartsWith("System"); t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
                {
                    string ns = f.FieldType.Namespace ?? "";
                    if (!(ns.StartsWith("Infragistics") || ns.StartsWith("DevExpress"))) continue;
                    if (typeof(Control).IsAssignableFrom(f.FieldType)) continue;
                    if (!typeof(System.ComponentModel.IComponent).IsAssignableFrom(f.FieldType)) continue;
                    object v;
                    try { v = f.GetValue(owner); } catch { continue; }
                    if (v == null || seen.Contains(v)) continue;
                    seen.Add(v);
                    ThemeComponent(v);
                }
            }
        }

        private static void ThemeComponent(object v)
        {
            TrySetEnumByName(v, "UseOsThemes", "False");
            TrySet(v, "UseAppStyling", false);
            TrySetEnumByName(v, "Style", "Office2007");
            Walk(v, 0, "");
            // A tela real fica com a pintura antiga da toolbar; o PrintWindow repinta e esconde isso.
            // Há mais de um UltraToolbarsManager; o da janela principal é o que tem toolbars.
            if (v.GetType().FullName == "Infragistics.Win.UltraWinToolbars.UltraToolbarsManager"
                && Convert.ToInt32(Get(Get(v, "Toolbars"), "Count") ?? 0) > 0) toolbars = v;
            for (Type bt = v.GetType(); bt != null; bt = bt.BaseType)
                if (bt.FullName == "Infragistics.Win.UltraWinDock.UltraDockManager") { ThemeDockManager(v); break; }
            Control host = Get(v, "DockWithinContainer") as Control;
            if (host != null) host.Invalidate(true);
            Log("componente " + v.GetType().FullName + (host == null ? "" : " repintado"), null);
        }

        // ---------- aparências Infragistics/DevExpress por reflection ----------

        private static readonly string[] skipProps = { "Parent", "Owner", "Container", "Site", "Control", "Form",
            "TopLevelControl", "DockWithinContainer", "MdiParent", "ParentForm", "Manager", "ToolbarsManager",
            "Layout", "DisplayLayout", "Grid", "Tag", "Image", "Images", "ImageList", "Font", "Cursor", "Nodes" };

        private static void Walk(object o, int depth, string path)
        {
            if (o == null || depth > 4) return;
            Type t = o.GetType();
            if (IsAppearance(t)) { ThemeAppearance(o, path); return; }

            foreach (PropertyInfo p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                Type pt = p.PropertyType;
                if (pt.IsValueType || pt == typeof(string)) continue;
                if (Array.IndexOf(skipProps, p.Name) >= 0) continue;
                string pns = pt.Namespace ?? "";
                bool lib = pns.StartsWith("Infragistics") || pns.StartsWith("DevExpress");
                if (!lib || typeof(Control).IsAssignableFrom(pt)) continue;
                if (!IsAppearance(pt) && depth >= 3) continue;
                object v;
                try { v = p.GetValue(o, null); } catch { continue; }
                if (v == null) continue;
                string sub = path + "." + p.Name;
                if (IsAppearance(v.GetType())) { ThemeAppearance(v, sub); continue; }
                if (seen.Contains(v)) continue;
                seen.Add(v);
                IEnumerable list = v as IEnumerable;
                if (list != null)
                {
                    int n = 0;
                    foreach (object item in list)
                    {
                        if (item == null || item is Control) continue;
                        if (++n > 300) break;
                        Walk(item, depth + 1, sub);
                    }
                }
                Walk(v, depth + 1, sub);
            }
        }

        private static bool IsAppearance(Type t)
        {
            for (; t != null; t = t.BaseType)
                if (t.FullName == "Infragistics.Win.AppearanceBase" || t.FullName == "DevExpress.Utils.AppearanceObject")
                    return true;
            return false;
        }

        private static void ThemeAppearance(object a, string path)
        {
            string p = path.ToLowerInvariant();
            bool selected = p.Contains("selected") || p.Contains("active") || p.Contains("hot") || p.Contains("pressed") || p.Contains("highlight") || p.Contains("focus");
            bool header = p.Contains("header") || p.Contains("caption") || p.Contains("tab");
            Color back = selected ? Dracula.CurrentLine : header ? Dracula.Darker : Dracula.Background;
            TrySet(a, "BackColor", back);
            TrySet(a, "BackColor2", back);
            TrySet(a, "BackColorDisabled", Dracula.Darker);
            TrySetEnumByName(a, "BackGradientStyle", "None");
            TrySet(a, "BorderColor", Dracula.Darkest);
            TrySet(a, "BorderColor3DBase", Dracula.Darkest);
            TrySet(a, "ForeColor", selected ? Dracula.Foreground : header ? Dracula.Purple : Dracula.Foreground);
            TrySet(a, "ForeColorDisabled", Dracula.Comment);
            TrySetEnumByName(a, "ThemedElementAlpha", "Transparent");
            // DevExpress liga a cor só com os flags Options.UseBackColor/UseForeColor.
            object opt = Get(a, "Options");
            if (opt != null) { TrySet(opt, "UseBackColor", true); TrySet(opt, "UseForeColor", true); TrySet(opt, "UseBorderColor", true); }
        }

        // ---------- Actipro SyntaxEditor ----------

        private static readonly HashSet<object> ours = new HashSet<object>();
        private static object toolbars;
        private const string CodeFont = "Cascadia Code", UiFont = "Segoe UI";

        // ---------- ícones Codicons (VS Code) na toolbar ----------

        private static readonly string codiconDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"GxDracula\codicons");
        private static System.Drawing.Text.PrivateFontCollection codiconFont;
        private static Dictionary<string, int> codepoints;
        private static readonly HashSet<object> iconed = new HashSet<object>();
        private static readonly string iconsOff = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GxDracula.icons.disabled");

        // Key da ferramenta (antes do "::") -> nome do codicon e cor.
        private static readonly Dictionary<string, KeyValuePair<string, Color>> iconMap = new Dictionary<string, KeyValuePair<string, Color>>
        {
            { "NewObject", I("new-file", Dracula.Cyan) }, { "Save", I("save", Dracula.Foreground) }, { "SaveAll", I("save-all", Dracula.Foreground) },
            { "Copy", I("copy", Dracula.Foreground) }, { "Paste", I("clippy", Dracula.Foreground) },
            { "Undo", I("discard", Dracula.Purple) }, { "Redo", I("redo", Dracula.Purple) }, { "UndoCloseDocument", I("history", Dracula.Purple) },
            { "BuildAll", I("package", Dracula.Orange) }, { "RebuildAll", I("refresh", Dracula.Orange) }, { "Cancel", I("debug-stop", Dracula.Red) },
            { "RunMainObject", I("play", Dracula.Green) }, { "CreateDatabase", I("database", Dracula.Yellow) },
            { "ZoomIn", I("zoom-in", Dracula.Foreground) }, { "ZoomOut", I("zoom-out", Dracula.Foreground) },
            { "Bold", I("bold", Dracula.Foreground) }, { "Italic", I("italic", Dracula.Foreground) },
            { "OrderedList", I("list-ordered", Dracula.Foreground) }, { "UnOrderedList", I("list-unordered", Dracula.Foreground) },
            { "ShowGrid", I("layout", Dracula.Comment) }, { "Indent", I("arrow-right", Dracula.Foreground) }, { "Unindent", I("arrow-left", Dracula.Foreground) },
        };

        private static KeyValuePair<string, Color> I(string name, Color c) { return new KeyValuePair<string, Color>(name, c); }

        private static void LoadCodicons()
        {
            string ttf = Path.Combine(codiconDir, "codicon.ttf"), css = Path.Combine(codiconDir, "codicon.css");
            if (!File.Exists(ttf) || !File.Exists(css)) return;
            codiconFont = new System.Drawing.Text.PrivateFontCollection();
            codiconFont.AddFontFile(ttf);
            codepoints = new Dictionary<string, int>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(css),
                @"\.codicon-([a-z0-9-]+):before\s*\{\s*content:\s*""\\([0-9a-f]+)"""))
                codepoints[m.Groups[1].Value] = Convert.ToInt32(m.Groups[2].Value, 16);
            Log("codicons " + codepoints.Count, null);
        }

        private static Bitmap Codicon(string name, Color color, int size)
        {
            int cp;
            if (codiconFont == null || !codepoints.TryGetValue(name, out cp)) return null;
            Bitmap b = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(b))
            using (Font f = new Font(codiconFont.Families[0], size * 0.8f, GraphicsUnit.Pixel))
            using (SolidBrush br = new SolidBrush(color))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.DrawString(char.ConvertFromUtf32(cp), f, br, new RectangleF(0, 0, size, size), sf);
            }
            return b;
        }

        private static void ThemeToolbarIcons()
        {
            if (toolbars == null)
                foreach (Form f in Application.OpenForms)
                    foreach (Control d in f.Controls)
                        if (d.GetType().Name == "UltraToolbarsDockArea" && Get(d, "ToolbarsManager") != null) toolbars = Get(d, "ToolbarsManager");
            if (toolbars == null) return;
            if (codiconFont == null) LoadCodicons();
            if (codiconFont == null) return;
            IEnumerable tools = Get(toolbars, "Tools") as IEnumerable;
            if (tools == null) return;
            foreach (object tool in tools)
            {
                if (iconed.Contains(tool)) continue;
                iconed.Add(tool);
                string key = (Get(tool, "Key") as string) ?? "";
                int sep = key.IndexOf("::");
                KeyValuePair<string, Color> icon;
                if (!iconMap.TryGetValue(sep < 0 ? key : key.Substring(0, sep), out icon)) continue;
                object app = Get(Get(Get(tool, "SharedProps"), "AppearancesSmall"), "Appearance");
                Image old = Get(app, "Image") as Image;
                Bitmap b = Codicon(icon.Key, icon.Value, old != null ? old.Width : 16);
                if (b != null) TrySet(app, "Image", b);
            }
        }

        private static void ThemeSyntaxEditor(Control editor) { ThemeSyntaxEditor(editor, true); }

        private static void ThemeSyntaxEditor(Control editor, bool track)
        {
            editor.BackColor = Dracula.Background;
            editor.ForeColor = Dracula.Foreground;
            if (editor.Font.FontFamily.Name != CodeFont) editor.Font = new Font(CodeFont, editor.Font.Size);
            // O tipo concreto (BaseSyntaxEditor) vive no assembly da Artech; o renderer está no do Actipro.
            Type rt = FindType("ActiproSoftware.SyntaxEditor.VisualStudio2005SyntaxEditorRenderer");
            Type fill = FindType("ActiproSoftware.Drawing.SolidColorBackgroundFill");
            if (rt != null && fill != null)
            {
                object r = Activator.CreateInstance(rt);
                Func<Color, object> F = delegate(Color c) { return Activator.CreateInstance(fill, c); };
                TrySet(r, "TextAreaBackgroundFill", F(Dracula.Background));
                TrySet(r, "TextAreaDisabledBackgroundFill", F(Dracula.Darker));
                TrySet(r, "LineNumberMarginBackgroundFill", F(Dracula.Background));
                TrySet(r, "LineNumberMarginDisabledBackgroundFill", F(Dracula.Darker));
                TrySet(r, "IndicatorMarginBackgroundFill", F(Dracula.Background));
                TrySet(r, "SelectionMarginBackgroundFill", F(Dracula.Background));
                TrySet(r, "SelectionMarginDisabledBackgroundFill", F(Dracula.Darker));
                TrySet(r, "UserMarginBackgroundFill", F(Dracula.Background));
                TrySet(r, "UserMarginDisabledBackgroundFill", F(Dracula.Darker));
                TrySet(r, "WordWrapMarginBackgroundFill", F(Dracula.Background));
                TrySet(r, "SplitterBackgroundFill", F(Dracula.Darker));
                TrySet(r, "ScrollBarBlockBackgroundFill", F(Dracula.Darker));
                TrySet(r, "LineNumberMarginForeColor", Dracula.Comment);
                TrySet(r, "UserMarginForeColor", Dracula.Comment);
                foreach (string b in new[] { "IndicatorMarginBorderColor", "LineNumberMarginBorderColor", "SelectionMarginBorderColor", "UserMarginBorderColor", "WordWrapMarginBorderColor" })
                    TrySet(r, b, Dracula.Background);
                TrySet(r, "CurrentLineHighlightBackColor", Dracula.LineHighlight);
                TrySet(r, "CurrentLineHighlightBorderColor", Dracula.LineHighlight);
                TrySet(r, "SelectionFocusedBackColor", Dracula.CurrentLine);
                TrySet(r, "SelectionFocusedBorderColor", Dracula.CurrentLine);
                TrySet(r, "SelectionFocusedForeColor", Color.Empty);
                TrySet(r, "SelectionUnfocusedBackColor", Dracula.LineHighlight);
                TrySet(r, "SelectionUnfocusedBorderColor", Dracula.LineHighlight);
                TrySet(r, "SelectionUnfocusedForeColor", Color.Empty);
                TrySet(r, "IndentationGuideColor", Dracula.CurrentLine);
                TrySet(r, "OutliningIndicatorBackColor", Dracula.Background);
                TrySet(r, "OutliningIndicatorForeColor", Dracula.Comment);
                TrySet(r, "WhitespaceSpaceForeColor", Dracula.CurrentLine);
                TrySet(r, "WhitespaceTabForeColor", Dracula.CurrentLine);
                TrySet(r, "ContentDividerColor", Dracula.CurrentLine);
                TrySet(r, "ResetAllPropertiesOnSystemColorChange", false);
                // Contorno do editor: o padrão é Fixed3D com SystemColors (branco no dark).
                Type sb = FindType("ActiproSoftware.Drawing.SimpleBorder");
                Type sbs = FindType("ActiproSoftware.Drawing.SimpleBorderStyle");
                if (sb != null && sbs != null)
                    try { TrySet(r, "Border", Activator.CreateInstance(sb, Enum.Parse(sbs, "Solid"), Dracula.Darker)); } catch { }
                ours.Add(r);
                TrySet(editor, "Renderer", r);
            }
            object caret = Get(editor, "Caret");
            TrySet(caret, "Color", Dracula.Foreground);
            if (track) editors.Add(new WeakReference(editor));
            ThemeEditorLanguage(editor);
        }

        private static void ThemeEditorLanguage(object editor)
        {
            object doc = Get(editor, "Document");
            ThemeLanguage(Get(doc, "Language"));
        }

        private static void ThemeLanguage(object lang)
        {
            if (lang == null || seenLanguages.Contains(lang)) return;
            seenLanguages.Add(lang);
            TrySet(lang, "BackColor", Dracula.Background);
            IEnumerable styles = Get(lang, "HighlightingStyles") as IEnumerable;
            if (styles != null)
                foreach (object s in styles) ThemeStyle(s);
            IEnumerable children = Get(lang, "ChildLanguages") as IEnumerable;
            if (children != null)
                foreach (object c in children) ThemeLanguage(c);
            Log("linguagem " + Get(lang, "Key"), null);
        }

        private static void ThemeStyle(object s)
        {
            string key = ((Get(s, "Key") as string ?? "") + " " + (Get(s, "Name") as string ?? "")).ToLowerInvariant();
            object fo = Get(s, "ForeColor");
            object bo = Get(s, "BackColor");
            Color fore = fo is Color ? (Color)fo : Color.Empty;
            Color back = bo is Color ? (Color)bo : Color.Empty;

            Color mapped;
            if (key.Contains("comment")) mapped = Dracula.Comment;
            else if (key.Contains("string") || key.Contains("char")) mapped = Dracula.Yellow;
            else if (key.Contains("number") || key.Contains("numeric") || key.Contains("constant") || key.Contains("date")) mapped = Dracula.Purple;
            else if (key.Contains("keyword") || key.Contains("reserved") || key.Contains("command") || key.Contains("operator")) mapped = Dracula.Pink;
            else if (key.Contains("variable")) mapped = Dracula.Orange;
            else if (key.Contains("attribute") || key.Contains("type") || key.Contains("domain")) mapped = Dracula.Cyan;
            else if (key.Contains("function") || key.Contains("method") || key.Contains("object") || key.Contains("call")) mapped = Dracula.Green;
            else if (key.Contains("error")) mapped = Dracula.Red;
            else if (key.Contains("default") || key.Contains("text") || key.Contains("identifier") || fore.IsEmpty) mapped = Dracula.Foreground;
            else mapped = Dracula.MapFore(fore);
            Log("estilo " + key + " fore=" + ColorTranslator.ToHtml(fore) + " back=" + ColorTranslator.ToHtml(back), null);
            TrySet(s, "ForeColor", mapped);

            if (!back.IsEmpty && back.A > 0)
                TrySet(s, "BackColor", Dracula.IsLight(back) && back.GetSaturation() < 0.2f ? Color.Empty : Dracula.CurrentLine);
            if (key.Contains("error")) TrySet(s, "UnderlineColor", Dracula.Red);
        }

        // ---------- Win32 ----------

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        // StatusBar (comctl32) ignora BackColor: sem tema visual aceita SB_SETBKCOLOR; o texto vai por owner-draw.
        private static void ThemeStatusBar(StatusBar s)
        {
            MethodInvoker paint = delegate
            {
                SetWindowTheme(s.Handle, "", "");
                SendMessage(s.Handle, 0x2001, IntPtr.Zero, (IntPtr)ColorTranslator.ToWin32(Dracula.Darkest));
                s.Invalidate();
            };
            if (s.IsHandleCreated) paint(); else s.HandleCreated += delegate { paint(); };
            // Painéis Sunken desenham relevo branco; sem borda, separados por uma linha escura no DrawItem.
            foreach (StatusBarPanel p in s.Panels) { p.Style = StatusBarPanelStyle.OwnerDraw; p.BorderStyle = StatusBarPanelBorderStyle.None; }
            s.DrawItem += delegate(object o, StatusBarDrawItemEventArgs e)
            {
                using (SolidBrush b = new SolidBrush(Dracula.Darkest)) e.Graphics.FillRectangle(b, e.Bounds);
                using (Pen sep = new Pen(Dracula.CurrentLine)) e.Graphics.DrawLine(sep, e.Bounds.Right - 1, e.Bounds.Top + 3, e.Bounds.Right - 1, e.Bounds.Bottom - 3);
                TextRenderer.DrawText(e.Graphics, e.Panel.Text, s.Font, e.Bounds, Dracula.Foreground,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            Log("statusbar " + s.Panels.Count + " painéis", null);
        }

        // TreeList da DevExpress (part Variables): o skin pinta o cabeçalho e ignora Appearance; em Flat a Appearance vale.
        private static void ThemeTreeList(Control t)
        {
            object lf = Get(t, "LookAndFeel");
            TrySet(lf, "UseDefaultLookAndFeel", false);
            TrySet(lf, "UseWindowsXPTheme", false);
            TrySetEnumByName(lf, "Style", "UltraFlat");
            Log("treelist lf style=" + Get(lf, "Style") + " default=" + Get(lf, "UseDefaultLookAndFeel") + " active=" + Get(lf, "ActiveStyle"), null);
            object ap = Get(t, "Appearance");
            DxApp(Get(ap, "Empty"), Dracula.Background, Dracula.Foreground);
            DxApp(Get(ap, "Row"), Dracula.Background, Dracula.Foreground);
            DxApp(Get(ap, "EvenRow"), Dracula.Background, Dracula.Foreground);
            DxApp(Get(ap, "OddRow"), Dracula.Background, Dracula.Foreground);
            DxApp(Get(ap, "HeaderPanel"), Dracula.Darkest, Dracula.Purple);
            DxApp(Get(ap, "FocusedRow"), Dracula.CurrentLine, Dracula.Foreground);
            // FocusedCell pinta a coluna inteira da célula focada; só a linha deve destacar.
            DxApp(Get(ap, "FocusedCell"), Dracula.Background, Dracula.Foreground);
            TrySet(Get(t, "OptionsSelection"), "EnableAppearanceFocusedCell", false);
            DxApp(Get(ap, "SelectedRow"), Dracula.CurrentLine, Dracula.Foreground);
            DxApp(Get(ap, "HideSelectionRow"), Dracula.LineHighlight, Dracula.Foreground);
            DxApp(Get(ap, "TreeLine"), Dracula.Comment, Dracula.Comment);
            DxApp(Get(ap, "HorzLine"), Dracula.Darker, Dracula.Darker);
            DxApp(Get(ap, "VertLine"), Dracula.Darker, Dracula.Darker);
            IEnumerable cols = Get(t, "Columns") as IEnumerable;
            if (cols != null)
                foreach (object col in cols)
                {
                    DxApp(Get(col, "AppearanceCell"), Dracula.Background, Dracula.Foreground);
                    DxApp(Get(col, "AppearanceHeader"), Dracula.Darkest, Dracula.Purple);
                }
            if (ap != null)
                foreach (PropertyInfo p in ap.GetType().GetProperties())
                {
                    object a = null; try { a = p.GetValue(ap, null); } catch { }
                    object bc = Get(a, "BackColor");
                    if (bc is Color && !((Color)bc).IsEmpty) Log("treelist ap " + p.Name + "=" + bc, null);
                }
            t.Invalidate();
        }

        // Combo DropDownList pinta a borda com SystemColors; repinta 2 px por cima depois do WM_PAINT.
        private sealed class BorderPainter : NativeWindow
        {
            private readonly Control c;
            public BorderPainter(Control c)
            {
                this.c = c;
                if (c.IsHandleCreated) AssignHandle(c.Handle);
                c.HandleCreated += delegate { AssignHandle(c.Handle); };
                c.HandleDestroyed += delegate { ReleaseHandle(); };
            }
            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != 0x000F || !running) return;
                using (Graphics g = Graphics.FromHwnd(c.Handle))
                using (Pen p = new Pen(Dracula.CurrentLine))
                using (Pen q = new Pen(Dracula.Background))
                using (SolidBrush bb = new SolidBrush(Dracula.Background))
                using (Pen chev = new Pen(Dracula.Purple, 1.6f))
                {
                    // Botão do dropdown: fundo escuro + chevron roxo (no lugar do botão branco do sistema).
                    int w = SystemInformation.VerticalScrollBarWidth + 2;
                    Rectangle btn = new Rectangle(c.Width - w - 1, 1, w, c.Height - 2);
                    g.FillRectangle(bb, btn);
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    int cx = btn.Left + btn.Width / 2, cy = btn.Top + btn.Height / 2;
                    g.DrawLines(chev, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                    g.DrawRectangle(p, 0, 0, c.Width - 1, c.Height - 1);
                    g.DrawRectangle(q, 1, 1, c.Width - 3, c.Height - 3);
                }
            }
        }

        // UltraGrid: a área sem linhas vem de DisplayLayout.Appearance (o Walk pula DisplayLayout por ser enorme).
        private static void ThemeUltraGrid(Control g)
        {
            object dl = Get(g, "DisplayLayout");
            if (dl == null) return;
            IgApp(Get(dl, "Appearance"), Dracula.Background, Dracula.Foreground);
            object ov = Get(dl, "Override");
            IgApp(Get(ov, "RowAppearance"), Dracula.Background, Dracula.Foreground);
            IgApp(Get(ov, "RowAlternateAppearance"), Dracula.Background, Dracula.Foreground);
            IgApp(Get(ov, "CellAppearance"), Dracula.Background, Dracula.Foreground);
            IgApp(Get(ov, "HeaderAppearance"), Dracula.Darkest, Dracula.Purple);
            IgApp(Get(ov, "RowSelectorAppearance"), Dracula.Darkest, Dracula.Foreground);
            IgApp(Get(ov, "SelectedRowAppearance"), Dracula.CurrentLine, Dracula.Foreground);
            IgApp(Get(ov, "ActiveRowAppearance"), Dracula.CurrentLine, Dracula.Foreground);
            IgApp(Get(ov, "GroupByRowAppearance"), Dracula.Darker, Dracula.Cyan);
            IgApp(Get(Get(dl, "GroupByBox"), "Appearance"), Dracula.Darker, Dracula.Comment);
            IgApp(Get(dl, "CaptionAppearance"), Dracula.Darkest, Dracula.Purple);
            TrySetEnumByName(dl, "HeaderStyle", "Standard");
            g.Invalidate();
        }

        private static void IgApp(object a, Color back, Color fore)
        {
            if (a == null) return;
            TrySet(a, "BackColor", back);
            TrySet(a, "BackColor2", back);
            TrySet(a, "ForeColor", fore);
            TrySet(a, "BorderColor", Dracula.Darker);
            TrySetEnumByName(a, "BackGradientStyle", "None");
            TrySetEnumByName(a, "ThemedElementAlpha", "Transparent");
        }

        private static void DxApp(object a, Color back, Color fore)
        {
            if (a == null) return;
            TrySet(a, "BackColor", back);
            TrySet(a, "BackColor2", back);
            TrySet(a, "ForeColor", fore);
            TrySet(a, "BorderColor", Dracula.Darker);
            TrySet(Get(a, "Options"), "UseBackColor", true);
            TrySet(Get(a, "Options"), "UseForeColor", true);
        }

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hwnd, IntPtr rect, IntPtr rgn, int flags);

        // Scrollbar é área não-cliente: Invalidate não repinta; RDW_FRAME | RDW_INVALIDATE | RDW_UPDATENOW.
        private static void RedrawFrame(Control c) { RedrawWindow(c.Handle, IntPtr.Zero, IntPtr.Zero, 0x0400 | 0x0001 | 0x0100 | 0x0080); }

        // Seleção do TreeView nativo sai no azul do sistema (Options): desenha o texto do nó.
        // Só em árvores sem owner-draw próprio, para não brigar com quem já desenha.
        // TabControl do WinForms (abas do Options > Text): com visual styles a página e as abas saem brancas.
        private static void ThemeWinTabs(TabControl tc)
        {
            tc.BackColor = Dracula.Darker;
            if (tc.IsHandleCreated) TabFramePainter.Attach(tc); else tc.HandleCreated += delegate { TabFramePainter.Attach(tc); };
            // Recarga a quente: o handler da versão anterior do tema continua pendurado; troca pelo atual.
            FieldInfo fd = typeof(TabControl).GetField("onDrawItem", BindingFlags.NonPublic | BindingFlags.Instance);
            Delegate old = fd == null ? null : fd.GetValue(tc) as Delegate;
            if (old != null)
            {
                foreach (Delegate one in old.GetInvocationList())
                    if (one.Method.DeclaringType == null || !one.Method.DeclaringType.Assembly.GetName().Name.StartsWith("GxDracula")) return;
                fd.SetValue(tc, null);
            }
            tc.DrawMode = TabDrawMode.OwnerDrawFixed;
            tc.DrawItem += delegate(object s, DrawItemEventArgs e)
            {
                if (e.Index < 0 || e.Index >= tc.TabPages.Count) return;
                bool sel = e.Index == tc.SelectedIndex;
                Rectangle r = tc.GetTabRect(e.Index);
                // Margem que o controle nativo deixa em volta da aba.
                using (SolidBrush b = new SolidBrush(Dracula.Darker)) e.Graphics.FillRectangle(b, Rectangle.Inflate(r, 2, 2));
                using (SolidBrush b = new SolidBrush(sel ? Dracula.Background : Dracula.Darker)) e.Graphics.FillRectangle(b, r);
                if (sel) using (Pen p = new Pen(Dracula.Purple, 2)) e.Graphics.DrawLine(p, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
                TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text, tc.Font, r, Dracula.Foreground,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };
            tc.Invalidate();
        }

        // A moldura da página e a faixa das abas vêm do tema visual, fora do DrawItem: repinta por cima no WM_PAINT.
        private sealed class TabFramePainter : NativeWindow
        {
            private static readonly System.Collections.Generic.HashSet<IntPtr> done = new System.Collections.Generic.HashSet<IntPtr>();
            private readonly TabControl tc;
            public static void Attach(TabControl t) { if (done.Add(t.Handle)) { new TabFramePainter(t); t.Invalidate(); } }
            private TabFramePainter(TabControl t) { tc = t; AssignHandle(t.Handle); t.HandleDestroyed += delegate { done.Remove(tc.Handle); ReleaseHandle(); }; }
            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != 0x000F || tc.TabCount == 0) return;
                using (Graphics g = Graphics.FromHwnd(tc.Handle))
                {
                    Rectangle d = tc.DisplayRectangle;
                    Rectangle frame = Rectangle.FromLTRB(d.Left - 4, d.Top - 4, d.Right + 3, d.Bottom + 3);
                    using (Pen p = new Pen(Dracula.Darker, 4)) g.DrawRectangle(p, Rectangle.Inflate(frame, -1, -1));
                    using (Pen p = new Pen(Dracula.CurrentLine)) g.DrawRectangle(p, frame);
                    Rectangle last = tc.GetTabRect(tc.TabCount - 1);
                    using (SolidBrush b = new SolidBrush(Dracula.Darker))
                        g.FillRectangle(b, Rectangle.FromLTRB(last.Right + 1, 0, tc.Width, Math.Max(frame.Top, last.Bottom + 2)));
                    for (int i = 0; i < tc.TabCount; i++)
                    {
                        Rectangle r = tc.GetTabRect(i);
                        using (Pen p = new Pen(Dracula.Darker, 2)) g.DrawRectangle(p, r);
                    }
                }
            }
        }

        private static TreeNode FindNode(TreeNodeCollection ns, string text)
        {
            foreach (TreeNode n in ns)
            {
                if (n.Text == text) return n;
                TreeNode r = FindNode(n.Nodes, text);
                if (r != null) return r;
            }
            return null;
        }

        private static void DarkTreeSelection(TreeView t)
        {
            if (t.DrawMode == TreeViewDrawMode.OwnerDrawAll && t.GetType().Name == "GxTreeView")
            {
                // GxTreeView desenha tudo sozinho com o azul do sistema na seleção: repinta só o texto do nó selecionado.
                t.DrawNode += delegate(object s, DrawTreeNodeEventArgs e)
                {
                    if (e.Node.Bounds.IsEmpty) return;
                    bool sel = (e.State & TreeNodeStates.Selected) != 0;
                    // O GxTreeView escreve o texto ~Indent+8 px antes do Bounds nativo (medido: 31 px com Indent 23).
                    Rectangle nb = e.Node.Bounds;
                    int x = nb.X - t.Indent - 8;
                    int w = TextRenderer.MeasureText(e.Node.Text, e.Node.NodeFont ?? t.Font).Width + 4;
                    using (SolidBrush b = new SolidBrush(t.BackColor)) e.Graphics.FillRectangle(b, Rectangle.FromLTRB(x, nb.Y, Math.Max(nb.Right + 2, x + w), nb.Bottom));
                    Rectangle r = new Rectangle(x, nb.Y, w, nb.Height);
                    // Texto de todos os nós: o GxTreeView usa um cinza escuro, ilegível no fundo Dracula.
                    using (SolidBrush b = new SolidBrush(sel ? Dracula.CurrentLine : t.BackColor)) e.Graphics.FillRectangle(b, r);
                    TextRenderer.DrawText(e.Graphics, e.Node.Text, e.Node.NodeFont ?? t.Font, r, sel ? Dracula.Purple : Dracula.Foreground,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                };
                Log("gxtree img=" + Get(t, "ImageSize") + " space=" + Get(t, "NodeTextSpace") + " exp=" + Get(t, "ExpansionIndicatorSize") + " draw=" + Get(t, "DrawImages") + " own=" + Get(t, "UseOwnImages") + " indent=" + t.Indent, null);
                t.Invalidate();
                return;
            }
            if (t.DrawMode != TreeViewDrawMode.Normal) { Log("tree " + t.Name + " drawmode=" + t.DrawMode, null); return; }
            Log("tree " + t.Name + " seleção escura", null);
            t.HideSelection = false;
            t.DrawMode = TreeViewDrawMode.OwnerDrawText;
            t.DrawNode += delegate(object s, DrawTreeNodeEventArgs e)
            {
                if (e.Bounds.IsEmpty) return;
                bool sel = (e.State & TreeNodeStates.Selected) != 0;
                Rectangle r = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width + 4, e.Bounds.Height);
                using (SolidBrush b = new SolidBrush(sel ? Dracula.CurrentLine : t.BackColor)) e.Graphics.FillRectangle(b, r);
                Font f = e.Node.NodeFont ?? t.Font;
                TextRenderer.DrawText(e.Graphics, e.Node.Text, f, r, sel ? Dracula.Purple : Dracula.Foreground,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            };
        }

        private static void NativeDark(Control c, string theme)
        {
            if (c.IsHandleCreated) { SetWindowTheme(c.Handle, theme, null); RedrawFrame(c); }
            else c.HandleCreated += delegate { SetWindowTheme(c.Handle, theme, null); RedrawFrame(c); };
        }

        private static void DarkTitleBar(Control form)
        {
            EventHandler set = delegate
            {
                int on = 1;
                if (DwmSetWindowAttribute(form.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(form.Handle, 19, ref on, 4);
            };
            if (form.IsHandleCreated) set(form, EventArgs.Empty);
            else form.HandleCreated += set;
        }

        // ---------- reflection helpers ----------

        private static object Get(object o, string name)
        {
            if (o == null) return null;
            // GetProperties em vez de GetProperty: propriedade redeclarada com "new" dá AmbiguousMatchException.
            foreach (PropertyInfo p in o.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (p.Name != name || p.GetIndexParameters().Length > 0) continue;
                try { return p.GetValue(o, null); } catch { return null; }
            }
            return null;
        }

        private static void TrySet(object o, string name, object value)
        {
            if (o == null) return;
            try
            {
                PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.CanWrite && (value == null || p.PropertyType.IsAssignableFrom(value.GetType())))
                    p.SetValue(o, value, null);
            }
            catch { }
        }

        private static void TrySetEnumByName(object o, string name, string member)
        {
            if (o == null) return;
            try
            {
                PropertyInfo p = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                if (p != null && p.CanWrite && p.PropertyType.IsEnum && Enum.IsDefined(p.PropertyType, member))
                    p.SetValue(o, Enum.Parse(p.PropertyType, member), null);
            }
            catch { }
        }

        private static void SetField(object o, string name, object value)
        {
            FieldInfo f = o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f != null) f.SetValue(o, value);
        }

        private static void TrySetStatic(Type t, string name, object value)
        {
            try
            {
                PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
                if (p != null && p.CanWrite) p.SetValue(null, value, null);
            }
            catch (Exception ex) { Log("static " + name, ex); }
        }
    }

    internal class DraculaColors : ProfessionalColorTable
    {
        public override Color ToolStripGradientBegin { get { return Dracula.Darker; } }
        public override Color ToolStripGradientMiddle { get { return Dracula.Darker; } }
        public override Color ToolStripGradientEnd { get { return Dracula.Darker; } }
        public override Color ToolStripBorder { get { return Dracula.Darkest; } }
        public override Color ToolStripDropDownBackground { get { return Dracula.Darker; } }
        public override Color MenuStripGradientBegin { get { return Dracula.Darkest; } }
        public override Color MenuStripGradientEnd { get { return Dracula.Darkest; } }
        public override Color MenuBorder { get { return Dracula.Darkest; } }
        public override Color MenuItemBorder { get { return Dracula.CurrentLine; } }
        public override Color MenuItemSelected { get { return Dracula.CurrentLine; } }
        public override Color MenuItemSelectedGradientBegin { get { return Dracula.CurrentLine; } }
        public override Color MenuItemSelectedGradientEnd { get { return Dracula.CurrentLine; } }
        public override Color MenuItemPressedGradientBegin { get { return Dracula.Background; } }
        public override Color MenuItemPressedGradientEnd { get { return Dracula.Background; } }
        public override Color ImageMarginGradientBegin { get { return Dracula.Darker; } }
        public override Color ImageMarginGradientMiddle { get { return Dracula.Darker; } }
        public override Color ImageMarginGradientEnd { get { return Dracula.Darker; } }
        public override Color ButtonSelectedHighlight { get { return Dracula.CurrentLine; } }
        public override Color ButtonSelectedGradientBegin { get { return Dracula.CurrentLine; } }
        public override Color ButtonSelectedGradientEnd { get { return Dracula.CurrentLine; } }
        public override Color ButtonSelectedBorder { get { return Dracula.Comment; } }
        public override Color ButtonPressedGradientBegin { get { return Dracula.Comment; } }
        public override Color ButtonPressedGradientEnd { get { return Dracula.Comment; } }
        public override Color ButtonCheckedGradientBegin { get { return Dracula.CurrentLine; } }
        public override Color ButtonCheckedGradientEnd { get { return Dracula.CurrentLine; } }
        public override Color SeparatorDark { get { return Dracula.CurrentLine; } }
        public override Color SeparatorLight { get { return Dracula.Darker; } }
        public override Color GripDark { get { return Dracula.Comment; } }
        public override Color GripLight { get { return Dracula.Darker; } }
        public override Color StatusStripGradientBegin { get { return Dracula.Purple; } }
        public override Color StatusStripGradientEnd { get { return Dracula.Purple; } }
    }

    internal class DraculaRenderer : ToolStripProfessionalRenderer
    {
        public DraculaRenderer() : base(new DraculaColors()) { RoundedEdges = false; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? (e.ToolStrip is StatusStrip ? Dracula.Background : Dracula.Foreground) : Dracula.Comment;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Dracula.Foreground;
            base.OnRenderArrow(e);
        }
    }
}
