using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.UI.Controls
{
    /// <summary>Desenho do controle com botões e analógicos reagindo ao vivo. Apenas renderiza.</summary>
    internal sealed class GamepadVisualDebugger : Control
    {
        private const float LogicalWidth = 520f;
        private const float LogicalHeight = 330f;
        private const float StickTravel = 24f;
        private const float DeadzoneRatio = Core.GamepadDefaults.StickDeadzone;

        private static readonly Color Background = Color.FromArgb(24, 26, 32);
        private static readonly Color Outline = Color.FromArgb(120, 126, 140);
        private static readonly Color Idle = Color.FromArgb(52, 56, 66);
        private static readonly Color Lit = Color.FromArgb(240, 240, 245);

        private GamepadState _state = GamepadState.Disconnected;

        public GamepadVisualDebugger()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Background;
            MinimumSize = new Size(360, 230);
        }

        public void SetState(GamepadState state)
        {
            if (state.PacketNumber == _state.PacketNumber && state.IsConnected == _state.IsConnected) return;
            _state = state;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = Math.Min(Width / LogicalWidth, Height / LogicalHeight);
            g.TranslateTransform((Width - LogicalWidth * scale) / 2f, (Height - LogicalHeight * scale) / 2f);
            g.ScaleTransform(scale, scale);

            DrawBody(g);
            DrawTriggersAndBumpers(g);
            DrawStick(g, 140, 125, _state.LeftThumbX, _state.LeftThumbY, _state.IsPressed(GamepadButtons.LeftStick));
            DrawStick(g, 330, 215, _state.RightThumbX, _state.RightThumbY, _state.IsPressed(GamepadButtons.RightStick));
            DrawDPad(g, 210, 215);
            DrawFaceButtons(g, 390, 125);
            DrawCenterButtons(g);

            if (!_state.IsConnected) DrawDisconnected(g);
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
                using (var brush = new SolidBrush(Color.FromArgb(34, 37, 46)))
                using (var pen = new Pen(Outline, 2f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }
            }
        }

        private void DrawTriggersAndBumpers(Graphics g)
        {
            DrawTrigger(g, 90, 8, _state.LeftTrigger, "LT");
            DrawTrigger(g, 340, 8, _state.RightTrigger, "RT");
            DrawPill(g, new RectangleF(90, 42, 90, 22), _state.IsPressed(GamepadButtons.LeftShoulder), "LB");
            DrawPill(g, new RectangleF(340, 42, 90, 22), _state.IsPressed(GamepadButtons.RightShoulder), "RB");
        }

        private static void DrawTrigger(Graphics g, float x, float y, byte value, string label)
        {
            var rect = new RectangleF(x, y, 90, 26);
            using (var back = new SolidBrush(Idle))
            using (var fill = new SolidBrush(Color.FromArgb(90, 170, 255)))
            using (var pen = new Pen(Outline, 1.5f))
            {
                g.FillRectangle(back, rect);
                g.FillRectangle(fill, x, y, 90f * value / 255f, 26);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
            DrawLabel(g, label + " " + value, rect, Lit);
        }

        private static void DrawPill(Graphics g, RectangleF rect, bool pressed, string label)
        {
            using (var brush = new SolidBrush(pressed ? Lit : Idle))
            using (var pen = new Pen(Outline, 1.5f))
            {
                g.FillRectangle(brush, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
            DrawLabel(g, label, rect, pressed ? Background : Lit);
        }

        private void DrawStick(Graphics g, float cx, float cy, short rawX, short rawY, bool clicked)
        {
            const float radius = 38f;
            using (var pen = new Pen(Outline, 2f))
            using (var dead = new Pen(Color.FromArgb(110, 255, 190, 60), 1f) { DashStyle = DashStyle.Dash })
            using (var back = new SolidBrush(Idle))
            {
                g.FillEllipse(back, cx - radius, cy - radius, radius * 2, radius * 2);
                g.DrawEllipse(pen, cx - radius, cy - radius, radius * 2, radius * 2);
                float dz = StickTravel * DeadzoneRatio * 2.2f;
                g.DrawEllipse(dead, cx - dz, cy - dz, dz * 2, dz * 2);
            }

            float nx = rawX / 32768f;
            float ny = rawY / 32768f;
            float px = cx + nx * StickTravel;
            float py = cy - ny * StickTravel;
            bool outsideDeadzone = Math.Sqrt(nx * nx + ny * ny) > DeadzoneRatio;

            Color knob = clicked ? Color.FromArgb(255, 120, 120)
                : outsideDeadzone ? Color.FromArgb(120, 230, 150) : Lit;
            using (var brush = new SolidBrush(knob))
            using (var pen = new Pen(Outline, 1.5f))
            {
                g.FillEllipse(brush, px - 15, py - 15, 30, 30);
                g.DrawEllipse(pen, px - 15, py - 15, 30, 30);
            }
        }

        private void DrawDPad(Graphics g, float cx, float cy)
        {
            const float s = 22f;
            DrawDPadArm(g, cx - s / 2, cy - s * 1.5f, s, s, _state.IsPressed(GamepadButtons.DPadUp));
            DrawDPadArm(g, cx - s / 2, cy + s * 0.5f, s, s, _state.IsPressed(GamepadButtons.DPadDown));
            DrawDPadArm(g, cx - s * 1.5f, cy - s / 2, s, s, _state.IsPressed(GamepadButtons.DPadLeft));
            DrawDPadArm(g, cx + s * 0.5f, cy - s / 2, s, s, _state.IsPressed(GamepadButtons.DPadRight));
            DrawDPadArm(g, cx - s / 2, cy - s / 2, s, s, false);
        }

        private static void DrawDPadArm(Graphics g, float x, float y, float w, float h, bool pressed)
        {
            using (var brush = new SolidBrush(pressed ? Lit : Idle))
            using (var pen = new Pen(Outline, 1.5f))
            {
                g.FillRectangle(brush, x, y, w, h);
                g.DrawRectangle(pen, x, y, w, h);
            }
        }

        private void DrawFaceButtons(Graphics g, float cx, float cy)
        {
            DrawRound(g, cx, cy + 32, _state.IsPressed(GamepadButtons.A), Color.FromArgb(90, 200, 90), "A");
            DrawRound(g, cx + 32, cy, _state.IsPressed(GamepadButtons.B), Color.FromArgb(230, 80, 70), "B");
            DrawRound(g, cx - 32, cy, _state.IsPressed(GamepadButtons.X), Color.FromArgb(70, 140, 240), "X");
            DrawRound(g, cx, cy - 32, _state.IsPressed(GamepadButtons.Y), Color.FromArgb(240, 200, 60), "Y");
        }

        private static void DrawRound(Graphics g, float cx, float cy, bool pressed, Color lit, string label)
        {
            const float r = 16f;
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

        private static void DrawDisconnected(Graphics g)
        {
            using (var veil = new SolidBrush(Color.FromArgb(150, 24, 26, 32)))
            using (var font = new Font("Segoe UI", 14f, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(255, 190, 90)))
            {
                g.FillRectangle(veil, -2000, -2000, 4000, 4000);
                var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(Strings.StatusNone, font, brush, new RectangleF(0, 0, LogicalWidth, LogicalHeight), format);
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
