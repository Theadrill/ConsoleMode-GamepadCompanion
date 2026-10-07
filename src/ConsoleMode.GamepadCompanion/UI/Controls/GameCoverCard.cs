using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Componente visual que representa a capa de um jogo cadastrado ou o card especial de adição [+].
    /// Renderiza capas, efeitos de gradiente caso não haja imagem, badges de ação para controle e foco couch gaming.
    /// </summary>
    internal sealed class GameCoverCard : Control
    {
        private static readonly Color BgCard = Color.FromArgb(34, 37, 46);
        private static readonly Color BorderDefault = Color.FromArgb(65, 70, 85);
        private static readonly Color BorderFocused = Color.FromArgb(120, 190, 255);
        private static readonly Color TextPrimary = Color.FromArgb(230, 232, 240);
        private static readonly Color TextSecondary = Color.FromArgb(160, 170, 185);
        private static readonly Color BadgeLaunchBg = Color.FromArgb(46, 125, 80);
        private static readonly Color BadgeConfigBg = Color.FromArgb(40, 80, 140);
        private static readonly Color BadgeAddBg = Color.FromArgb(30, 110, 150);

        private readonly GameEntry _game;
        private Image _coverImage;
        private bool _isFocusedCard;

        public GameCoverCard(GameEntry game = null)
        {
            _game = game;
            Size = new Size(160, 220);
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;

            LoadCoverImage();
        }

        public GameEntry Game => _game;
        public bool IsAddSlot => _game == null;

        public bool IsFocusedCard
        {
            get => _isFocusedCard;
            set
            {
                if (_isFocusedCard != value)
                {
                    _isFocusedCard = value;
                    Invalidate();
                }
            }
        }

        public void ReloadImage()
        {
            LoadCoverImage();
            Invalidate();
        }

        private void LoadCoverImage()
        {
            if (_coverImage != null)
            {
                _coverImage.Dispose();
                _coverImage = null;
            }

            if (_game != null && !string.IsNullOrWhiteSpace(_game.CoverImagePath))
            {
                try
                {
                    if (File.Exists(_game.CoverImagePath))
                    {
                        // Carrega em memória para não bloquear o arquivo no disco
                        using (var stream = new FileStream(_game.CoverImagePath, FileMode.Open, FileAccess.Read))
                        {
                            _coverImage = Image.FromStream(stream);
                        }
                    }
                }
                catch
                {
                    _coverImage = null;
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            var bounds = new Rectangle(0, 0, Width, Height);
            var innerBounds = new Rectangle(0, 0, Width - 1, Height - 1);

            if (IsAddSlot)
            {
                PaintAddSlot(g, bounds);
            }
            else
            {
                PaintGameCard(g, bounds);
            }

            // Borda do card
            int borderWidth = _isFocusedCard ? 3 : 1;
            Color borderColor = _isFocusedCard ? BorderFocused : BorderDefault;
            using (var borderPen = new Pen(borderColor, borderWidth))
            {
                int offset = borderWidth / 2;
                g.DrawRectangle(borderPen, offset, offset, Width - borderWidth, Height - borderWidth);
            }
        }

        private void PaintGameCard(Graphics g, Rectangle bounds)
        {
            if (_coverImage != null)
            {
                g.DrawImage(_coverImage, bounds);
                // Gradiente escuro inferior para legibilidade do título
                using (var grad = new LinearGradientBrush(
                    new Point(0, Height - 70), new Point(0, Height),
                    Color.FromArgb(0, 15, 17, 22), Color.FromArgb(230, 15, 17, 22)))
                {
                    g.FillRectangle(grad, 0, Height - 70, Width, 70);
                }
            }
            else
            {
                // Fallback com gradiente dark elegante
                using (var bgBrush = new LinearGradientBrush(
                    new Point(0, 0), new Point(0, Height),
                    Color.FromArgb(42, 46, 58), Color.FromArgb(22, 24, 30)))
                {
                    g.FillRectangle(bgBrush, bounds);
                }

                // Ícone de jogo centralizado estilizado
                using (var iconFont = new Font("Segoe UI", 26f, FontStyle.Bold))
                using (var iconBrush = new SolidBrush(Color.FromArgb(80, 90, 115)))
                {
                    var sfIcon = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("🎮", iconFont, iconBrush, new RectangleF(0, 40, Width, 50), sfIcon);
                }
            }

            // Nome do jogo na base
            using (var titleFont = new Font("Segoe UI", 9.5f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(TextPrimary))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Far,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };
                g.DrawString(_game.Name, titleFont, titleBrush, new RectangleF(6, Height - 28, Width - 12, 22), sf);
            }

            // Overlays de ação caso esteja focado com gamepad
            if (_isFocusedCard)
            {
                // Película escura sutil
                using (var overlayBrush = new SolidBrush(Color.FromArgb(140, 10, 12, 16)))
                {
                    g.FillRectangle(overlayBrush, bounds);
                }

                // Badge [A] INICIAR
                DrawBadge(g, Strings.ActionLaunch, BadgeLaunchBg, (Height / 2) - 24);

                // Badge [Y] CONFIGURAR
                DrawBadge(g, Strings.ActionConfigure, BadgeConfigBg, (Height / 2) + 6);
            }
        }

        private void PaintAddSlot(Graphics g, Rectangle bounds)
        {
            // Fundo escuro com padrão sutil
            using (var bgBrush = new SolidBrush(BgCard))
            {
                g.FillRectangle(bgBrush, bounds);
            }

            // Sinal [+] estilizado no centro
            using (var plusFont = new Font("Segoe UI", 36f, FontStyle.Bold))
            using (var plusBrush = new SolidBrush(_isFocusedCard ? BorderFocused : TextSecondary))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("+", plusFont, plusBrush, new RectangleF(0, 35, Width, 60), sf);
            }

            // Título "Adicionar Jogo"
            using (var font = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (var brush = new SolidBrush(_isFocusedCard ? TextPrimary : TextSecondary))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(Strings.AddGameTitle, font, brush, new RectangleF(6, 100, Width - 12, 24), sf);
            }

            // Overlay de ação ao focar
            if (_isFocusedCard)
            {
                DrawBadge(g, Strings.ActionAdd, BadgeAddBg, Height - 48);
            }
        }

        private void DrawBadge(Graphics g, string text, Color bgColor, int top)
        {
            int badgeWidth = Width - 24;
            int badgeHeight = 24;
            var badgeRect = new Rectangle(12, top, badgeWidth, badgeHeight);

            using (var path = CreateRoundedRectanglePath(badgeRect, 4))
            using (var brush = new SolidBrush(bgColor))
            using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 1))
            using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.White))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);

                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, font, textBrush, badgeRect, sf);
            }
        }

        private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _coverImage != null)
            {
                _coverImage.Dispose();
                _coverImage = null;
            }
            base.Dispose(disposing);
        }
    }
}
