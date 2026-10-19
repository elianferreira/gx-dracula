using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using Infragistics.Win;

namespace GxDracula
{
    // Pinta no lugar do Infragistics os botões de dropdown (combos da toolbar, UltraComboEditor) e o botão
    // de personalização no fim de cada toolbar: o Office2007 os desenha cinza-claro, mesmo no esquema Black.
    public sealed class DraculaDrawFilter : IUIElementDrawFilter
    {
        public static readonly DraculaDrawFilter Instance = new DraculaDrawFilter();
        public static int BorderCalls, PhaseCalls;

        private static bool Ours(UIElement e)
        {
            string n = e.GetType().Name;
            if (n.EndsWith("DropDownButtonUIElement") || n.StartsWith("QuickCustomize")) return true;
            // Botão de editor genérico só quando é DropDownEditorButton (combo Version da History); os de imagem ficam.
            if (n == "EditorButtonUIElement")
            {
                PropertyInfo pi = e.GetType().GetProperty("Button");
                object b = pi == null ? null : pi.GetValue(e, null);
                if (b == null) return false;
                if (b.GetType().Name.Contains("DropDown")) return true;
                // Botão sem imagem nem texto = só a seta do sistema.
                PropertyInfo ap = b.GetType().GetProperty("Appearance"), tx = b.GetType().GetProperty("Text");
                object a = ap == null ? null : ap.GetValue(b, null);
                PropertyInfo im = a == null ? null : a.GetType().GetProperty("Image");
                return (im == null || im.GetValue(a, null) == null) && (tx == null || string.IsNullOrEmpty(tx.GetValue(b, null) as string));
            }
            return false;
        }

        // Literal da máscara (/ e : das datas nas grids): o Infragistics o pinta na cor do fundo no esquema escuro.
        private static char Literal(UIElement e)
        {
            Infragistics.Win.UltraWinMaskedEdit.DisplayCharUIElement d = e as Infragistics.Win.UltraWinMaskedEdit.DisplayCharUIElement;
            return d != null && d.DisplayChar != null && !d.DisplayChar.IsEditable ? d.DisplayChar.Char : '\0';
        }

        public DrawPhase GetPhasesToFilter(ref UIElementDrawParams p)
        {
            PhaseCalls++;
            if (Literal(p.Element) != '\0') return DrawPhase.BeforeDrawForeground;
            // Borda clara a cada 2 linhas nas grids do Team Development: some sem depender de qual Appearance a traz.
            if (RowBorder(p.Element)) return DrawPhase.BeforeDrawBorders;
            return Ours(p.Element) ? DrawPhase.BeforeDrawElement : DrawPhase.None;
        }

        private static bool RowBorder(UIElement e)
        {
            string n = e.GetType().Name;
            return n == "RowUIElement" || n == "RowCellAreaUIElement" || n == "RowAutoPreviewUIElement" || n == "RowColRegionIntersectionUIElement"
                // Borda clara a cada 2 linhas na Update: vem da célula (BorderStyleCell da linha alternada), não da linha.
                || n == "CellUIElement" || n == "EditorWithTextDisplayTextUIElement";
        }

        public bool DrawElement(DrawPhase phase, ref UIElementDrawParams p)
        {
            if (phase == DrawPhase.BeforeDrawBorders) { BorderCalls++; return true; }
            if (phase == DrawPhase.BeforeDrawForeground)
            {
                char ch = Literal(p.Element);
                System.Windows.Forms.TextRenderer.DrawText(p.Graphics, ch.ToString(), p.Font ?? System.Windows.Forms.Control.DefaultFont, p.Element.Rect,
                    Dracula.Foreground, System.Windows.Forms.TextFormatFlags.HorizontalCenter | System.Windows.Forms.TextFormatFlags.VerticalCenter | System.Windows.Forms.TextFormatFlags.NoPadding | System.Windows.Forms.TextFormatFlags.NoPrefix);
                return true;
            }
            Rectangle r = p.Element.Rect;
            Graphics g = p.Graphics;
            bool hot = p.Element.Rect.Contains(p.Element.Control == null ? Point.Empty
                : p.Element.Control.PointToClient(System.Windows.Forms.Control.MousePosition));
            using (SolidBrush b = new SolidBrush(hot ? Dracula.CurrentLine : Dracula.Background)) g.FillRectangle(b, r);

            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = r.Left + r.Width / 2, cy = r.Top + r.Height / 2;
            using (Pen chev = new Pen(hot ? Dracula.Pink : Dracula.Purple, 1.6f))
            {
                if (p.Element.GetType().Name.StartsWith("QuickCustomize"))
                {
                    // "Mais opções" da toolbar: traço + chevron, como o overflow do VS Code.
                    g.DrawLine(chev, cx - 3, cy - 4, cx + 3, cy - 4);
                    g.DrawLines(chev, new[] { new Point(cx - 3, cy), new Point(cx, cy + 3), new Point(cx + 3, cy) });
                }
                else
                    g.DrawLines(chev, new[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
            }
            g.SmoothingMode = old;
            return true;
        }
    }

    // Grid com DrawFilter próprio da IDE (Select Objects, Team Development): o nosso fica por fora — some com a borda
    // clara das linhas e repassa o resto ao filtro original.
    public sealed class ChainDrawFilter : IUIElementDrawFilter
    {
        public readonly IUIElementDrawFilter Inner;
        public ChainDrawFilter(IUIElementDrawFilter inner) { Inner = inner; }

        public DrawPhase GetPhasesToFilter(ref UIElementDrawParams p)
        {
            DrawPhase mine = DraculaDrawFilter.Instance.GetPhasesToFilter(ref p);
            return mine | (Inner == null ? DrawPhase.None : Inner.GetPhasesToFilter(ref p));
        }

        public bool DrawElement(DrawPhase phase, ref UIElementDrawParams p)
        {
            if (phase == DrawPhase.BeforeDrawBorders && DraculaDrawFilter.Instance.GetPhasesToFilter(ref p) == DrawPhase.BeforeDrawBorders)
                return DraculaDrawFilter.Instance.DrawElement(phase, ref p);
            if (Inner != null && (Inner.GetPhasesToFilter(ref p) & phase) != 0) return Inner.DrawElement(phase, ref p);
            return (DraculaDrawFilter.Instance.GetPhasesToFilter(ref p) & phase) != 0 && DraculaDrawFilter.Instance.DrawElement(phase, ref p);
        }
    }

    public static partial class Theme
    {
        // Garante o filtro da versão atual do tema na grid, encadeando o da IDE; desembrulha filtros de versões antigas
        // (recarga a quente = outro assembly) pelo campo Inner.
        private static void EnsureGridFilter(object grid)
        {
            object df = Get(grid, "DrawFilter");
            if (df is DraculaDrawFilter || df is ChainDrawFilter) return;
            while (df != null && df.GetType().FullName.StartsWith("GxDracula."))
                df = df.GetType().GetField("Inner") == null ? null : df.GetType().GetField("Inner").GetValue(df);
            try { TrySet(grid, "DrawFilter", df == null ? (object)DraculaDrawFilter.Instance : new ChainDrawFilter((IUIElementDrawFilter)df)); }
            catch (Exception ex) { Log("chainfilter", ex); }
        }

        // Isolado num método próprio: só toca nos tipos do Infragistics quando há um componente Infragistics.
        private static void SetDrawFilter(object target)
        {
            try { TrySet(target, "DrawFilter", DraculaDrawFilter.Instance); }
            catch (Exception ex) { Log("drawfilter", ex); }
        }
    }
}
