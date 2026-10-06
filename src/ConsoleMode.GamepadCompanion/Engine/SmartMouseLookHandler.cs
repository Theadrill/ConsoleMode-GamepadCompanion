using System;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Engine
{
    /// <summary>
    /// Gerencia o Smart Mouse Look no WoW:
    /// Envia F9 Down quando o analógico esquerdo sai da deadzone ou o botão A é pressionado.
    /// Envia F9 Up quando ambos voltam ao repouso.
    /// </summary>
    public sealed class SmartMouseLookHandler
    {
        private readonly IInputSimulator _input;
        private bool _f9Held;

        public SmartMouseLookHandler(IInputSimulator input)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
        }

        public bool IsF9Held => _f9Held;

        public void Update(bool leftStickActive, bool jumpButtonActive)
        {
            bool shouldHoldF9 = leftStickActive || jumpButtonActive;

            if (shouldHoldF9 && !_f9Held)
            {
                _input.KeyDown(VirtualKey.F9);
                _f9Held = true;
            }
            else if (!shouldHoldF9 && _f9Held)
            {
                _input.KeyUp(VirtualKey.F9);
                _f9Held = false;
            }
        }

        public void Reset()
        {
            if (_f9Held)
            {
                _input.KeyUp(VirtualKey.F9);
                _f9Held = false;
            }
        }
    }
}
