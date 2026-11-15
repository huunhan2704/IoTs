namespace Iot04.WinForms;

public static class StyleHelper
{
    public static readonly Color ColorBg = Color.FromArgb(248, 248, 248);
    public static readonly Color ColorPanel = Color.White;
    public static readonly Color ColorBorder = Color.FromArgb(215, 215, 215);
    public static readonly Color ColorHeaderBg = Color.FromArgb(235, 235, 235);
    public static readonly Color ColorTextPrimary = Color.FromArgb(20, 20, 20);
    public static readonly Color ColorTextMuted = Color.FromArgb(100, 100, 100);

    public static void ApplyMonochromeGrid(DataGridView dgv)
    {
        dgv.EnableHeadersVisualStyles = false;
        dgv.ColumnHeadersDefaultCellStyle.BackColor = ColorHeaderBg;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = ColorTextPrimary;
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.25F, FontStyle.Bold);
        dgv.ColumnHeadersHeight = 34;

        dgv.DefaultCellStyle.BackColor = Color.White;
        dgv.DefaultCellStyle.ForeColor = ColorTextPrimary;
        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 220, 220);
        dgv.DefaultCellStyle.SelectionForeColor = Color.Black;
        dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
        dgv.GridColor = ColorBorder;
        dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        dgv.BackgroundColor = Color.White;
        dgv.BorderStyle = BorderStyle.None;
        dgv.AllowUserToAddRows = false;
        dgv.AllowUserToDeleteRows = false;
        dgv.ReadOnly = true;
        dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgv.RowHeadersVisible = false;
        dgv.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgv.RowTemplate.Height = 30;
    }

    public static Button CreateButton(string text, int width = 160, int height = 32)
    {
        return new Button
        {
            Text = text,
            Size = new Size(width, height),
            BackColor = Color.White,
            ForeColor = ColorTextPrimary,
            FlatStyle = FlatStyle.Standard,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
    }
}
