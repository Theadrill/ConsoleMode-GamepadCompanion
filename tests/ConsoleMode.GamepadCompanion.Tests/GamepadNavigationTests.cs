using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.UI.Controls;
using ConsoleMode.GamepadCompanion.UI.Navigation;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public sealed class GamepadNavigationTests
    {
        [Fact]
        public void NavigationManager_StartsAtFirstControl()
        {
            var nav = new GamepadNavigationManager();
            var btn1 = new Button();
            var btn2 = new Button();
            var navBtn1 = new ButtonNavigable("Btn1", btn1);
            var navBtn2 = new ButtonNavigable("Btn2", btn2);

            nav.RegisterControl(navBtn1);
            nav.RegisterControl(navBtn2);

            Assert.Equal(0, nav.FocusedIndex);
            Assert.Same(navBtn1, nav.FocusedControl);
            Assert.True(navBtn1.IsFocused);
            Assert.False(navBtn2.IsFocused);
        }

        [Fact]
        public void NavigationManager_DPadDownAndUp_NavigatesAndClamps()
        {
            var nav = new GamepadNavigationManager();
            var b1 = new Button();
            var b2 = new Button();
            var b3 = new Button();

            var nb1 = new ButtonNavigable("B1", b1);
            var nb2 = new ButtonNavigable("B2", b2);
            var nb3 = new ButtonNavigable("B3", b3);

            nav.RegisterControl(nb1);
            nav.RegisterControl(nb2);
            nav.RegisterControl(nb3);

            // D-Pad Down -> item 1
            var stateDown = new GamepadState(true, 1, GamepadButtons.DPadDown, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateDown, 100);
            Assert.Equal(1, nav.FocusedIndex);
            Assert.Same(nb2, nav.FocusedControl);

            // Release
            var stateNeutral = new GamepadState(true, 2, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateNeutral, 150);

            // D-Pad Down -> item 2 (last)
            nav.ProcessGamepad(stateDown, 200);
            Assert.Equal(2, nav.FocusedIndex);
            Assert.Same(nb3, nav.FocusedControl);

            // Release and D-Pad Down again -> clamps at item 2
            nav.ProcessGamepad(stateNeutral, 250);
            nav.ProcessGamepad(stateDown, 300);
            Assert.Equal(2, nav.FocusedIndex);

            // D-Pad Up -> item 1
            nav.ProcessGamepad(stateNeutral, 350);
            var stateUp = new GamepadState(true, 3, GamepadButtons.DPadUp, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateUp, 400);
            Assert.Equal(1, nav.FocusedIndex);
            Assert.Same(nb2, nav.FocusedControl);

            // D-Pad Up -> item 0
            nav.ProcessGamepad(stateNeutral, 450);
            nav.ProcessGamepad(stateUp, 500);
            Assert.Equal(0, nav.FocusedIndex);

            // D-Pad Up again -> clamps at item 0
            nav.ProcessGamepad(stateNeutral, 550);
            nav.ProcessGamepad(stateUp, 600);
            Assert.Equal(0, nav.FocusedIndex);
        }

        [Fact]
        public void NavigationManager_ButtonA_ExecutesButtonClick()
        {
            var nav = new GamepadNavigationManager();
            var button = new Button();
            bool clicked = false;
            button.Click += (s, e) => clicked = true;

            var navBtn = new ButtonNavigable("Btn", button);
            nav.RegisterControl(navBtn);

            var stateA = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateA, 100);

            Assert.True(clicked);
        }

        [Fact]
        public void SliderNavigable_EditAdjustSaveAndCancelCycle()
        {
            var track = new TrackBar { Minimum = 0, Maximum = 100, Value = 50 };
            var title = new Label();
            var valLabel = new Label { Text = "50" };
            int lastSaved = 50;

            var slider = new SliderNavigable("Sens", "Sensibilidade", title, track, valLabel, v => lastSaved = v);
            var nav = new GamepadNavigationManager();
            nav.RegisterControl(slider);

            // 1. Initial state: not editing
            Assert.False(slider.IsEditing);

            // 2. Press A -> enters editing mode
            var stateA = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);
            var stateNeutral = new GamepadState(true, 2, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateA, 100);
            nav.ProcessGamepad(stateNeutral, 120);

            Assert.True(slider.IsEditing);

            // 3. Press D-Pad Right -> increments value
            var stateRight = new GamepadState(true, 3, GamepadButtons.DPadRight, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateRight, 150);
            nav.ProcessGamepad(stateNeutral, 180);

            Assert.Equal(51, slider.Value);
            Assert.Equal(51, lastSaved);

            // 4. Press B -> cancels and restores initial value (50)
            var stateB = new GamepadState(true, 4, GamepadButtons.B, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateB, 200);

            Assert.False(slider.IsEditing);
            Assert.Equal(50, slider.Value);
            Assert.Equal(50, lastSaved);
            Assert.Equal("50", valLabel.Text);

            // 5. Enter editing again and confirm with A
            nav.ProcessGamepad(stateNeutral, 220);
            nav.ProcessGamepad(stateA, 250);
            nav.ProcessGamepad(stateNeutral, 280);
            Assert.True(slider.IsEditing);

            // D-Pad Left -> 49
            var stateLeft = new GamepadState(true, 5, GamepadButtons.DPadLeft, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateLeft, 300);
            nav.ProcessGamepad(stateNeutral, 320);
            Assert.Equal(49, slider.Value);

            // Confirm with A
            nav.ProcessGamepad(stateA, 350);
            Assert.False(slider.IsEditing);
            Assert.Equal(49, slider.Value);
            Assert.Equal(49, lastSaved);
        }

        [Fact]
        public void DropdownNavigable_EditNavigateConfirmAndCancel()
        {
            var combo = new ComboBox();
            combo.Items.Add("Automático");
            combo.Items.Add("Jogador 1");
            combo.Items.Add("Jogador 2");
            combo.SelectedIndex = 0;

            var title = new Label();
            var dropdown = new DropdownNavigable("Combo", title, combo);
            var nav = new GamepadNavigationManager();
            nav.RegisterControl(dropdown);

            Assert.False(dropdown.IsEditing);
            Assert.Equal(0, combo.SelectedIndex);

            // 1. Press A to enter selection mode
            var stateA = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);
            var stateNeutral = new GamepadState(true, 2, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateA, 100);
            nav.ProcessGamepad(stateNeutral, 120);

            Assert.True(dropdown.IsEditing);
            Assert.Equal(0, dropdown.TempIndex);

            // 2. Navigate down with D-Pad
            var stateDown = new GamepadState(true, 3, GamepadButtons.DPadDown, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateDown, 150);
            nav.ProcessGamepad(stateNeutral, 180);

            Assert.Equal(1, dropdown.TempIndex);

            // 3. Cancel with B -> returns to index 0
            var stateB = new GamepadState(true, 4, GamepadButtons.B, 0, 0, 0, 0, 0, 0);
            nav.ProcessGamepad(stateB, 200);

            Assert.False(dropdown.IsEditing);
            Assert.Equal(0, combo.SelectedIndex);

            // 4. Enter again, navigate to 2 and confirm with A
            nav.ProcessGamepad(stateNeutral, 220);
            nav.ProcessGamepad(stateA, 250);
            nav.ProcessGamepad(stateNeutral, 280);

            nav.ProcessGamepad(stateDown, 300);
            nav.ProcessGamepad(stateNeutral, 320);
            nav.ProcessGamepad(stateDown, 350);
            nav.ProcessGamepad(stateNeutral, 380);

            Assert.Equal(2, dropdown.TempIndex);

            nav.ProcessGamepad(stateA, 400);
            Assert.False(dropdown.IsEditing);
            Assert.Equal(2, combo.SelectedIndex);
        }

        [Fact]
        public void SliderNavigable_BoundsHaveCleanSpacingBetweenSliders()
        {
            var p = new Panel();
            var l1 = new Label { Top = 172, Height = 18 };
            var t1 = new TrackBar { Top = 190 };
            var v1 = new Label { Top = 194, Height = 20 };
            var slider1 = new SliderNavigable("S1", "Sensibilidade", l1, t1, v1, null);

            var l2 = new Label { Top = 252, Height = 18 };
            var t2 = new TrackBar { Top = 270 };
            var v2 = new Label { Top = 274, Height = 20 };
            var slider2 = new SliderNavigable("S2", "Deadzone", l2, t2, v2, null);

            // Bounds of S1 must be strictly above S2 with a gap
            Assert.True(slider1.Bounds.Bottom < slider2.Bounds.Top);
            Assert.False(slider1.Bounds.IntersectsWith(slider2.Bounds));
            Assert.True(slider2.Bounds.Top - slider1.Bounds.Bottom >= 10);
        }

        [Fact]
        public void FocusOverlay_ExitDialog_GamepadNavigationAndActions()
        {
            var form = new Form();
            var overlay = new FocusOverlayPanel();
            form.Controls.Add(overlay);

            bool closedApp = false;
            bool minimized = false;
            bool cancelled = false;

            overlay.ShowExitDialog(
                form,
                onCloseApp: () => closedApp = true,
                onMinimizeToTray: () => minimized = true,
                onCancel: () => cancelled = true);

            Assert.True(overlay.IsExitDialogOpen);

            var stateNeutral = new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            var stateB = new GamepadState(true, 2, GamepadButtons.B, 0, 0, 0, 0, 0, 0);
            var stateDown = new GamepadState(true, 3, GamepadButtons.DPadDown, 0, 0, 0, 0, 0, 0);
            var stateA = new GamepadState(true, 4, GamepadButtons.A, 0, 0, 0, 0, 0, 0);

            // Test cancel with B
            overlay.ProcessExitGamepad(stateB, 100);
            Assert.True(cancelled);
            Assert.False(overlay.IsExitDialogOpen);

            // Re-open and confirm option 0 (Minimizar)
            cancelled = false;
            overlay.ShowExitDialog(form, () => closedApp = true, () => minimized = true, () => cancelled = true);
            overlay.ProcessExitGamepad(stateNeutral, 120);
            overlay.ProcessExitGamepad(stateA, 150);
            Assert.True(minimized);
            Assert.False(closedApp);
            Assert.False(overlay.IsExitDialogOpen);

            // Re-open, navigate down to option 1 (Fechar) and confirm with A
            minimized = false;
            overlay.ShowExitDialog(form, () => closedApp = true, () => minimized = true, () => cancelled = true);
            overlay.ProcessExitGamepad(stateNeutral, 200);
            overlay.ProcessExitGamepad(stateDown, 220);
            overlay.ProcessExitGamepad(stateNeutral, 240);
            overlay.ProcessExitGamepad(stateA, 260);
            Assert.True(closedApp);
            Assert.False(minimized);
            Assert.False(overlay.IsExitDialogOpen);
        }

        [Fact]
        public void SliderEditing_DirectionTicks_DoNotRetriggerEditingChanged()
        {
            var nav = new GamepadNavigationManager();
            var l = new Label();
            var t = new TrackBar { Minimum = 0, Maximum = 100, Value = 50 };
            var v = new Label { Text = "50" };
            var slider = new SliderNavigable("S", "Sens", l, t, v, null);
            nav.RegisterControl(slider);

            int editingChangedCount = 0;
            nav.EditingChanged += (ctrl, isEditing) => editingChangedCount++;

            var stateA = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);
            var stateNeutral = new GamepadState(true, 2, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            var stateRight = new GamepadState(true, 3, GamepadButtons.DPadRight, 0, 0, 0, 0, 0, 0);

            // 1. Enter edit mode -> exactly 1 event
            nav.ProcessGamepad(stateA, 100);
            nav.ProcessGamepad(stateNeutral, 120);
            Assert.Equal(1, editingChangedCount);
            Assert.True(slider.IsEditing);

            // 2. Multiple directional ticks -> editingChangedCount must REMAIN 1
            for (int i = 0; i < 10; i++)
            {
                nav.ProcessGamepad(stateRight, 150 + (i * 40));
                nav.ProcessGamepad(stateNeutral, 170 + (i * 40));
            }

            Assert.Equal(60, slider.Value);
            Assert.Equal(1, editingChangedCount); // Must still be 1!

            // 3. Confirm with A -> exactly 2 events (one for enter, one for exit)
            nav.ProcessGamepad(stateA, 600);
            Assert.Equal(2, editingChangedCount);
            Assert.False(slider.IsEditing);
        }
    }
}
