using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace GxDracula
{
    // Acabamento das abas (documentos Infragistics e parts Crownwood). Chamado a cada tick do tema.
    public static partial class Theme
    {
        private static readonly HashSet<Form> tabFormsScanned = new HashSet<Form>();
        private static readonly List<WeakReference> mdiManagers = new List<WeakReference>();


        private static void ThemeTabsExtra()
        {
            foreach (Form f in Application.OpenForms)
            {
                if (!f.IsMdiContainer || tabFormsScanned.Contains(f)) continue;
                tabFormsScanned.Add(f);
                foreach (Control c in f.Controls)
                {
                    if (c.GetType().FullName != "Infragistics.Win.UltraWinTabbedMdi.MdiTabGroupControl") continue;
                    object m = Get(c, "Manager") ?? Get(Get(c, "TabGroup"), "Manager");
                    if (m == null) foreach (PropertyInfo p in c.GetType().GetProperties(BindingFlags.Instance | BindingFlags.NonPublic))
                        if (p.PropertyType.Name == "UltraTabbedMdiManager" || p.PropertyType.Name == "MdiTabGroup")
                        {
                            object v = p.GetValue(c, null);
                            m = p.PropertyType.Name == "MdiTabGroup" ? Get(v, "Manager") : v;
                            if (m != null) break;
                        }
                    bool dup = false;
                    foreach (WeakReference w0 in mdiManagers) if (w0.Target == m) dup = true;
                    if (m != null && !dup) mdiManagers.Add(new WeakReference(m));
                }
                Log("tabs: forms mdi " + f.GetType().FullName + " managers=" + mdiManagers.Count, null);
            }
            ThemeCrownTabsExtra();
            foreach (WeakReference w in mdiManagers.ToArray())
            {
                object m = w.Target;
                if (m == null) { mdiManagers.Remove(w); continue; }
                // O Walk do Theme.cs tinge as aparências de cada MdiTab/MdiTabGroup, que vencem as do manager.
                IEnumerable groups = Get(m, "TabGroups") as IEnumerable;
                if (groups != null)
                    foreach (object g in groups)
                    {
                        object gset = Get(g, "Settings"); if (!Equals(Convert.ToString(Get(Get(gset, "CloseButtonAppearance"), "BackColorAlpha")), "Transparent")) StyleTabSettings(gset, null);
                        IEnumerable tabs = Get(g, "Tabs") as IEnumerable;
                        if (tabs == null) continue;
                        foreach (object t in tabs)
                            {
                                object tset = Get(t, "Settings");
                                if (!Same(Get(tset, "ActiveTabAppearance"), Dracula.Background)) StyleTabSettings(null, tset);
                                CrispTabIcon(t, tset);
                            }
                    }
                // Reaplica só se alguém (Walk, IDE) mexeu: a cor da aba ativa é o sentinela.
                object act = Get(Get(m, "TabSettings"), "ActiveTabAppearance");
                object bc = Get(act, "BackColor");
                if (bc is Color && (Color)bc == Dracula.Background && Get(act, "ImageBackground") != null) continue;
                StyleMdiTabs(m);
            }
        }

        private sealed class TabIcon { public Icon Src; public Bitmap Img; }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, TabIcon> tabIcons =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Form, TabIcon>();

        // O ícone do form vem em 25x25 com borda semitransparente e o manager reduz para 20x20: fica borrado e apagado
        // no fundo escuro. Reamostra uma vez para o ImageSize e reforça o alfa das bordas.
        private static void CrispTabIcon(object tab, object tset)
        {
            Form f = Get(tab, "Form") as Form;
            object ap = Get(tset, "TabAppearance");
            if (f == null || ap == null || f.Icon == null) return;
            TabIcon ti = tabIcons.GetOrCreateValue(f);
            if (ti.Src != f.Icon || ti.Img == null)
            {
                ti.Src = f.Icon;
                ti.Img = BoostIcon(f.Icon, 20);
            }
            if (!ReferenceEquals(Get(ap, "Image"), ti.Img)) { TrySet(ap, "Image", ti.Img); TrySetEnumByName(ap, "ImageAlpha", "Opaque"); }
        }

        private static Bitmap BoostIcon(Icon ic, int size)
        {
            Bitmap dst = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Bitmap src = ic.ToBitmap())
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(src, new Rectangle(0, 0, size, size));
            }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Color c = dst.GetPixel(x, y);
                    if (c.A < 0x18) { dst.SetPixel(x, y, Color.Transparent); continue; }
                    dst.SetPixel(x, y, Color.FromArgb(Math.Min(255, c.A * 2), c.R, c.G, c.B));
                }
            return dst;
        }

        // ---------- abas das parts (Crownwood MultiBox) ----------

        // Lido no MagicLocalLibrary descompilado: TabPage nasce com ColorTab = White e OnSystemColorsChanged chama
        // DefineBackColor(DefaultBackColor), que recalcula _backLight*/_backIDE em cinza claro. Sem os ganchos, a aba nova
        // ou a faixa inteira ficava clara até o próximo tick (700 ms) ao trocar de objeto/part.
        private static void HookCrownTabs(Control c)
        {
            object pages = Get(c, "TabPages");
            EventInfo ins = pages == null ? null : pages.GetType().GetEvent("Inserted");
            if (ins != null)
                try { ins.AddEventHandler(pages, Delegate.CreateDelegate(ins.EventHandlerType, typeof(Theme).GetMethod("CrownPageInserted", BindingFlags.Static | BindingFlags.NonPublic))); }
                catch (Exception ex) { Log("tabs: inserted", ex); }
            c.SystemColorsChanged += delegate
            {
                if (!running) return;
                c.BackColor = Dracula.Darker;
                SetField(c, "_backIDE", Dracula.Darker);
                SetField(c, "_backLight", Dracula.Background);
                SetField(c, "_backLightLight", Dracula.Background);
                SetField(c, "_backDark", Dracula.Darkest);
                SetField(c, "_backDarkDark", Dracula.Darkest);
                c.Invalidate();
            };
        }

        private static void CrownPageInserted(int index, object page)
        {
            if (running) TrySet(page, "ColorTab", Dracula.Darker);
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, CrownTabPainter> crownPainters =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Control, CrownTabPainter>();

        private static void ThemeCrownTabsExtra()
        {
            foreach (WeakReference w in crownTabs)
            {
                Control c = w.Target as Control;
                if (c == null || c.IsDisposed) continue;
                CrownTabPainter cp;
                if (crownPainters.TryGetValue(c, out cp)) continue;
                crownPainters.Add(c, new CrownTabPainter(c));
                c.Invalidate();
            }
        }

        // Repinta a faixa de abas por cima do Crownwood logo depois do WM_PAINT dele (estilo VS Code).
        private sealed class CrownTabPainter : NativeWindow
        {
            private readonly Control c;
            private FieldInfo rectsF, areaF, hotF;

            public CrownTabPainter(Control c)
            {
                this.c = c;
                Type t = c.GetType();
                for (; t != null && rectsF == null; t = t.BaseType)
                {
                    rectsF = t.GetField("_tabRects", BindingFlags.Instance | BindingFlags.NonPublic);
                    areaF = t.GetField("_tabsAreaRect", BindingFlags.Instance | BindingFlags.NonPublic);
                    hotF = t.GetField("_hotTrackPage", BindingFlags.Instance | BindingFlags.NonPublic);
                }
                if (c.IsHandleCreated) AssignHandle(c.Handle);
                c.HandleCreated += delegate { AssignHandle(c.Handle); };
                c.HandleDestroyed += delegate { ReleaseHandle(); };
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                if (m.Msg != 0x000F || !running || rectsF == null) return;
                try { Paint(); } catch (Exception ex) { rectsF = null; Log("tabs: crown paint", ex); }
            }

            private void Paint()
            {
                ArrayList rects = rectsF.GetValue(c) as ArrayList;
                IList pages = Get(c, "TabPages") as IList;
                if (rects == null || pages == null || rects.Count == 0) return;
                int sel = Convert.ToInt32(Get(c, "SelectedIndex") ?? -1);
                int hot = hotF == null ? -1 : (int)hotF.GetValue(c);
                Rectangle area = areaF == null ? Rectangle.Empty : (Rectangle)areaF.GetValue(c);
                Font font = c.Font;
                using (Graphics g = Graphics.FromHwnd(c.Handle))
                using (SolidBrush darker = new SolidBrush(Dracula.Darker))
                using (SolidBrush bg = new SolidBrush(Dracula.Background))
                using (SolidBrush hotB = new SolidBrush(Dracula.LineHighlight))
                using (SolidBrush line = new SolidBrush(Dracula.Pink))
                using (Pen sep = new Pen(Dracula.Darkest))
                {
                    if (!area.IsEmpty) g.FillRectangle(darker, area);
                    for (int i = 0; i < rects.Count && i < pages.Count; i++)
                    {
                        if (!(rects[i] is Rectangle)) continue;
                        Rectangle r = (Rectangle)rects[i];
                        if (r.Width <= 0) continue;
                        // A caixa do MultiBox é desenhada um pouco maior que o retângulo lógico.
                        r.Inflate(1, 1);
                        bool s = i == sel;
                        g.FillRectangle(s ? bg : i == hot ? hotB : darker, r);
                        if (s) g.FillRectangle(line, r.X, r.Bottom - 2, r.Width, 2);
                        else g.DrawLine(sep, r.Right - 1, r.Y + 4, r.Right - 1, r.Bottom - 5);
                        string text = Convert.ToString(Get(pages[i], "Title"));
                        if (string.IsNullOrEmpty(text)) text = ((Control)pages[i]).Text;
                        TextRenderer.DrawText(g, text, font, r, s || i == hot ? Dracula.Foreground : TabMuted,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                    }
                }
            }
        }

        private static bool Same(object a, Color c) { object v = Get(a, "BackColor"); return v is Color && ((Color)v).ToArgb() == c.ToArgb(); }

        private static readonly Color TabMuted = Color.FromArgb(0xBF, 0xBF, 0xBF);
        private static Bitmap lineActive, lineSelected, lineNone;

        // Faixa de 1 px de largura: 2 px de cor no topo e o fundo embaixo, esticada com margem superior fixa.
        private static Bitmap TabLine(Color top, Color back)
        {
            Bitmap b = new Bitmap(3, 8);
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 3; x++) b.SetPixel(x, y, y < 2 ? top : back);
            return b;
        }

        private static void IgTab(object a, Color back, Color fore, Bitmap line)
        {
            if (a == null) return;
            TrySet(a, "BackColor", back);
            TrySet(a, "BackColor2", back);
            TrySetEnumByName(a, "BackGradientStyle", "None");
            TrySet(a, "BorderColor", back);
            TrySet(a, "ForeColor", fore);
            TrySetEnumByName(a, "ImageAlpha", "Opaque");
            TrySetEnumByName(a, "ForegroundAlpha", "Opaque");
            TrySetEnumByName(a, "BackColorAlpha", "Opaque");
            TrySetEnumByName(a, "ThemedElementAlpha", "Transparent");
            if (line != null)
            {
                TrySet(a, "ImageBackground", line);
                TrySetEnumByName(a, "ImageBackgroundStyle", "Stretched");
                Type mt = FindType("Infragistics.Win.ImageBackgroundStretchMargins");
                if (mt != null)
                    try { TrySet(a, "ImageBackgroundStretchMargins", Activator.CreateInstance(mt, new object[] { 0, 2, 0, 0 })); } catch { }
            }
        }

        private static void StyleMdiTabs(object m)
        {
            if (lineActive == null)
            {
                lineActive = TabLine(Dracula.Pink, Dracula.Background);
                lineSelected = TabLine(Dracula.Purple, Dracula.Background);
                lineNone = TabLine(Dracula.Darker, Dracula.Darker);
            }
            StyleTabSettings(Get(m, "TabGroupSettings"), Get(m, "TabSettings"));
        }

        private static void StyleTabSettings(object gs, object ts)
        {
            if (lineActive == null)
            {
                lineActive = TabLine(Dracula.Pink, Dracula.Background);
                lineSelected = TabLine(Dracula.Purple, Dracula.Background);
                lineNone = TabLine(Dracula.Darker, Dracula.Darker);
            }
            IgTab(Get(ts, "TabAppearance"), Dracula.Darker, TabMuted, lineNone);
            IgTab(Get(ts, "HotTrackTabAppearance"), Dracula.LineHighlight, Dracula.Foreground, null);
            IgTab(Get(ts, "SelectedTabAppearance"), Dracula.Background, Dracula.Foreground, lineSelected);
            IgTab(Get(ts, "ActiveTabAppearance"), Dracula.Background, Dracula.Foreground, lineActive);
            IgTab(Get(gs, "TabAreaAppearance"), Dracula.Darker, TabMuted, null);
            IgTab(Get(gs, "CloseButtonAppearance"), Dracula.Darker, Dracula.Comment, null);
            TrySetEnumByName(Get(gs, "CloseButtonAppearance"), "BackColorAlpha", "Transparent");
            TrySetEnumByName(Get(gs, "CloseButtonAppearance"), "BorderAlpha", "Transparent");
            IgTab(Get(gs, "HotTrackCloseButtonAppearance"), Dracula.CurrentLine, Dracula.Foreground, null);
            IgTab(Get(gs, "PressedCloseButtonAppearance"), Dracula.Comment, Dracula.Foreground, null);
            TrySetEnumByName(gs, "TabStyle", "Flat");
            TrySetEnumByName(gs, "ButtonStyle", "Flat");
            TrySetEnumByName(ts, "HotTrack", "True");
        }
    }
}
