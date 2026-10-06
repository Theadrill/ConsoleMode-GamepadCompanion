using System;
using System.Diagnostics;
using ConsoleMode.GamepadCompanion.Hardware;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class SingleInstanceGuardTests
    {
        [Fact]
        public void EnsureSingleInstance_FirstInstance_ReturnsTrueWithoutPrompt()
        {
            string uniqueMutex = @"Local\TestMutex_" + Guid.NewGuid().ToString("N");
            bool promptCalled = false;

            using (var guard = new SingleInstanceGuard(
                uniqueMutex,
                () =>
                {
                    promptCalled = true;
                    return false;
                },
                pid => new Process[0],
                p => { }))
            {
                bool canRun = guard.EnsureSingleInstance();

                Assert.True(canRun);
                Assert.False(promptCalled);
            }
        }

        [Fact]
        public void EnsureSingleInstance_ExistingInstanceAndUserSelectsNo_ReturnsFalseWithoutKill()
        {
            string uniqueMutex = @"Local\TestMutex_" + Guid.NewGuid().ToString("N");
            bool promptCalled = false;
            bool killCalled = false;

            // Instância 1 ocupa o mutex
            using (var firstGuard = new SingleInstanceGuard(
                uniqueMutex,
                () => false,
                pid => new Process[0],
                p => { }))
            {
                Assert.True(firstGuard.EnsureSingleInstance());

                // Instância 2 tenta rodar e usuário diz "Não"
                using (var secondGuard = new SingleInstanceGuard(
                    uniqueMutex,
                    () =>
                    {
                        promptCalled = true;
                        return false;
                    },
                    pid => new Process[0],
                    p => { killCalled = true; }))
                {
                    bool canRun = secondGuard.EnsureSingleInstance();

                    Assert.False(canRun);
                    Assert.True(promptCalled);
                    Assert.False(killCalled);
                }
            }
        }

        [Fact]
        public void EnsureSingleInstance_ExistingInstanceAndUserSelectsYes_CallsKillAndReturnsTrue()
        {
            string uniqueMutex = @"Local\TestMutex_" + Guid.NewGuid().ToString("N");
            bool promptCalled = false;
            int killCount = 0;

            // Simula processo existente encontrado pela lista
            var dummyProcess = Process.GetCurrentProcess();

            using (var guard = new SingleInstanceGuard(
                uniqueMutex,
                () =>
                {
                    promptCalled = true;
                    return true;
                },
                pid => new[] { dummyProcess },
                p => { killCount++; }))
            {
                bool canRun = guard.EnsureSingleInstance();

                Assert.True(canRun);
                Assert.True(promptCalled);
                Assert.Equal(1, killCount);
            }
        }
    }
}
