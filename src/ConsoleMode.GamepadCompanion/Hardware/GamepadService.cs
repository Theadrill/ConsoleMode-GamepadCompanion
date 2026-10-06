using System;
using System.Diagnostics;
using System.Threading;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>Polling de alta frequência do XInput em thread dedicada.</summary>
    public sealed class GamepadService : IGamepadService
    {
        private const int PollIntervalMs = 4; // ~250Hz

        private readonly object _gate = new object();
        private Thread _thread;
        private volatile bool _running;
        private GamepadState _current = GamepadState.Disconnected;
        private int _activeSlot = -1;
        private readonly bool[] _slotConnected = new bool[XInputNative.MaxControllers];

        public int SelectedSlot { get; set; } = -1;

        public int ActiveSlot => _activeSlot;

        public GamepadState CurrentState
        {
            get { lock (_gate) { return _current; } }
        }

        public event Action<GamepadState> StateUpdated;
        public event Action<int, bool> ConnectionChanged;

        public bool IsSlotConnected(int slot)
        {
            return slot >= 0 && slot < XInputNative.MaxControllers && _slotConnected[slot];
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _thread = new Thread(PollLoop) { IsBackground = true, Name = "GamepadPoller" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            var t = _thread;
            if (t != null && t.IsAlive) t.Join(500);
            _thread = null;
        }

        public void Dispose() => Stop();

        private void PollLoop()
        {
            var sw = Stopwatch.StartNew();
            long next = 0;

            while (_running)
            {
                PollOnce();

                next += PollIntervalMs;
                long wait = next - sw.ElapsedMilliseconds;
                if (wait > 0) Thread.Sleep((int)wait);
                else next = sw.ElapsedMilliseconds;
            }
        }

        private void PollOnce()
        {
            // Atualiza conectividade de todos os slots (para o dropdown da UI).
            var raw = new XInputNative.XInputState[XInputNative.MaxControllers];
            for (int i = 0; i < XInputNative.MaxControllers; i++)
            {
                bool connected = XInputNative.GetState((uint)i, out raw[i]) == XInputNative.ErrorSuccess;
                _slotConnected[i] = connected;
            }

            int slot = ResolveSlot();
            if (slot != _activeSlot)
            {
                int old = _activeSlot;
                _activeSlot = slot;
                if (old >= 0) ConnectionChanged?.Invoke(old, false);
                if (slot >= 0) ConnectionChanged?.Invoke(slot, true);
            }

            GamepadState state = GamepadState.Disconnected;
            if (slot >= 0)
            {
                var g = raw[slot].Gamepad;
                state = new GamepadState(
                    true, raw[slot].dwPacketNumber, (GamepadButtons)g.wButtons,
                    g.bLeftTrigger, g.bRightTrigger,
                    g.sThumbLX, g.sThumbLY, g.sThumbRX, g.sThumbRY);
            }

            lock (_gate) { _current = state; }
            StateUpdated?.Invoke(state);
        }

        private int ResolveSlot()
        {
            int sel = SelectedSlot;
            if (sel >= 0 && sel < XInputNative.MaxControllers) return _slotConnected[sel] ? sel : -1;

            for (int i = 0; i < XInputNative.MaxControllers; i++)
                if (_slotConnected[i]) return i;
            return -1;
        }
    }
}
