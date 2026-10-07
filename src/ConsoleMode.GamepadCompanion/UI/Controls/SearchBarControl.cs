using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Barra de pesquisa estilizada para a biblioteca de jogos no estilo Couch Gaming.
    /// Suporta Live Search, botão de limpeza [✕], atalho [X] e destaque visual de foco.
    /// </summary>
    internal sealed class SearchBarControl : UserControl
    {
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static readonly Color BgColor = Color.FromArgb(20, 22, 28);
        private static readonly Color BorderNormal = Color.FromArgb(52, 56, 66);
        private static readonly Color BorderFocused = Color.FromArgb(120, 190, 255);
        private static readonly Color TextColor = Color.FromArgb(230, 232, 240);
        private static readonly Color IconColor = Color.FromArgb(160, 170, 185);

        private readonly TextBox _txtSearch = new TextBox();
        private readonly Button _btnClear = new Button();
        private bool _isSearchFocused;

        public event Action<string> SearchTextChanged;
        public event Action ClearRequested;

        public SearchBarControl()
        {
            BackColor = BgColor;
            Height = 36;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            BuildControls();
        }

        public string SearchText
        {
            get => _txtSearch.Text;
            set => _txtSearch.Text = value ?? string.Empty;
        }

        public bool IsSearchFocused => _isSearchFocused || _txtSearch.Focused;

        private void BuildControls()
        {
            _txtSearch.BorderStyle = BorderStyle.None;
            _txtSearch.BackColor = BgColor;
            _txtSearch.ForeColor = TextColor;
            _txtSearch.Font = new Font("Segoe UI", 10f);
            _txtSearch.Location = new Point(38, 8);
            _txtSearch.Width = Width - 76;
            _txtSearch.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;

            _txtSearch.GotFocus += (s, e) =>
            {
                _isSearchFocused = true;
                Invalidate();
            };

            _txtSearch.LostFocus += (s, e) =>
            {
                _isSearchFocused = false;
                Invalidate();
            };

            _txtSearch.TextChanged += (s, e) =>
            {
                _btnClear.Visible = !string.IsNullOrEmpty(_txtSearch.Text);
                SearchTextChanged?.Invoke(_txtSearch.Text);
            };

            _btnClear.Text = "✕";
            _btnClear.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _btnClear.ForeColor = IconColor;
            _btnClear.BackColor = Color.Transparent;
            _btnClear.FlatStyle = FlatStyle.Flat;
            _btnClear.FlatAppearance.BorderSize = 0;
            _btnClear.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 44, 56);
            _btnClear.Size = new Size(26, 26);
            _btnClear.Location = new Point(Width - 32, 5);
            _btnClear.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _btnClear.Cursor = Cursors.Hand;
            _btnClear.Visible = false;

            _btnClear.Click += (s, e) =>
            {
                ClearSearch();
            };

            Controls.Add(_txtSearch);
            Controls.Add(_btnClear);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateCueBanner();
        }

        private void UpdateCueBanner()
        {
            if (_txtSearch.IsHandleCreated)
            {
                SendMessage(_txtSearch.Handle, EM_SETCUEBANNER, (IntPtr)1, Strings.SearchPlaceholder);
            }
        }

        public void FocusInput()
        {
            if (!_txtSearch.Focused)
            {
                _txtSearch.Focus();
                _txtSearch.SelectAll();
            }
            _isSearchFocused = true;
            Invalidate();
        }

        public void BlurInput()
        {
            _isSearchFocused = false;
            Parent?.Focus();
            Invalidate();
        }

        public void ClearSearch()
        {
            _txtSearch.Text = string.Empty;
            _btnClear.Visible = false;
            ClearRequested?.Invoke();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color border = IsSearchFocused ? BorderFocused : BorderNormal;
            float penWidth = IsSearchFocused ? 1.5f : 1f;

            using (var bgBrush = new SolidBrush(BgColor))
            using (var borderPen = new Pen(border, penWidth))
            {
                FillRoundedRectangle(g, bgBrush, rect, 6);
                DrawRoundedRectangle(g, borderPen, rect, 6);
            }

            // Ícone de Lupa
            int iconX = 12;
            int iconY = 10;
            using (var iconFont = new Font("Segoe UI", 9.5f))
            using (var iconBrush = new SolidBrush(IsSearchFocused ? BorderFocused : IconColor))
            {
                g.DrawString("🔍", iconFont, iconBrush, iconX, iconY);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            FocusInput();
        }

        private static void FillRoundedRectangle(Graphics g, Brush brush, Rectangle r, int radius)
        {
            using (var path = CreateRoundedRectanglePath(r, radius))
            {
                g.FillPath(brush, path);
            }
        }

        private static void DrawRoundedRectangle(Graphics g, Pen pen, Rectangle r, int radius)
        {
            using (var path = CreateRoundedRectanglePath(r, radius))
            {
                g.DrawPath(pen, path);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
