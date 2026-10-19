using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GxDracula
{
    public static partial class Theme
    {
        private static object spacedManager;
        private static int thumbScan;
        private static readonly System.Collections.Generic.HashSet<int> scrollStyled = new System.Collections.Generic.HashSet<int>();

        // Controles Infragistics (UltraTree do Patterns, Outliner…) desenham a própria scrollbar: thumb quase invisível no esquema Black.
        private static void StyleScrollLook(Control k)
        {
            object sl = Get(k, "ScrollBarLook") ?? Get(Get(k, "DisplayLayout"), "ScrollBarLook");
            if (InheritsFrom(k, "Infragistics.Win.UltraWinGrid")) StyleGridHeaders(k);
            if (sl == null) return;
            Color thumb = Color.FromArgb(0x9A, 0x9E, 0xB8), hot = Dracula.Purple;
            IgApp(Get(sl, "Appearance"), Dracula.Darkest, Dracula.Comment);
            IgApp(Get(sl, "TrackAppearance"), Dracula.Darkest, Dracula.Comment);
            IgApp(Get(sl, "ButtonAppearance"), Dracula.Darkest, Dracula.Comment);
            IgApp(Get(sl, "ThumbAppearance"), thumb, thumb);
            TrySet(Get(sl, "ThumbAppearance"), "BorderColor", thumb);
            foreach (string n in new[] { "ThumbAppearanceVertical", "ThumbAppearanceHorizontal" }) IgApp(Get(sl, n), thumb, thumb);
            foreach (string n in new[] { "TrackAppearanceVertical", "TrackAppearanceHorizontal", "ButtonAppearanceVertical", "ButtonAppearanceHorizontal" })
                IgApp(Get(sl, n), Dracula.Darkest, Dracula.Comment);
            TrySetEnumByName(sl, "ViewStyle", "Standard");
            if (!InheritsFrom(k, "Infragistics.Win.UltraWinGrid"))
            {
                TrySetEnumByName(k, "UseOsThemes", "False");
                TrySet(k, "UseAppStyling", false);
            }
            else TrySet(k, "UseAppStyling", true);
            k.Invalidate();
            Log("scrollbarlook " + k.Name, null);
        }

        // Controles criados antes do reload a quente não passam de novo pelo Apply: varre as árvores de vez em quando.
        private static void AttachThumbs()
        {
            if ((thumbScan++ % 10) != 0) return;
            try { foreach (Form f in Application.OpenForms) AttachThumbs(f); }
            catch (Exception ex) { if (thumbScan < 30) Log("thumb scan", ex); }
        }

        private static void AttachThumbs(Control c)
        {
            foreach (Control k in c.Controls)
            {
                if (k.GetType().Namespace != null && k.GetType().Namespace.StartsWith("Artech") && InheritsFrom(k, "Infragistics") && scrollStyled.Add(k.GetHashCode() ^ 71)) StyleScrollLook(k);
                if (k.Visible && InheritsFrom(k, "Infragistics.Win.UltraWinGrid"))
                {
                    FixRowAppearances(k);
                    // Toda grid (não só as de tipo Artech) recebe o filtro atual: sem ele a borda a cada 2 linhas aparece.
                    object df = Get(k, "DrawFilter");
                    if (df == null || !(df is DraculaDrawFilter) && df.GetType().FullName == typeof(DraculaDrawFilter).FullName) { SetDrawFilter(k); k.Invalidate(); }
                }
                // Editores criados antes do reload (combo Version da History) ficam sem o filtro das setas.
                if (InheritsFrom(k, "Infragistics.Win.UltraWinEditors") && Get(k, "DrawFilter") == null) { SetDrawFilter(k); k.Invalidate(); }
                // CheckBox em modo botão (filtro do Team Development): borda branca do sistema.
                ComboBox co = k as ComboBox;
                if (co != null && combosFixed.Add(co.GetHashCode() ^ 3)) ResetCombo(co);
                // headerComboBox (eventos/subs) vem com ForeColor preto fixo.
                if (co != null && co.ForeColor != Dracula.Foreground) co.ForeColor = Dracula.Foreground;
                CheckBox cb = k as CheckBox;
                if (cb != null && cb.Appearance == Appearance.Button && cb.FlatStyle != FlatStyle.Flat)
                {
                    cb.FlatStyle = FlatStyle.Flat;
                    cb.FlatAppearance.BorderColor = Dracula.CurrentLine;
                    cb.FlatAppearance.CheckedBackColor = Dracula.CurrentLine;
                    cb.FlatAppearance.MouseOverBackColor = Dracula.CurrentLine;
                    cb.BackColor = Dracula.Background;
                }
                if (k is LinkLabel)
                {
                    LinkLabel ll = (LinkLabel)k;
                    if (ll.LinkColor != Dracula.Cyan) { ll.LinkColor = Dracula.Cyan; ll.ActiveLinkColor = Dracula.Pink; ll.VisitedLinkColor = Dracula.Cyan; }
                }
                if (k is TreeView && k.IsHandleCreated && !ThumbPainter.Has(k.Handle)) new ThumbPainter(k);
                string tn = k.GetType().FullName;
                if (k.IsHandleCreated && !Overpaint.Has(k.Handle))
                {
                    if (tn.EndsWith("TLScrollBar")) new Overpaint(k, true);
                    else if (tn == "VisualHint.SmartPropertyGrid.InternalGrid") new Overpaint(k, false);
                }
                AttachThumbs(k);
            }
        }

        // Espaço entre as ferramentas da toolbar e o filtro de desenho (dropdowns e botão de personalização).
        private static void ThemeToolbarExtra()
        {
            if (toolbars == null || ReferenceEquals(toolbars, spacedManager)) return;
            spacedManager = toolbars;
            try
            {
                SetDrawFilter(toolbars);
                SpaceSettings(Get(toolbars, "ToolbarSettings"));
                SpaceSettings(Get(toolbars, "MenuSettings"));
                System.Collections.IEnumerable list = Get(toolbars, "Toolbars") as System.Collections.IEnumerable;
                if (list != null) foreach (object tb in list) SpaceSettings(Get(tb, "Settings"));
                Control host = Get(toolbars, "DockWithinContainer") as Control;
                if (host != null) host.Invalidate(true);
                Log("toolbar espaçada + drawfilter", null);
            }
            catch (Exception ex) { Log("ThemeToolbarExtra", ex); }
        }

        // Painéis encaixados (Properties, Toolbox…): a aba selecionada vinha com borda branca do Office2007.
        private static void ThemeDockManager(object dm)
        {
            try
            {
                object ps = Get(dm, "DefaultPaneSettings");
                foreach (string n in new[] { "TabAppearance", "HotTrackingTabAppearance" })
                    IgApp(Get(ps, n), Dracula.Darker, Dracula.Comment);
                foreach (string n in new[] { "SelectedTabAppearance", "ActiveTabAppearance" })
                    IgApp(Get(ps, n), Dracula.Background, Dracula.Foreground);
                foreach (string n in new[] { "SelectedTabAppearance", "ActiveTabAppearance", "TabAppearance", "HotTrackingTabAppearance" })
                    TrySet(Get(ps, n), "BorderColor", Dracula.Darker);
                IgApp(Get(dm, "SplitterBarAppearance"), Dracula.Darker, Dracula.Foreground);
                IgApp(Get(dm, "UnpinnedTabAreaAppearance"), Dracula.Darker, Dracula.Foreground);
                StyleGroup(Get(dm, "DefaultGroupSettings"));
                System.Collections.IEnumerable areas = Get(dm, "DockAreas") as System.Collections.IEnumerable;
                if (areas != null) foreach (object a in areas) StyleGroups(a, 0);
                Log("dock manager tingido", null);
            }
            catch (Exception ex) { Log("ThemeDockManager", ex); }
        }

        private static void StyleGroup(object gs)
        {
            if (gs == null) return;
            TrySetEnumByName(gs, "TabStyle", "Flat");
            IgApp(Get(gs, "TabAreaAppearance"), Dracula.Darker, Dracula.Foreground);
        }

        private static void StyleGroups(object pane, int depth)
        {
            if (pane == null || depth > 8) return;
            StyleGroup(Get(pane, "Settings") != null && Get(Get(pane, "Settings"), "TabStyle") != null ? Get(pane, "Settings") : null);
            System.Collections.IEnumerable kids = Get(pane, "Panes") as System.Collections.IEnumerable;
            if (kids != null) foreach (object k in kids) StyleGroups(k, depth + 1);
        }

        // Cabeçalho das grids: o gradiente cinza vem do tema do SO / estilo XP; plano e escuro.
        private static void StyleGridHeaders(Control k)
        {
            TrySetEnumByName(k, "UseOsThemes", "Default");
            object dl = Get(k, "DisplayLayout");
            TrySetEnumByName(dl, "HeaderStyle", "Standard");
            object ov = Get(dl, "Override");
            TrySetEnumByName(ov, "HeaderStyle", "Standard");
            IgApp(Get(ov, "HeaderAppearance"), Dracula.Darkest, Dracula.Purple);
            TrySet(Get(ov, "HeaderAppearance"), "BorderColor", Dracula.CurrentLine);
            TrySetEnumByName(ov, "BorderStyleHeader", "Solid");
            TrySetEnumByName(ov, "BorderStyleRow", "None");
            TrySetEnumByName(ov, "BorderStyleCell", "None");
            IgApp(Get(ov, "RowSelectorAppearance"), Dracula.Background, Dracula.Foreground);
            TrySetEnumByName(ov, "RowSelectors", "False");
            IgApp(Get(ov, "MaskLiteralsAppearance"), Dracula.Background, Dracula.Foreground);
            object df = Get(k, "DrawFilter");
            // Filtro de uma versão anterior do tema (recarga a quente = outro assembly): troca pelo atual,
            // senão a grid fica com o desenho antigo (a borda a cada 2 linhas "voltava").
            if (df == null || !(df is DraculaDrawFilter) && df.GetType().FullName == typeof(DraculaDrawFilter).FullName) SetDrawFilter(k);
            else if (!(df is DraculaDrawFilter)) Log("grid " + k.Name + " já tem DrawFilter " + df.GetType().FullName, null);
            TrySet(ov, "DefaultRowHeight", 26);
            System.Collections.IEnumerable bands = Get(dl, "Bands") as System.Collections.IEnumerable;
            if (bands != null)
                foreach (object b in bands)
                {
                    object bo = Get(b, "Override");
                    IgApp(Get(bo, "RowAppearance"), Dracula.Background, Dracula.Foreground);
                    IgApp(Get(bo, "RowAlternateAppearance"), Dracula.Background, Dracula.Foreground);
                    IgApp(Get(bo, "CellAppearance"), Dracula.Background, Dracula.Foreground);
                    IgApp(Get(bo, "HeaderAppearance"), Dracula.Darkest, Dracula.Purple);
                    IgApp(Get(bo, "ActiveRowAppearance"), Dracula.CurrentLine, Dracula.Foreground);
                    IgApp(Get(bo, "SelectedRowAppearance"), Dracula.CurrentLine, Dracula.Foreground);
                    System.Collections.IEnumerable cols = Get(b, "Columns") as System.Collections.IEnumerable;
                    if (cols == null) continue;
                    foreach (object c in cols)
                    {
                        IgApp(Get(Get(c, "Header"), "Appearance"), Dracula.Darkest, Dracula.Purple);
                        IgApp(Get(c, "CellAppearance"), Dracula.Background, Dracula.Foreground);
                        // AutoFit espreme colunas curtas até 1 letra (Ignored Objects: T, D, M…).
                        string cap = Get(Get(c, "Header"), "Caption") as string;
                        if (!Convert.ToBoolean(Get(c, "Hidden") ?? false) && Convert.ToInt32(Get(c, "MinWidth") ?? 0) < 40 && !string.IsNullOrEmpty(cap) && cap.Length > 2)
                            TrySet(c, "MinWidth", Math.Min(150, TextRenderer.MeasureText(cap, k.Font).Width + 16));
                        // Literais da máscara (/ e : das datas) saíam na cor do fundo.
                        if (!string.IsNullOrEmpty(Get(c, "MaskInput") as string) || Convert.ToBoolean(Get(c, "HasMaskLiteralsAppearance") ?? false))
                            IgApp(Get(c, "MaskLiteralsAppearance"), Dracula.Background, Dracula.Foreground);
                    }
                }
            k.Invalidate();
        }

        // A IDE dá às linhas uma Appearance própria (compartilhada) que o tema mapeia para a cor de seleção:
        // a grid inteira parecia selecionada (History do Team Development).
        private static void FixRowAppearances(Control k)
        {
            System.Collections.IEnumerable rows = Get(k, "Rows") as System.Collections.IEnumerable;
            if (rows == null) return;
            bool changed = false; int n = 0;
            // Linhas alternadas: a borda clara vem do RowAlternateAppearance do Override (layout e bandas),
            // que a IDE repõe ao recarregar a grid; por isso roda a cada varredura, não uma vez por grid.
            object lay = Get(k, "DisplayLayout");
            System.Collections.Generic.List<object> ovs = new System.Collections.Generic.List<object>();
            if (lay != null) ovs.Add(Get(lay, "Override"));
            System.Collections.IEnumerable bands = lay == null ? null : Get(lay, "Bands") as System.Collections.IEnumerable;
            if (bands != null) foreach (object b in bands) ovs.Add(Get(b, "Override"));
            foreach (object ov in ovs)
            {
                if (ov == null) continue;
                foreach (string p in new[] { "RowAppearance", "RowAlternateAppearance", "CellAppearance" })
                {
                    if (!Convert.ToBoolean(Get(ov, "Has" + p) ?? false)) continue;
                    object a = Get(ov, p);
                    object brc = Get(a, "BorderColor");
                    if (brc is Color && !((Color)brc).IsEmpty && ((Color)brc).ToArgb() != Dracula.Darker.ToArgb()) { TrySet(a, "BorderColor", Dracula.Darker); changed = true; }
                    object fc = Get(a, "ForeColor");
                    if (fc is Color && !((Color)fc).IsEmpty && Dracula.IsDark((Color)fc)) { TrySet(a, "ForeColor", Dracula.Foreground); changed = true; }
                }
                object bs = Get(ov, "BorderStyleRow");
                if (bs != null && bs.ToString() != "None") { TrySetEnumByName(ov, "BorderStyleRow", "None"); changed = true; }
            }
            foreach (object r in rows)
            {
                if (++n > 2000) break;
                if (Convert.ToBoolean(Get(r, "Selected") ?? false)) continue;
                foreach (string p in new[] { "Appearance", "CellAppearance" })
                {
                    if (!Convert.ToBoolean(Get(r, "Has" + p) ?? false)) continue;
                    object a = Get(r, p);
                    // Borda prateada (#C0C0C0) e texto escuro que a IDE põe em linhas alternadas/ignoradas.
                    object brc = Get(a, "BorderColor");
                    if (brc is Color && !((Color)brc).IsEmpty && ((Color)brc).ToArgb() != Dracula.Darker.ToArgb()) { TrySet(a, "BorderColor", Dracula.Darker); changed = true; }
                    object fc = Get(a, "ForeColor");
                    if (fc is Color && !((Color)fc).IsEmpty && Dracula.IsDark((Color)fc)) { TrySet(a, "ForeColor", Dracula.Foreground); changed = true; }
                    object bc = Get(a, "BackColor");
                    if (bc is Color && ((Color)bc).ToArgb() == Color.FromArgb(0x44, 0x47, 0x5A).ToArgb())
                    {
                        TrySet(a, "BackColor", Dracula.Background); TrySet(a, "BackColor2", Dracula.Background); changed = true;
                    }
                }
            }
            if (changed) k.Invalidate();
        }

        private static readonly System.Collections.Generic.HashSet<int> combosFixed = new System.Collections.Generic.HashSet<int>();

        // O preto da lista vinha do ForeColor fixo da IDE, não do tema: sem owner-draw (piscava no hover e
        // os handlers de versões antigas do tema, recarregadas a quente, continuavam pintando por cima).
        private static void ResetCombo(ComboBox co)
        {
            try
            {
                FieldInfo key = typeof(ComboBox).GetField("EVENT_DRAWITEM", BindingFlags.NonPublic | BindingFlags.Static);
                PropertyInfo evp = typeof(System.ComponentModel.Component).GetProperty("Events", BindingFlags.NonPublic | BindingFlags.Instance);
                if (key != null && evp != null)
                {
                    System.ComponentModel.EventHandlerList ev = (System.ComponentModel.EventHandlerList)evp.GetValue(co, null);
                    object k = key.GetValue(null);
                    Delegate d = ev[k];
                    if (d != null && d.Method.DeclaringType != null && d.Method.DeclaringType.Assembly.GetName().Name.StartsWith("GxDracula")) ev.RemoveHandler(k, d);
                    else if (d != null)
                        foreach (Delegate one in d.GetInvocationList())
                            if (one.Method.DeclaringType != null && one.Method.DeclaringType.Assembly.GetName().Name.StartsWith("GxDracula")) ev.RemoveHandler(k, one);
                    if (ev[k] == null) DarkComboText(co);
                }
            }
            catch (Exception ex) { Log("ResetCombo", ex); }
            co.ForeColor = Dracula.Foreground;
            co.BackColor = Dracula.Background;
            if (co.GetType().Name == "FontDropDownList") { if (co.IsHandleCreated) OwnerComboPainter.Attach(co); else co.HandleCreated += delegate { OwnerComboPainter.Attach(co); }; }
            // Sem tema visual: o DarkMode_CFD redesenha o botão em hot-track ao passar o mouse (pisca contra a seta
            // do BorderPainter) e pinta o texto do edit com a cor do tema (preto).
            UnthemeCombo(co);
            co.HandleCreated += delegate { UnthemeCombo(co); };
            co.ForeColorChanged += delegate
            {
                if (co.ForeColor == Dracula.Foreground) return;
                if (foreLog++ < 2) Log("combo " + co.Name + " ForeColor trocado para " + co.ForeColor + " por: " + Environment.StackTrace.Replace(Environment.NewLine, " | ").Substring(0, 600), null);
                co.ForeColor = Dracula.Foreground;
            };
            co.Invalidate();
        }

        private static int foreLog;

        // FontDropDownList do Actipro (Options > Output) sobrescreve OnDrawItem com fundo branco, sem chamar o evento:
        // intercepta o WM_DRAWITEM refletido e desenha cada fonte no próprio estilo, em fundo escuro.
        private sealed class OwnerComboPainter : NativeWindow
        {
            private static readonly System.Collections.Generic.HashSet<IntPtr> done = new System.Collections.Generic.HashSet<IntPtr>();
            private readonly ComboBox co;
            public static void Attach(ComboBox c) { if (c.IsHandleCreated && done.Add(c.Handle)) new OwnerComboPainter(c); }
            private OwnerComboPainter(ComboBox c) { co = c; AssignHandle(c.Handle); c.HandleDestroyed += delegate { done.Remove(co.Handle); ReleaseHandle(); }; c.Invalidate(); }

            [StructLayout(LayoutKind.Sequential)]
            private struct DRAWITEMSTRUCT { public int CtlType, CtlID, itemID, itemAction, itemState; public IntPtr hwndItem, hDC; public int l, t, r, b; public IntPtr itemData; }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg != 0x202B) { base.WndProc(ref m); return; }
                DRAWITEMSTRUCT d = (DRAWITEMSTRUCT)Marshal.PtrToStructure(m.LParam, typeof(DRAWITEMSTRUCT));
                Rectangle r = Rectangle.FromLTRB(d.l, d.t, d.r, d.b);
                bool sel = (d.itemState & 0x1) != 0 && (d.itemState & 0x1000) == 0;
                using (Graphics g = Graphics.FromHdc(d.hDC))
                {
                    using (SolidBrush bg = new SolidBrush(sel ? Dracula.CurrentLine : Dracula.Background)) g.FillRectangle(bg, r);
                    if (sel) using (SolidBrush bar = new SolidBrush(Dracula.Purple)) g.FillRectangle(bar, r.X, r.Y, 2, r.Height);
                    if (d.itemID >= 0 && d.itemID < co.Items.Count)
                    {
                        string s = co.GetItemText(co.Items[d.itemID]);
                        Font f = null;
                        try { f = new Font(s, co.Font.Size); } catch { }
                        TextRenderer.DrawText(g, s, f ?? co.Font, new Rectangle(r.X + 6, r.Y, r.Width - 6, r.Height), Dracula.Foreground,
                            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                        if (f != null) f.Dispose();
                    }
                }
                m.Result = (IntPtr)1;
            }
        }

        private static void UnthemeCombo(ComboBox co)
        {
            if (!co.IsHandleCreated) return;
            SetWindowTheme(co.Handle, "", "");
            COMBOBOXINFO i = new COMBOBOXINFO(); i.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
            if (GetComboBoxInfo(co.Handle, ref i))
            {
                if (i.hwndItem != IntPtr.Zero) SetWindowTheme(i.hwndItem, "", "");
                if (i.hwndList != IntPtr.Zero) SetWindowTheme(i.hwndList, "DarkMode_Explorer", null);
            }
        }

        [DllImport("user32.dll")] private static extern bool GetComboBoxInfo(IntPtr h, ref COMBOBOXINFO i);
        [StructLayout(LayoutKind.Sequential)]
        private struct COMBOBOXINFO { public int cbSize; public int l1, t1, r1, b1, l2, t2, r2, b2; public int state; public IntPtr hwndCombo, hwndItem, hwndList; }

        // "combo open|close|hover N": abre o combo visível de eventos e loga o hwnd da lista (para PrintWindow).
        private static void ComboCommand(string arg)
        {
            ComboBox co = null;
            foreach (Form f in Application.OpenForms) { co = FindCombo(f); if (co != null) break; }
            if (co == null) { Log("combo: não achou", null); return; }
            if (arg == "open") co.DroppedDown = true;
            else if (arg == "close") co.DroppedDown = false;
            COMBOBOXINFO i = new COMBOBOXINFO(); i.cbSize = Marshal.SizeOf(typeof(COMBOBOXINFO));
            GetComboBoxInfo(co.Handle, ref i);
            Log("combo h=" + co.Handle.ToInt64() + " edit=" + i.hwndItem.ToInt64() + " w=" + co.Width + " ht=" + co.Height + " list=" + i.hwndList.ToInt64() + " drawmode=" + co.DrawMode + " fore=" + co.ForeColor + " style=" + co.DropDownStyle, null);
        }

        private static ComboBox FindCombo(Control c)
        {
            foreach (Control k in c.Controls)
            {
                if (k is ComboBox && k.Name == "headerComboBox" && k.Visible) return (ComboBox)k;
                ComboBox r = FindCombo(k); if (r != null) return r;
            }
            return null;
        }

        // Combo DropDownList com DarkMode_CFD escreve o item selecionado em preto: desenha os itens.
        private static void DarkComboText(ComboBox co)
        {
            co.DrawMode = DrawMode.OwnerDrawFixed;
            co.ItemHeight = Math.Max(co.ItemHeight, co.Font.Height + 6);
            co.DrawItem += delegate(object o, DrawItemEventArgs e)
            {
                ComboBox cbx = (ComboBox)o;
                // Uma única pintura por item, sempre completa (fundo + texto): sem isso o hover piscava.
                bool sel = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
                using (SolidBrush bg = new SolidBrush(sel ? Color.FromArgb(0x44, 0x47, 0x5A) : Dracula.Background)) e.Graphics.FillRectangle(bg, e.Bounds);
                if (sel) using (SolidBrush bar = new SolidBrush(Dracula.Purple)) e.Graphics.FillRectangle(bar, e.Bounds.X, e.Bounds.Y, 2, e.Bounds.Height);
                if (e.Index < 0) return;
                TextRenderer.DrawText(e.Graphics, cbx.GetItemText(cbx.Items[e.Index]), cbx.Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height), Dracula.Foreground,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            };
            co.Invalidate();
        }

        private static void SpaceSettings(object s)
        {
            if (s == null) return;
            TrySet(s, "ToolSpacing", 6);
            TrySet(s, "PaddingLeft", 4);
            TrySet(s, "PaddingRight", 4);
            TrySet(s, "PaddingTop", 2);
            TrySet(s, "PaddingBottom", 2);
        }

        // TreeList (Variables): o cabeçalho e os checkboxes vêm do skin e ignoram Appearance → custom draw.
        private static readonly System.Collections.Generic.HashSet<int> hookedLists = new System.Collections.Generic.HashSet<int>();

        private static void HookTreeListDraw(Control t)
        {
            if (t.GetType().GetEvent("CustomDrawColumnHeader") == null || !hookedLists.Add(t.GetHashCode())) return;
            Hook(t, "CustomDrawColumnHeader", "OnDxHeader");
            Hook(t, "CustomDrawNodeCell", "OnDxCell");
            Hook(t, "CustomDrawNodeIndent", "OnDxIndent");
            Hook(t, "CustomDrawEmptyArea", "OnDxEmpty");
        }

        private static void Hook(object target, string evt, string method)
        {
            try
            {
                EventInfo ei = target.GetType().GetEvent(evt);
                if (ei == null) { Log("treelist sem evento " + evt, null); return; }
                MethodInfo mi = typeof(Theme).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
                ei.AddEventHandler(target, Delegate.CreateDelegate(ei.EventHandlerType, mi));
            }
            catch (Exception ex) { Log("hook " + evt, ex); }
        }

        private static void OnDxHeader(object sender, EventArgs e)
        {
            Graphics g = Get(e, "Graphics") as Graphics;
            object b = Get(e, "Bounds");
            if (g == null || !(b is Rectangle)) return;
            Rectangle r = (Rectangle)b;
            using (SolidBrush bg = new SolidBrush(Dracula.Darkest)) g.FillRectangle(bg, r);
            using (Pen p = new Pen(Dracula.CurrentLine))
            {
                g.DrawLine(p, r.Right - 1, r.Top + 3, r.Right - 1, r.Bottom - 4);
                g.DrawLine(p, r.Left, r.Bottom - 1, r.Right, r.Bottom - 1);
            }
            string cap = Get(e, "Caption") as string;
            if (!string.IsNullOrEmpty(cap))
                TextRenderer.DrawText(g, cap, ((Control)sender).Font, new Rectangle(r.X + 6, r.Y, r.Width - 8, r.Height),
                    Dracula.Purple, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TrySet(e, "Handled", true);
        }

        private static void OnDxEmpty(object sender, EventArgs e)
        {
            Graphics g = Get(e, "Graphics") as Graphics;
            object b = Get(e, "Bounds");
            if (g == null || !(b is Rectangle)) return;
            using (SolidBrush bg = new SolidBrush(Dracula.Background)) g.FillRectangle(bg, (Rectangle)b);
            TrySet(e, "Handled", true);
        }

        // Faixa de recuo da árvore: a IDE a pinta com cor própria (coluna Name mais clara).
        private static void OnDxIndent(object sender, EventArgs e)
        {
            Graphics g = Get(e, "Graphics") as Graphics;
            object b = Get(e, "Bounds");
            if (g == null || !(b is Rectangle)) return;
            using (SolidBrush bg = new SolidBrush(Dracula.Background)) g.FillRectangle(bg, (Rectangle)b);
        }

        // Coluna "Is Collection": o editor de checkbox desenha o quadrado branco do sistema.
        private static void OnDxCell(object sender, EventArgs e)
        {
            Graphics g = Get(e, "Graphics") as Graphics;
            object b = Get(e, "Bounds");
            object v = Get(e, "CellValue");
            object ap = Get(e, "Appearance");
            // A IDE dá à coluna Name um fundo próprio (mais claro); só a linha focada destaca.
            Color back = ReferenceEquals(Get(e, "Node"), Get(sender, "FocusedNode")) ? Dracula.CurrentLine : Dracula.Background;
            TrySet(ap, "BackColor", back); TrySet(ap, "BackColor2", back);
            if (g == null || !(b is Rectangle) || !(v is bool)) return;
            using (SolidBrush bg = new SolidBrush(back)) g.FillRectangle(bg, (Rectangle)b);
            DrawCheck(g, (Rectangle)b, (bool)v);
            TrySet(e, "Handled", true);
        }

        internal static void DrawCheck(Graphics g, Rectangle r, bool on)
        {
            int s = 13;
            Rectangle box = new Rectangle(r.X + (r.Width - s) / 2, r.Y + (r.Height - s) / 2, s - 1, s - 1);
            using (SolidBrush bg = new SolidBrush(on ? Dracula.Purple : Dracula.Background)) g.FillRectangle(bg, box);
            using (Pen p = new Pen(on ? Dracula.Purple : Dracula.Comment)) g.DrawRectangle(p, box);
            if (!on) return;
            System.Drawing.Drawing2D.SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Dracula.Background, 2f))
                g.DrawLines(p, new[] { new Point(box.X + 3, box.Y + 6), new Point(box.X + 5, box.Y + 9), new Point(box.X + 10, box.Y + 3) });
            g.SmoothingMode = old;
        }

        // Scrollbar da DevExpress 7.3 (Variables) ignora o LookAndFeel e sai clara: desenha trilho, setas e thumb por cima.
        // Grid de Properties: só a moldura (1 px clara na área não-cliente).
        private sealed class Overpaint : NativeWindow
        {
            [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr h);
            [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
            private static readonly System.Collections.Generic.HashSet<IntPtr> done = new System.Collections.Generic.HashSet<IntPtr>();
            public static bool Has(IntPtr h) { return done.Contains(h); }
            private readonly Control c; private readonly bool scroll;

            public Overpaint(Control c, bool scroll)
            {
                this.c = c; this.scroll = scroll;
                done.Add(c.Handle); AssignHandle(c.Handle);
                c.HandleDestroyed += delegate { done.Remove(Handle); ReleaseHandle(); };
                c.Invalidate();
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                int msg = m.Msg;
                if (!running || !(msg == 0x000F || msg == 0x0085 || msg == 0x0200 || msg == 0x0202 || msg == 0x0201 || msg == 0x02A3)) return;
                try { if (scroll) PaintScroll(); else PaintFrame(); } catch { }
            }

            private void PaintFrame()
            {
                IntPtr dc = GetWindowDC(c.Handle);
                try
                {
                    using (Graphics g = Graphics.FromHdc(dc))
                    using (Pen p = new Pen(Dracula.Darker))
                    {
                        int w = c.Width, h = c.Height;
                        g.DrawRectangle(p, 0, 0, w - 1, h - 1);
                        g.DrawRectangle(p, 1, 1, w - 3, h - 3);
                    }
                }
                finally { ReleaseDC(c.Handle, dc); }
            }

            private void PaintScroll()
            {
                int min = Convert.ToInt32(Get(c, "Minimum") ?? 0), max = Convert.ToInt32(Get(c, "Maximum") ?? 0);
                int large = Math.Max(1, Convert.ToInt32(Get(c, "LargeChange") ?? 1)), val = Convert.ToInt32(Get(c, "Value") ?? 0);
                bool vert = c.Height >= c.Width;
                int len = vert ? c.Height : c.Width, btn = vert ? c.Width : c.Height;
                using (Graphics g = c.CreateGraphics())
                using (SolidBrush track = new SolidBrush(Dracula.Darker))
                using (SolidBrush thumb = new SolidBrush(Color.FromArgb(0x6A, 0x6E, 0x8A)))
                using (Pen arrow = new Pen(Dracula.Comment, 1.6f))
                {
                    g.FillRectangle(track, 0, 0, c.Width, c.Height);
                    int range = max - min + 1, area = len - 2 * btn;
                    if (range > large && area > 10)
                    {
                        int tl = Math.Max(16, area * large / range);
                        int tp = btn + (int)((long)(area - tl) * (val - min) / Math.Max(1, range - large));
                        Rectangle r = vert ? new Rectangle(4, tp, c.Width - 8, tl) : new Rectangle(tp, 4, tl, c.Height - 8);
                        g.FillRectangle(thumb, r);
                    }
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    int m = btn / 2;
                    if (vert)
                    {
                        g.DrawLines(arrow, new[] { new Point(m - 4, m + 2), new Point(m, m - 2), new Point(m + 4, m + 2) });
                        int y = len - m;
                        g.DrawLines(arrow, new[] { new Point(m - 4, y - 2), new Point(m, y + 2), new Point(m + 4, y - 2) });
                    }
                    else
                    {
                        g.DrawLines(arrow, new[] { new Point(m + 2, m - 4), new Point(m - 2, m), new Point(m + 2, m + 4) });
                        int x = len - m;
                        g.DrawLines(arrow, new[] { new Point(x - 2, m - 4), new Point(x + 2, m), new Point(x - 2, m + 4) });
                    }
                }
            }
        }

        // Thumb do scroll nativo no DarkMode_Explorer some contra o trilho: repinta o thumb (área não-cliente) por cima.
        private sealed class ThumbPainter : NativeWindow
        {
            [StructLayout(LayoutKind.Sequential)]
            private struct RECT { public int Left, Top, Right, Bottom; }
            [StructLayout(LayoutKind.Sequential)]
            private struct SCROLLBARINFO
            {
                public int cbSize; public RECT rcScrollBar; public int dxyLineButton, xyThumbTop, xyThumbBottom, reserved;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public int[] rgstate;
            }
            [DllImport("user32.dll")] private static extern bool GetScrollBarInfo(IntPtr h, int idObject, ref SCROLLBARINFO i);
            [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr h, out RECT r);
            [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr h);
            [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);

            private static readonly System.Collections.Generic.HashSet<IntPtr> done = new System.Collections.Generic.HashSet<IntPtr>();
            private readonly Control c;
            public static bool Has(IntPtr h) { return done.Contains(h); }

            public ThumbPainter(Control c)
            {
                this.c = c;
                if (c.IsHandleCreated) Attach();
                c.HandleCreated += delegate { Attach(); };
                c.HandleDestroyed += delegate { done.Remove(Handle); ReleaseHandle(); };
            }

            private void Attach()
            {
                if (Handle != IntPtr.Zero || !done.Add(c.Handle)) return;
                AssignHandle(c.Handle);
            }

            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                // WM_PAINT, WM_NCPAINT, WM_VSCROLL, WM_HSCROLL, WM_MOUSEWHEEL, WM_MOUSEMOVE, WM_NCMOUSEMOVE
                int msg = m.Msg;
                if (!running || !(msg == 0x000F || msg == 0x0085 || msg == 0x0115 || msg == 0x0114 || msg == 0x020A || msg == 0x00A0 || msg == 0x0200)) return;
                try { Paint(); } catch { }
            }

            private void Paint()
            {
                RECT w; GetWindowRect(c.Handle, out w);
                IntPtr dc = GetWindowDC(c.Handle);
                try
                {
                    using (Graphics g = Graphics.FromHdc(dc))
                    {
                        PaintBar(g, w, -5, true);   // OBJID_VSCROLL
                        PaintBar(g, w, -6, false);  // OBJID_HSCROLL
                    }
                }
                finally { ReleaseDC(c.Handle, dc); }
            }

            private void PaintBar(Graphics g, RECT w, int id, bool vertical)
            {
                SCROLLBARINFO i = new SCROLLBARINFO();
                i.cbSize = Marshal.SizeOf(typeof(SCROLLBARINFO));
                if (!GetScrollBarInfo(c.Handle, id, ref i)) return;
                if ((i.rgstate[0] & 0x8000) != 0 || (i.rgstate[0] & 0x0001) != 0) return; // invisível / indisponível
                Rectangle bar = Rectangle.FromLTRB(i.rcScrollBar.Left - w.Left, i.rcScrollBar.Top - w.Top,
                    i.rcScrollBar.Right - w.Left, i.rcScrollBar.Bottom - w.Top);
                if (bar.Width <= 0 || bar.Height <= 0 || i.xyThumbBottom <= i.xyThumbTop) return;
                Rectangle thumb = vertical
                    ? new Rectangle(bar.Left + 3, bar.Top + i.xyThumbTop + 1, bar.Width - 6, i.xyThumbBottom - i.xyThumbTop - 2)
                    : new Rectangle(bar.Left + i.xyThumbTop + 1, bar.Top + 3, i.xyThumbBottom - i.xyThumbTop - 2, bar.Height - 6);
                // Trilho bem escuro + thumb claro: o contraste do DarkMode_Explorer era quase nenhum.
                int a = i.dxyLineButton;
                Rectangle trackR = vertical ? new Rectangle(bar.Left, bar.Top + a, bar.Width, bar.Height - 2 * a)
                                            : new Rectangle(bar.Left + a, bar.Top, bar.Width - 2 * a, bar.Height);
                using (SolidBrush tb = new SolidBrush(Dracula.Darkest)) g.FillRectangle(tb, trackR);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(0x9A, 0x9E, 0xB8))) g.FillRectangle(b, thumb);
            }
        }
    }
}
