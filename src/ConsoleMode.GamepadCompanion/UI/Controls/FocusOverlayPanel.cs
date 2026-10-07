using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.UI.Navigation;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Painel de overlay escurecido para edição modal de sliders e dropdown via gamepad.
    /// Renderiza a interface escurecida e destaca o elemento ativo no estilo Couch Gaming.
    /// </summary>
    public sealed class FocusOverlayPanel : Control
    {
        private Bitmap _snapshot;
        private INavigableControl _activeControl;

        private bool _isExitDialog;
        private int _exitOptionIndex;
        private Action _onCloseApp;
        private Action _onMinimizeToTray;
        private Action _onCancelExit;
        private Rectangle _exitOption0Rect;
        private Rectangle _exitOption1Rect;

        private bool _prevExitA;
        private bool _prevExitB;
        private bool _prevExitUp;
        private bool _prevExitDown;

        public FocusOverlayPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.Selectable,
                true);
            Visible = false;
        }

        public bool IsActive => _isExitDialog || (_activeControl != null && _activeControl.IsEditing);

        public bool IsExitDialogOpen => _isExitDialog;

        public void ShowOverlay(INavigableControl control, Form parent)
        {
            if (control == null || parent == null) return;

            // Se o overlay já está ativo para este controle, apenas redesenha sem recapturar tela
            if (Visible && _activeControl == control && !_isExitDialog)
            {
                Invalidate();
                return;
            }

            if (_activeControl != null)
            {
                _activeControl.StateChanged -= OnActiveControlStateChanged;
            }

            _activeControl = control;
            _activeControl.StateChanged += OnActiveControlStateChanged;
            _isExitDialog = false;

            Bounds = new Rectangle(0, 0, parent.ClientSize.Width, parent.ClientSize.Height);

            // Tira snapshot limpo apenas da área cliente do formulário
            _snapshot?.Dispose();
            _snapshot = CaptureClientArea(parent);

            BringToFront();
            Visible = true;
            Invalidate();
        }

        public void ShowExitDialog(Form parent, Action onCloseApp, Action onMinimizeToTray, Action onCancel = null)
        {
            if (parent == null) return;

            if (Visible && _isExitDialog)
            {
                Invalidate();
                return;
            }

            if (_activeControl != null)
            {
                _activeControl.StateChanged -= OnActiveControlStateChanged;
                _activeControl = null;
            }

            _isExitDialog = true;
            _exitOptionIndex = 0; // Padrão: Minimizar para a barra de tarefas
            _onCloseApp = onCloseApp;
            _onMinimizeToTray = onMinimizeToTray;
            _onCancelExit = onCancel;

            _prevExitA = true; // Evita disparo acidental se botão A ainda estiver pressionado do clique
            _prevExitB = false;
            _prevExitUp = false;
            _prevExitDown = false;

            Bounds = new Rectangle(0, 0, parent.ClientSize.Width, parent.ClientSize.Height);

            _snapshot?.Dispose();
            _snapshot = CaptureClientArea(parent);

            BringToFront();
            Visible = true;
            Invalidate();
        }

        private static Bitmap CaptureClientArea(Form parent)
        {
            int w = Math.Max(1, parent.ClientSize.Width);
            int h = Math.Max(1, parent.ClientSize.Height);
            var bmp = new Bitmap(w, h);

            try
            {
                if (parent.IsHandleCreated && parent.Visible)
                {
                    Point origin = parent.PointToScreen(Point.Empty);
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(origin.X, origin.Y, 0, 0, new Size(w, h), CopyPixelOperation.SourceCopy);
                    }
                    return bmp;
                }
            }
            catch
            {
                // Fallback seguro caso esteja em execução headless/teste
            }

            using (var g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(Color.FromArgb(30, 32, 40)))
            {
                g.FillRectangle(brush, 0, 0, w, h);
            }
            return bmp;
        }

        public void CloseExitDialog()
        {
            _isExitDialog = false;
            _onCloseApp = null;
            _onMinimizeToTray = null;
            _onCancelExit = null;

            Visible = false;
            _snapshot?.Dispose();
            _snapshot = null;
            Parent?.Invalidate(true);
        }

        public void ProcessExitGamepad(GamepadState state, long nowMs)
        {
            if (!state.IsConnected || !_isExitDialog) return;

            // Botão A confirma a opção selecionada
            bool aPressed = state.IsPressed(GamepadButtons.A);
            if (aPressed && !_prevExitA)
            {
                if (_exitOptionIndex == 0)
                {
                    var act = _onMinimizeToTray;
                    CloseExitDialog();
                    act?.Invoke();
                }
                else
                {
                    var act = _onCloseApp;
                    CloseExitDialog();
                    act?.Invoke();
                }
                return;
            }
            _prevExitA = aPressed;

            // Botão B cancela e retorna para a tela principal
            bool bPressed = state.IsPressed(GamepadButtons.B);
            if (bPressed && !_prevExitB)
            {
                var cancel = _onCancelExit;
                CloseExitDialog();
                cancel?.Invoke();
                return;
            }
            _prevExitB = bPressed;

            // D-Pad Up e Down alternam entre as 2 opções
            bool up = state.IsPressed(GamepadButtons.DPadUp);
            if (up && !_prevExitUp)
            {
                _exitOptionIndex = _exitOptionIndex == 0 ? 1 : 0;
                Invalidate();
            }
            _prevExitUp = up;

            bool down = state.IsPressed(GamepadButtons.DPadDown);
            if (down && !_prevExitDown)
            {
                _exitOptionIndex = _exitOptionIndex == 0 ? 1 : 0;
                Invalidate();
            }
            _prevExitDown = down;
        }

        public void HideOverlay()
        {
            if (_activeControl != null)
            {
                _activeControl.StateChanged -= OnActiveControlStateChanged;
                _activeControl = null;
            }

            _isExitDialog = false;
            Visible = false;
            _snapshot?.Dispose();
            _snapshot = null;
            Parent?.Invalidate(true);
        }

        private void OnActiveControlStateChanged()
        {
            if (_activeControl == null || !_activeControl.IsEditing)
            {
                HideOverlay();
                return;
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Desenha o snapshot de fundo capturado
            if (_snapshot != null)
            {
                g.DrawImageUnscaled(_snapshot, 0, 0);
            }
            else
            {
                using (var bgBrush = new SolidBrush(Color.FromArgb(30, 32, 40)))
                {
                    g.FillRectangle(bgBrush, ClientRectangle);
                }
            }

            // 2. Aplica a camada de escurecimento semi-transparente
            using (var dimBrush = new SolidBrush(Color.FromArgb(190, 15, 17, 22)))
            {
                g.FillRectangle(dimBrush, ClientRectangle);
            }

            // 3. Renderiza o card do controle ativo em destaque
            if (_isExitDialog)
            {
                DrawExitDialogCard(g);
            }
            else if (_activeControl is SliderNavigable slider)
            {
                DrawSliderCard(g, slider);
            }
            else if (_activeControl is DropdownNavigable dropdown)
            {
                DrawDropdownCard(g, dropdown);
            }
        }

        private void DrawSliderCard(Graphics g, SliderNavigable slider)
        {
            int cardW = 440;
            int cardH = 180;
            int cardX = (ClientSize.Width - cardW) / 2;
            int cardY = (ClientSize.Height - cardH) / 2;
            var cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            // Card background & borda
            using (var cardBrush = new SolidBrush(Color.FromArgb(34, 37, 46)))
            using (var borderPen = new Pen(Color.FromArgb(120, 190, 255), 2f))
            {
                FillRoundedRectangle(g, cardBrush, cardRect, 8);
                DrawRoundedRectangle(g, borderPen, cardRect, 8);
            }

            // Título
            using (var titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.FromArgb(230, 232, 240)))
            {
                g.DrawString(slider.Title, titleFont, textBrush, cardX + 24, cardY + 20);
            }

            // Valor em destaque
            string valueDisplay = slider.Value.ToString();
            if (slider.Title.Contains("%"))
            {
                valueDisplay += "%";
            }

            using (var valueFont = new Font("Segoe UI", 16f, FontStyle.Bold))
            using (var valueBrush = new SolidBrush(Color.FromArgb(120, 190, 255)))
            {
                var valSize = g.MeasureString(valueDisplay, valueFont);
                g.DrawString(valueDisplay, valueFont, valueBrush, cardX + cardW - 24 - valSize.Width, cardY + 16);
            }

            // Barra gráfica de progresso
            int barX = cardX + 24;
            int barY = cardY + 68;
            int barW = cardW - 48;
            int barH = 14;
            var trackRect = new Rectangle(barX, barY, barW, barH);

            using (var trackBrush = new SolidBrush(Color.FromArgb(52, 56, 66)))
            {
                FillRoundedRectangle(g, trackBrush, trackRect, 7);
            }

            int min = slider.Minimum;
            int max = slider.Maximum;
            int val = slider.Value;
            float ratio = max > min ? (float)(val - min) / (max - min) : 0f;
            ratio = Math.Max(0f, Math.Min(1f, ratio));
            int fillW = Math.Max(barH, (int)(barW * ratio));
            var fillRect = new Rectangle(barX, barY, fillW, barH);

            using (var fillBrush = new SolidBrush(Color.FromArgb(90, 170, 255)))
            {
                FillRoundedRectangle(g, fillBrush, fillRect, 7);
            }

            // Indicador / Thumb
            int thumbX = barX + (int)(barW * ratio);
            int thumbY = barY + (barH / 2);
            int thumbR = 10;
            var thumbRect = new Rectangle(thumbX - thumbR, thumbY - thumbR, thumbR * 2, thumbR * 2);

            using (var thumbBrush = new SolidBrush(Color.White))
            using (var thumbBorder = new Pen(Color.FromArgb(120, 190, 255), 2f))
            {
                g.FillEllipse(thumbBrush, thumbRect);
                g.DrawEllipse(thumbBorder, thumbRect);
            }

            // Valores Min / Max nos cantos da barra
            using (var minMaxFont = new Font("Segoe UI", 8.5f))
            using (var minMaxBrush = new SolidBrush(Color.FromArgb(160, 170, 185)))
            {
                g.DrawString(min.ToString(), minMaxFont, minMaxBrush, barX, barY + barH + 4);
                string maxStr = max.ToString();
                var maxSize = g.MeasureString(maxStr, minMaxFont);
                g.DrawString(maxStr, minMaxFont, minMaxBrush, barX + barW - maxSize.Width, barY + barH + 4);
            }

            // Legenda de atalhos
            int hintY = cardY + cardH - 34;
            DrawButtonBadge(g, "[◄ / ►]", Color.FromArgb(120, 190, 255), cardX + 24, hintY);
            DrawText(g, "Ajustar", Color.FromArgb(200, 205, 215), cardX + 78, hintY);

            DrawButtonBadge(g, "[A]", Color.FromArgb(120, 230, 150), cardX + 170, hintY);
            DrawText(g, "Confirmar", Color.FromArgb(200, 205, 215), cardX + 198, hintY);

            DrawButtonBadge(g, "[B]", Color.FromArgb(255, 120, 120), cardX + 300, hintY);
            DrawText(g, "Cancelar", Color.FromArgb(200, 205, 215), cardX + 328, hintY);
        }

        private void DrawDropdownCard(Graphics g, DropdownNavigable dropdown)
        {
            var combo = dropdown.ComboBox;
            int count = Math.Max(1, combo.Items.Count);
            int itemH = 36;
            int headerH = 50;
            int footerH = 46;
            int cardW = 380;
            int cardH = headerH + (count * itemH) + footerH;
            int cardX = (ClientSize.Width - cardW) / 2;
            int cardY = Math.Max(20, (ClientSize.Height - cardH) / 2);
            var cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            // Card background & borda
            using (var cardBrush = new SolidBrush(Color.FromArgb(34, 37, 46)))
            using (var borderPen = new Pen(Color.FromArgb(120, 190, 255), 2f))
            {
                FillRoundedRectangle(g, cardBrush, cardRect, 8);
                DrawRoundedRectangle(g, borderPen, cardRect, 8);
            }

            // Título
            using (var titleFont = new Font("Segoe UI", 11.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(Color.FromArgb(230, 232, 240)))
            {
                g.DrawString(Strings.SelectActiveController, titleFont, textBrush, cardX + 20, cardY + 16);
            }

            // Linhas dos itens
            int listY = cardY + headerH;
            for (int i = 0; i < count; i++)
            {
                int rowY = listY + (i * itemH);
                var rowRect = new Rectangle(cardX + 16, rowY, cardW - 32, itemH - 4);
                bool isSelected = i == dropdown.TempIndex;

                Color rowBg = isSelected ? Color.FromArgb(46, 75, 115) : Color.FromArgb(24, 26, 32);
                Color rowBorder = isSelected ? Color.FromArgb(120, 190, 255) : Color.FromArgb(52, 56, 66);

                using (var rowBrush = new SolidBrush(rowBg))
                using (var rowPen = new Pen(rowBorder, isSelected ? 1.5f : 1f))
                {
                    FillRoundedRectangle(g, rowBrush, rowRect, 6);
                    DrawRoundedRectangle(g, rowPen, rowRect, 6);
                }

                string itemText = i < combo.Items.Count ? combo.Items[i]?.ToString() ?? string.Empty : string.Empty;

                // Indicador de status (bolinha colorida)
                Color statusColor = Color.FromArgb(120, 126, 140);
                if (itemText.Contains(Strings.Connected)) statusColor = Color.FromArgb(120, 230, 150);
                else if (itemText.Contains(Strings.Automatic)) statusColor = Color.FromArgb(120, 190, 255);

                using (var dotBrush = new SolidBrush(statusColor))
                {
                    g.FillEllipse(dotBrush, rowRect.X + 12, rowRect.Y + (rowRect.Height / 2) - 4, 8, 8);
                }

                using (var itemFont = new Font("Segoe UI", 9.5f, isSelected ? FontStyle.Bold : FontStyle.Regular))
                using (var itemBrush = new SolidBrush(isSelected ? Color.White : Color.FromArgb(220, 225, 235)))
                {
                    g.DrawString(itemText, itemFont, itemBrush, rowRect.X + 28, rowRect.Y + 6);
                }
            }

            // Legenda no rodapé
            int hintY = cardY + cardH - 32;
            DrawButtonBadge(g, "[▲ / ▼]", Color.FromArgb(120, 190, 255), cardX + 20, hintY);
            DrawText(g, "Navegar", Color.FromArgb(200, 205, 215), cardX + 74, hintY);

            DrawButtonBadge(g, "[A]", Color.FromArgb(120, 230, 150), cardX + 160, hintY);
            DrawText(g, "Confirmar", Color.FromArgb(200, 205, 215), cardX + 188, hintY);

            DrawButtonBadge(g, "[B]", Color.FromArgb(255, 120, 120), cardX + 276, hintY);
            DrawText(g, "Cancelar", Color.FromArgb(200, 205, 215), cardX + 304, hintY);
        }

        private void DrawExitDialogCard(Graphics g)
        {
            int cardW = 460;
            int cardH = 250;
            int cardX = (ClientSize.Width - cardW) / 2;
            int cardY = (ClientSize.Height - cardH) / 2;
            var cardRect = new Rectangle(cardX, cardY, cardW, cardH);

            // Background do card & borda
            using (var cardBrush = new SolidBrush(Color.FromArgb(34, 37, 46)))
            using (var borderPen = new Pen(Color.FromArgb(120, 190, 255), 2f))
            {
                FillRoundedRectangle(g, cardBrush, cardRect, 8);
                DrawRoundedRectangle(g, borderPen, cardRect, 8);
            }

            // Título
            using (var titleFont = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(Color.FromArgb(230, 232, 240)))
            {
                g.DrawString(Strings.ExitDialogTitle, titleFont, titleBrush, cardX + 24, cardY + 18);
            }

            // Subtítulo
            using (var subFont = new Font("Segoe UI", 9f))
            using (var subBrush = new SolidBrush(Color.FromArgb(160, 170, 185)))
            {
                g.DrawString(Strings.ExitDialogMessage, subFont, subBrush, cardX + 24, cardY + 42);
            }

            // Opção 0: Minimizar para a Barra de Tarefas
            _exitOption0Rect = new Rectangle(cardX + 20, cardY + 68, cardW - 40, 52);
            bool sel0 = _exitOptionIndex == 0;
            Color bg0 = sel0 ? Color.FromArgb(46, 75, 115) : Color.FromArgb(24, 26, 32);
            Color border0 = sel0 ? Color.FromArgb(120, 190, 255) : Color.FromArgb(52, 56, 66);

            using (var bBrush = new SolidBrush(bg0))
            using (var bPen = new Pen(border0, sel0 ? 1.5f : 1f))
            {
                FillRoundedRectangle(g, bBrush, _exitOption0Rect, 6);
                DrawRoundedRectangle(g, bPen, _exitOption0Rect, 6);
            }

            using (var optFont = new Font("Segoe UI", 10f, sel0 ? FontStyle.Bold : FontStyle.Regular))
            using (var optBrush = new SolidBrush(sel0 ? Color.White : Color.FromArgb(220, 225, 235)))
            {
                g.DrawString(Strings.ExitActionMinimize, optFont, optBrush, _exitOption0Rect.X + 16, _exitOption0Rect.Y + 8);
            }
            using (var descFont = new Font("Segoe UI", 8.2f))
            using (var descBrush = new SolidBrush(Color.FromArgb(160, 170, 185)))
            {
                g.DrawString(Strings.ExitActionMinimizeDesc, descFont, descBrush, _exitOption0Rect.X + 16, _exitOption0Rect.Y + 28);
            }

            // Opção 1: Fechar o Aplicativo
            _exitOption1Rect = new Rectangle(cardX + 20, cardY + 128, cardW - 40, 52);
            bool sel1 = _exitOptionIndex == 1;
            Color bg1 = sel1 ? Color.FromArgb(115, 45, 45) : Color.FromArgb(24, 26, 32);
            Color border1 = sel1 ? Color.FromArgb(255, 120, 120) : Color.FromArgb(52, 56, 66);

            using (var bBrush = new SolidBrush(bg1))
            using (var bPen = new Pen(border1, sel1 ? 1.5f : 1f))
            {
                FillRoundedRectangle(g, bBrush, _exitOption1Rect, 6);
                DrawRoundedRectangle(g, bPen, _exitOption1Rect, 6);
            }

            using (var optFont = new Font("Segoe UI", 10f, sel1 ? FontStyle.Bold : FontStyle.Regular))
            using (var optBrush = new SolidBrush(sel1 ? Color.White : Color.FromArgb(220, 225, 235)))
            {
                g.DrawString(Strings.ExitActionClose, optFont, optBrush, _exitOption1Rect.X + 16, _exitOption1Rect.Y + 8);
            }
            using (var descFont = new Font("Segoe UI", 8.2f))
            using (var descBrush = new SolidBrush(Color.FromArgb(160, 170, 185)))
            {
                g.DrawString(Strings.ExitActionCloseDesc, descFont, descBrush, _exitOption1Rect.X + 16, _exitOption1Rect.Y + 28);
            }

            // Legenda de atalhos
            int hintY = cardY + cardH - 30;
            DrawButtonBadge(g, "[▲ / ▼]", Color.FromArgb(120, 190, 255), cardX + 24, hintY);
            DrawText(g, "Selecionar", Color.FromArgb(200, 205, 215), cardX + 76, hintY);

            DrawButtonBadge(g, "[A]", Color.FromArgb(120, 230, 150), cardX + 170, hintY);
            DrawText(g, "Confirmar", Color.FromArgb(200, 205, 215), cardX + 196, hintY);

            DrawButtonBadge(g, "[B]", Color.FromArgb(255, 120, 120), cardX + 290, hintY);
            DrawText(g, "Voltar", Color.FromArgb(200, 205, 215), cardX + 316, hintY);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!_isExitDialog) return;

            if (_exitOption0Rect.Contains(e.Location))
            {
                var act = _onMinimizeToTray;
                CloseExitDialog();
                act?.Invoke();
            }
            else if (_exitOption1Rect.Contains(e.Location))
            {
                var act = _onCloseApp;
                CloseExitDialog();
                act?.Invoke();
            }
            else
            {
                var cancel = _onCancelExit;
                CloseExitDialog();
                cancel?.Invoke();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isExitDialog) return;

            if (_exitOption0Rect.Contains(e.Location) && _exitOptionIndex != 0)
            {
                _exitOptionIndex = 0;
                Invalidate();
            }
            else if (_exitOption1Rect.Contains(e.Location) && _exitOptionIndex != 1)
            {
                _exitOptionIndex = 1;
                Invalidate();
            }
        }

        private static void DrawButtonBadge(Graphics g, string text, Color color, int x, int y)
        {
            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var brush = new SolidBrush(color))
            {
                g.DrawString(text, font, brush, x, y);
            }
        }

        private static void DrawText(Graphics g, string text, Color color, int x, int y)
        {
            using (var font = new Font("Segoe UI", 9f))
            using (var brush = new SolidBrush(color))
            {
                g.DrawString(text, font, brush, x, y);
            }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _snapshot?.Dispose();
                _snapshot = null;
            }
            base.Dispose(disposing);
        }
    }
}
