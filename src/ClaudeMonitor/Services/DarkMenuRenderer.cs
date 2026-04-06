using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ClaudeMonitor.Services;

public sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color BgColor = Color.FromArgb(240, 26, 26, 46);       // #1a1a2e
    private static readonly Color ItemHover = Color.FromArgb(255, 37, 37, 69);      // #252545
    private static readonly Color TextColor = Color.FromArgb(255, 200, 200, 210);   // light gray
    private static readonly Color AccentColor = Color.FromArgb(255, 192, 132, 252); // #c084fc
    private static readonly Color SepColor = Color.FromArgb(255, 50, 50, 70);       // separator
    private static readonly Color CheckColor = Color.FromArgb(255, 74, 222, 128);   // #4ade80
    private static readonly Color DisabledColor = Color.FromArgb(255, 100, 100, 110);

    public DarkMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(BgColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(SepColor);
        var r = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var rc = new Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
        if (e.Item.Selected && e.Item.Enabled)
        {
            using var brush = new SolidBrush(ItemHover);
            using var path = RoundedRect(rc, 6);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? TextColor : DisabledColor;
        e.TextFont = new Font("Segoe UI", 9.5f);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(SepColor);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // No image margin background
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal class DarkColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => Color.FromArgb(255, 50, 50, 70);
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => Color.FromArgb(255, 37, 37, 69);
    public override Color MenuStripGradientBegin => Color.FromArgb(240, 26, 26, 46);
    public override Color MenuStripGradientEnd => Color.FromArgb(240, 26, 26, 46);
    public override Color MenuItemSelectedGradientBegin => Color.FromArgb(255, 37, 37, 69);
    public override Color MenuItemSelectedGradientEnd => Color.FromArgb(255, 37, 37, 69);
    public override Color MenuItemPressedGradientBegin => Color.FromArgb(255, 45, 45, 80);
    public override Color MenuItemPressedGradientEnd => Color.FromArgb(255, 45, 45, 80);
    public override Color ImageMarginGradientBegin => Color.FromArgb(240, 26, 26, 46);
    public override Color ImageMarginGradientMiddle => Color.FromArgb(240, 26, 26, 46);
    public override Color ImageMarginGradientEnd => Color.FromArgb(240, 26, 26, 46);
    public override Color CheckBackground => Color.Transparent;
    public override Color CheckSelectedBackground => Color.Transparent;
    public override Color CheckPressedBackground => Color.Transparent;
    public override Color ToolStripDropDownBackground => Color.FromArgb(240, 26, 26, 46);
    public override Color SeparatorDark => Color.FromArgb(255, 50, 50, 70);
    public override Color SeparatorLight => Color.Transparent;
}
