using System.Collections.Generic;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class SmartMouseLookHandlerTests
    {
        private class FakeInputSimulator : IInputSimulator
        {
            public List<VirtualKey> DownKeys { get; } = new List<VirtualKey>();
            public List<VirtualKey> UpKeys { get; } = new List<VirtualKey>();

            public void KeyDown(VirtualKey key) => DownKeys.Add(key);
            public void KeyUp(VirtualKey key) => UpKeys.Add(key);
            public void MouseButtonDown(MouseButton button) { }
            public void MouseButtonUp(MouseButton button) { }
            public void MouseMoveRelative(int dx, int dy) { }
        }

        [Fact]
        public void StickMoves_SendsF9DownOnce()
        {
            var fake = new FakeInputSimulator();
            var handler = new SmartMouseLookHandler(fake);

            handler.Update(leftStickActive: true, jumpButtonActive: false);
            Assert.True(handler.IsF9Held);
            Assert.Contains(VirtualKey.F9, fake.DownKeys);
            Assert.Empty(fake.UpKeys);

            // Segunda chamada ainda com o stick ativo não deve duplicar KeyDown
            handler.Update(leftStickActive: true, jumpButtonActive: false);
            Assert.Single(fake.DownKeys);
        }

        [Fact]
        public void StickReturnsToNeutral_SendsF9Up()
        {
            var fake = new FakeInputSimulator();
            var handler = new SmartMouseLookHandler(fake);

            handler.Update(leftStickActive: true, jumpButtonActive: false);
            handler.Update(leftStickActive: false, jumpButtonActive: false);

            Assert.False(handler.IsF9Held);
            Assert.Contains(VirtualKey.F9, fake.UpKeys);
        }

        [Fact]
        public void JumpButton_ActivatesF9()
        {
            var fake = new FakeInputSimulator();
            var handler = new SmartMouseLookHandler(fake);

            handler.Update(leftStickActive: false, jumpButtonActive: true);
            Assert.True(handler.IsF9Held);
            Assert.Contains(VirtualKey.F9, fake.DownKeys);

            handler.Update(leftStickActive: false, jumpButtonActive: false);
            Assert.False(handler.IsF9Held);
            Assert.Contains(VirtualKey.F9, fake.UpKeys);
        }

        [Fact]
        public void StickAndJumpTogether_OnlyReleasesF9WhenBothNeutral()
        {
            var fake = new FakeInputSimulator();
            var handler = new SmartMouseLookHandler(fake);

            // Ativa ambos
            handler.Update(leftStickActive: true, jumpButtonActive: true);
            Assert.True(handler.IsF9Held);

            // Solta stick, mas mantém pulo
            handler.Update(leftStickActive: false, jumpButtonActive: true);
            Assert.True(handler.IsF9Held);
            Assert.Empty(fake.UpKeys);

            // Solta pulo também
            handler.Update(leftStickActive: false, jumpButtonActive: false);
            Assert.False(handler.IsF9Held);
            Assert.Single(fake.UpKeys);
        }
    }
}
