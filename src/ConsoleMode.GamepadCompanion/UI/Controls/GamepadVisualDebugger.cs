using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>
    /// Desenho do controle ou console portátil com botões, analógicos, trackpads e botões traseiros reagindo ao vivo.
    /// Suporta modo StandardGamepad e HandheldSteamDeck. Apenas renderiza.
    /// </summary>
    internal sealed class GamepadVisualDebugger : Control
    {
        private const float StandardLogicalWidth = 520f;
        private const float StandardLogicalHeight = 330f;
        private const float HandheldLogicalWidth = 620f;
        private const float HandheldLogicalHeight = 346f;
        private const float StickTravel = 24f;

        private static readonly Color Background = Color.FromArgb(24, 26, 32);
        private static readonly Color Outline = Color.FromArgb(120, 126, 140);
        private static readonly Color Idle = Color.FromArgb(52, 56, 66);
        private static readonly Color Lit = Color.FromArgb(240, 240, 245);
        private static readonly Color ChassisBg = Color.FromArgb(34, 37, 46);
        private static readonly Color ScreenBg = Color.FromArgb(16, 18, 23);
        private static readonly Color ScreenBorder = Color.FromArgb(48, 52, 64);
        private static readonly Color TouchDotColor = Color.FromArgb(120, 230, 150);

        private GamepadState _state = GamepadState.Disconnected;
        private SteamDeckRawState _deckRaw;
        private float _stickDeadzone = Core.GamepadDefaults.StickDeadzone;
        private byte _triggerThreshold = 51;
        private ControllerVisualMode _visualMode = ControllerVisualMode.StandardGamepad;

        public GamepadVisualDebugger()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Background;
            MinimumSize = new Size(360, 230);
        }

        public ControllerVisualMode VisualMode
        {
            get => _visualMode;
            set
            {
                if (_visualMode == value) return;
                _visualMode = value;
                Invalidate();
            }
        }

        /// <summary>Deadzone dos analógicos (0-1) mostrada no círculo tracejado.</summary>
        public float StickDeadzone
        {
            get => _stickDeadzone;
            set
            {
                if (Math.Abs(_stickDeadzone - value) < 0.0001f) return;
                _stickDeadzone = value;
                Invalidate();
            }
        }

        /// <summary>Limiar (0-255) dos gatilhos mostrado como marca nas barras.</summary>
        public byte TriggerThreshold
        {
            get => _triggerThreshold;
            set
            {
                if (_triggerThreshold == value) return;
                _triggerThreshold = value;
                Invalidate();
            }
        }

        public void SetState(GamepadState state)
        {
            if (state.PacketNumber == _state.PacketNumber && state.IsConnected == _state.IsConnected) return;
            _state = state;
            Invalidate();
        }

        public void SetSteamDeckRawState(SteamDeckRawState raw)
        {
            _deckRaw = raw;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float logW = _visualMode == ControllerVisualMode.HandheldSteamDeck ? HandheldLogicalWidth : StandardLogicalWidth;
            float logH = _visualMode == ControllerVisualMode.HandheldSteamDeck ? HandheldLogicalHeight : StandardLogicalHeight;

            float scale = Math.Min(Width / logW, Height / logH);
            g.TranslateTransform((Width - logW * scale) / 2f, (Height - logH * scale) / 2f);
            g.ScaleTransform(scale, scale);

            if (_visualMode == ControllerVisualMode.HandheldSteamDeck)
            {
                DrawHandheld(g);
            }
            else
            {
                DrawStandardGamepad(g);
            }

            if (!_state.IsConnected) DrawDisconnected(g, logW, logH);
        }

        private void DrawStandardGamepad(Graphics g)
        {
            DrawBody(g);
            DrawTriggersAndBumpers(g);
            DrawStick(g, 140, 125, _state.LeftThumbX, _state.LeftThumbY, _state.IsPressed(GamepadButtons.LeftStick));
            DrawStick(g, 330, 215, _state.RightThumbX, _state.RightThumbY, _state.IsPressed(GamepadButtons.RightStick));
            DrawDPad(g, 210, 215);
            DrawFaceButtons(g, 390, 125);
            DrawCenterButtons(g);
        }

        private void DrawHandheld(Graphics g)
        {
            // 1. Carcaça do Handheld
            DrawHandheldChassis(g);

            // 2. Tela Central
            DrawHandheldScreen(g);

            // 3. Gatilhos & Bumpers no Topo
            DrawTrigger(g, 55, 6, _state.LeftTrigger, "LT", 85);
            DrawTrigger(g, 480, 6, _state.RightTrigger, "RT", 85);
            DrawPill(g, new RectangleF(55, 30, 85, 18), _state.IsPressed(GamepadButtons.LeftShoulder) || _deckRaw.IsL1, "LB");
            DrawPill(g, new RectangleF(480, 30, 85, 18), _state.IsPressed(GamepadButtons.RightShoulder) || _deckRaw.IsR1, "RB");

            // 4. Lado Esquerdo (Stick, D-Pad, Trackpad, Botões)
            DrawStick(g, 100, 92, _state.LeftThumbX, _state.LeftThumbY, _state.IsPressed(GamepadButtons.LeftStick) || _deckRaw.IsL3, 30f);
            DrawDPad(g, 100, 168, 18f);
            DrawTrackpad(g, new RectangleF(72, 212, 56, 56), "L-PAD", _deckRaw.IsLPadTouch, _deckRaw.IsLPadPress, _deckRaw.LPadX, _deckRaw.LPadY);
            DrawPill(g, new RectangleF(150, 72, 26, 18), _state.IsPressed(GamepadButtons.Back) || _deckRaw.IsBack, "◂");
            DrawPill(g, new RectangleF(146, 226, 32, 18), _state.IsPressed(GamepadButtons.Guide) || _deckRaw.IsGuide, "STEAM");

            // 5. Lado Direito (ABXY, Stick, Trackpad, Botões)
            DrawFaceButtons(g, 520, 92);
            DrawStick(g, 520, 168, _state.RightThumbX, _state.RightThumbY, _state.IsPressed(GamepadButtons.RightStick) || _deckRaw.IsR3, 30f);
            DrawTrackpad(g, new RectangleF(492, 212, 56, 56), "R-PAD", _deckRaw.IsRPadTouch, _deckRaw.IsRPadPress, _deckRaw.RPadX, _deckRaw.RPadY);
            DrawPill(g, new RectangleF(444, 72, 26, 18), _state.IsPressed(GamepadButtons.Start) || _deckRaw.IsStart, "≡");
            DrawPill(g, new RectangleF(442, 226, 32, 18), (_deckRaw.Buttons5 & SDCButton5.BTN_QUICK_ACCESS) != 0, "•••");

            // 6. Linha Horizontal Única de Botões Traseiros no Rodapé: [ L4 ] [ L5 ]   [ R5 ] [ R4 ]
            DrawRearButtonsRow(g);
        }

        private static void DrawHandheldChassis(Graphics g)
        {
            var rect = new RectangleF(24, 28, 572, 258);
            using (var path = CreateRoundedRectanglePath(rect, 22f))
            using (var brush = new SolidBrush(ChassisBg))
            using (var pen = new Pen(Outline, 2f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }
        }

        private static void DrawHandheldScreen(Graphics g)
        {
            var screenRect = new RectangleF(186, 44, 248, 192);
            using (var path = CreateRoundedRectanglePath(screenRect, 8f))
            using (var brush = new SolidBrush(ScreenBg))
            using (var pen = new Pen(ScreenBorder, 1.5f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            // Marca / Header na Tela
            using (var font = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(90, 96, 115)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("STEAM DECK", font, brush, screenRect, sf);
            }
        }

        private void DrawTrackpad(Graphics g, RectangleF rect, string label, bool touched, bool pressed, short rawX, short rawY)
        {
            Color fill = pressed ? Color.FromArgb(70, 110, 160) : (touched ? Color.FromArgb(42, 48, 62) : Idle);
            Color border = pressed ? Color.FromArgb(120, 190, 255) : (touched ? TouchDotColor : Outline);

            using (var path = CreateRoundedRectanglePath(rect, 6f))
            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(border, pressed ? 2.5f : 1.5f))
            {
                g.FillPath(brush, path);
                g.DrawPath(pen, path);
            }

            // Desenha ponto de toque analógico quando o dedo está na superfície
            if (touched)
            {
                float normX = Math.Max(-1f, Math.Min(1f, rawX / 32768f));
                float normY = Math.Max(-1f, Math.Min(1f, rawY / 32768f));
                float dotX = rect.X + rect.Width / 2f + normX * (rect.Width / 2f - 6f);
                float dotY = rect.Y + rect.Height / 2f - normY * (rect.Height / 2f - 6f);

                using (var dotBrush = new SolidBrush(TouchDotColor))
                using (var dotPen = new Pen(Color.White, 1f))
                {
                    g.FillEllipse(dotBrush, dotX - 5, dotY - 5, 10, 10);
                    g.DrawEllipse(dotPen, dotX - 5, dotY - 5, 10, 10);
                }
            }
            else
            {
                DrawLabel(g, label, rect, Lit);
            }
        }

        private void DrawRearButtonsRow(Graphics g)
        {
            // Linha horizontal unificada no rodapé do controle: [ L4 ] [ L5 ]   [ R5 ] [ R4 ]
            const float y = 298f;
            const float h = 28f;
            const float w = 68f;

            DrawPill(g, new RectangleF(100, y, w, h), _deckRaw.IsL4, "L4");
            DrawPill(g, new RectangleF(176, y, w, h), _deckRaw.IsL5, "L5");

            // Legenda no centro da linha
            var centerRect = new RectangleF(252, y, 116, h);
            using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(120, 130, 150)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("REAR PADDLES", font, brush, centerRect, sf);
            }

            DrawPill(g, new RectangleF(376, y, w, h), _deckRaw.IsR5, "R5");
            DrawPill(g, new RectangleF(452, y, w, h), _deckRaw.IsR4, "R4");
        }

        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawBody(Graphics g)
        {
            using (var path = new GraphicsPath())
            {
                path.AddArc(50, 70, 120, 120, 180, 90);
                path.AddArc(350, 70, 120, 120, 270, 90);
                path.AddArc(400, 160, 100, 140, 0, 70);
                path.AddArc(20, 160, 100, 140, 110, 70);
                path.CloseFigure();
                using (var brush = new SolidBrush(ChassisBg))
                using (var pen = new Pen(Outline, 2f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }
            }
        }

        private void DrawTriggersAndBumpers(Graphics g)
        {
            DrawTrigger(g, 90, 8, _state.LeftTrigger, "LT", 90);
            DrawTrigger(g, 340, 8, _state.RightTrigger, "RT", 90);
            DrawPill(g, new RectangleF(90, 42, 90, 22), _state.IsPressed(GamepadButtons.LeftShoulder), "LB");
            DrawPill(g, new RectangleF(340, 42, 90, 22), _state.IsPressed(GamepadButtons.RightShoulder), "RB");
        }

        private void DrawTrigger(Graphics g, float x, float y, byte value, string label, float width)
        {
            var rect = new RectangleF(x, y, width, 22);
            bool active = value >= _triggerThreshold;
            Color fillColor = active ? Color.FromArgb(120, 230, 150) : Color.FromArgb(90, 170, 255);
            float markX = x + width * _triggerThreshold / 255f;
            using (var back = new SolidBrush(Idle))
            using (var fill = new SolidBrush(fillColor))
            using (var pen = new Pen(Outline, 1.5f))
            using (var mark = new Pen(Color.FromArgb(255, 190, 60), 2f))
            {
                g.FillRectangle(back, rect);
                g.FillRectangle(fill, x, y, width * value / 255f, 22);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                g.DrawLine(mark, markX, y - 2, markX, y + 24);
            }
            DrawLabel(g, label + " " + value, rect, Lit);
        }

        private static void DrawPill(Graphics g, RectangleF rect, bool pressed, string label)
        {
            Color fill = pressed ? Color.FromArgb(120, 190, 255) : Idle;
            Color text = pressed ? Background : Lit;

            using (var brush = new SolidBrush(fill))
            using (var pen = new Pen(pressed ? Color.White : Outline, pressed ? 2f : 1.5f))
            {
                g.FillRectangle(brush, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
            DrawLabel(g, label, rect, text);
        }

        private void DrawStick(Graphics g, float cx, float cy, short rawX, short rawY, bool clicked, float radius = 38f)
        {
            using (var pen = new Pen(Outline, 2f))
            using (var dead = new Pen(Color.FromArgb(110, 255, 190, 60), 1f) { DashStyle = DashStyle.Dash })
            using (var back = new SolidBrush(Idle))
            {
                g.FillEllipse(back, cx - radius, cy - radius, radius * 2, radius * 2);
                g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
                float dz = StickTravel * _stickDeadzone;
                g.DrawEllipse(dead, cx - dz, cy - dz, dz * 2, dz * 2);
            }

            float nx = rawX / 32768f;
            float ny = rawY / 32768f;
            float px = cx + nx * StickTravel;
            float py = cy - ny * StickTravel;
            bool outsideDeadzone = Math.Sqrt(nx * nx + ny * ny) > _stickDeadzone;

            Color knob = clicked ? Color.FromArgb(255, 120, 120)
                : outsideDeadzone ? Color.FromArgb(120, 230, 150) : Lit;
            float knobRadius = radius > 32f ? 15f : 12f;
            using (var brush = new SolidBrush(knob))
            using (var pen = new Pen(Outline, 1.5f))
            {
                g.FillEllipse(brush, px - knobRadius, py - knobRadius, knobRadius * 2, knobRadius * 2);
                g.DrawEllipse(pen, px - knobRadius, py - knobRadius, knobRadius * 2, knobRadius * 2);
            }
        }

        private void DrawDPad(Graphics g, float cx, float cy, float s = 22f)
        {
            DrawDPadArm(g, cx - s / 2, cy - s * 1.5f, s, s, _state.IsPressed(GamepadButtons.DPadUp) || _deckRaw.IsDPadUp);
            DrawDPadArm(g, cx - s / 2, cy + s * 0.5f, s, s, _state.IsPressed(GamepadButtons.DPadDown) || _deckRaw.IsDPadDown);
            DrawDPadArm(g, cx - s * 1.5f, cy - s / 2, s, s, _state.IsPressed(GamepadButtons.DPadLeft) || _deckRaw.IsDPadLeft);
            DrawDPadArm(g, cx + s * 0.5f, cy - s / 2, s, s, _state.IsPressed(GamepadButtons.DPadRight) || _deckRaw.IsDPadRight);
            DrawDPadArm(g, cx - s / 2, cy - s / 2, s, s, false);
        }

        private static void DrawDPadArm(Graphics g, float x, float y, float w, float h, bool pressed)
        {
            using (var brush = new SolidBrush(pressed ? Lit : Idle))
            using (var pen = new Pen(pressed ? Color.White : Outline, 1.5f))
            {
                g.FillRectangle(brush, x, y, w, h);
                g.DrawRectangle(pen, x, y, w, h);
            }
        }

        private void DrawFaceButtons(Graphics g, float cx, float cy)
        {
            DrawRound(g, cx, cy + 30, _state.IsPressed(GamepadButtons.A) || _deckRaw.IsA, Color.FromArgb(90, 200, 90), "A");
            DrawRound(g, cx + 30, cy, _state.IsPressed(GamepadButtons.B) || _deckRaw.IsB, Color.FromArgb(230, 80, 70), "B");
            DrawRound(g, cx - 30, cy, _state.IsPressed(GamepadButtons.X) || _deckRaw.IsX, Color.FromArgb(70, 140, 240), "X");
            DrawRound(g, cx, cy - 30, _state.IsPressed(GamepadButtons.Y) || _deckRaw.IsY, Color.FromArgb(240, 200, 60), "Y");
        }

        private static void DrawRound(Graphics g, float cx, float cy, bool pressed, Color lit, string label)
        {
            const float r = 15f;
            var rect = new RectangleF(cx - r, cy - r, r * 2, r * 2);
            using (var brush = new SolidBrush(pressed ? lit : Idle))
            using (var pen = new Pen(pressed ? lit : Outline, 2f))
            {
                g.FillEllipse(brush, rect);
                g.DrawEllipse(pen, rect);
            }
            DrawLabel(g, label, rect, pressed ? Background : Lit);
        }

        private void DrawCenterButtons(Graphics g)
        {
            DrawRound(g, 262, 80, _state.IsPressed(GamepadButtons.Guide), Color.FromArgb(160, 120, 255), "G");
            DrawPill(g, new RectangleF(222, 122, 34, 18), _state.IsPressed(GamepadButtons.Back), "◂");
            DrawPill(g, new RectangleF(268, 122, 34, 18), _state.IsPressed(GamepadButtons.Start), "≡");
        }

        private static void DrawDisconnected(Graphics g, float logW, float logH)
        {
            using (var veil = new SolidBrush(Color.FromArgb(150, 24, 26, 32)))
            using (var font = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(255, 190, 90)))
            {
                g.FillRectangle(veil, -2000, -2000, 4000, 4000);
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(Strings.StatusNone, font, brush, new RectangleF(0, 0, logW, logH), format);
            }
        }

        private static void DrawLabel(Graphics g, string text, RectangleF rect, Color color)
        {
            using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (var brush = new SolidBrush(color))
            {
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(text, font, brush, rect, format);
            }
        }
    }
}
