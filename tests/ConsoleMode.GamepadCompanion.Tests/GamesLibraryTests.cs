using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Interfaces;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine;
using ConsoleMode.GamepadCompanion.Hardware;
using ConsoleMode.GamepadCompanion.UI.Controls;
using ConsoleMode.GamepadCompanion.UI.Navigation;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public sealed class GamesLibraryTests
    {
        [Fact]
        public void GameEntry_Clone_CopiesAllFieldsCorrectly()
        {
            var original = new GameEntry
            {
                Id = "custom-game-1",
                Name = "Elden Ring",
                LauncherName = "Steam.exe",
                MainExecutable = "eldenring.exe",
                TargetPath = @"C:\Games\eldenring.exe",
                WorkingDirectory = @"C:\Games",
                Arguments = "-novid",
                CoverImagePath = @"C:\Games\cover.png"
            };

            var clone = original.Clone();

            Assert.NotSame(original, clone);
            Assert.Equal(original.Id, clone.Id);
            Assert.Equal(original.Name, clone.Name);
            Assert.Equal(original.LauncherName, clone.LauncherName);
            Assert.Equal(original.MainExecutable, clone.MainExecutable);
            Assert.Equal(original.TargetPath, clone.TargetPath);
            Assert.Equal(original.WorkingDirectory, clone.WorkingDirectory);
            Assert.Equal(original.Arguments, clone.Arguments);
            Assert.Equal(original.CoverImagePath, clone.CoverImagePath);
        }

        [Fact]
        public void GameRepository_SeedsDefaultTurtleWoW_WhenFileDoesNotExist()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_games_{Guid.NewGuid():N}.ini");
            try
            {
                var repo = new GameRepository(tempFile);
                var games = repo.GetAll();

                Assert.Single(games);
                Assert.Equal("turtle-wow", games[0].Id);
                Assert.Equal("Turtle WoW", games[0].Name);
                Assert.Equal("WoW.exe", games[0].MainExecutable);
                Assert.True(File.Exists(tempFile));
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void GameRepository_SavesAndRetrievesGame_Correctly()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_games_{Guid.NewGuid():N}.ini");
            try
            {
                var repo = new GameRepository(tempFile);
                var newGame = new GameEntry
                {
                    Id = "diablo-2",
                    Name = "Diablo II Resurrected",
                    LauncherName = "Battle.net.exe",
                    MainExecutable = "D2R.exe",
                    TargetPath = @"C:\Diablo II\D2R.exe",
                    WorkingDirectory = @"C:\Diablo II",
                    Arguments = "-mod",
                    CoverImagePath = @"C:\Diablo II\cover.jpg"
                };

                repo.Save(newGame);

                var retrieved = repo.GetById("diablo-2");
                Assert.NotNull(retrieved);
                Assert.Equal("Diablo II Resurrected", retrieved.Name);
                Assert.Equal("D2R.exe", retrieved.MainExecutable);
                Assert.Equal(@"C:\Diablo II\D2R.exe", retrieved.TargetPath);

                var all = repo.GetAll();
                Assert.Contains(all, g => g.Id == "diablo-2");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void GameRepository_DeletesGame_RemovesFromIni()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"test_games_{Guid.NewGuid():N}.ini");
            try
            {
                var repo = new GameRepository(tempFile);
                var game = new GameEntry
                {
                    Id = "game-to-delete",
                    Name = "Temporary Game",
                    TargetPath = "game.exe"
                };

                repo.Save(game);
                Assert.NotNull(repo.GetById("game-to-delete"));

                bool deleted = repo.Delete("game-to-delete");
                Assert.True(deleted);
                Assert.Null(repo.GetById("game-to-delete"));
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void WindowTracker_RegisterGameExecutable_AddsToRecognizedGames()
        {
            var tracker = new WindowTracker();

            // Cadastra executável com caminho ou extensão
            tracker.RegisterGameExecutable(@"C:\Program Files\Game\MySuperGame.exe");

            // Não deve disparar exceção
            Assert.False(tracker.IsGameFocused);
        }

        [Fact]
        public void GameLauncher_Throws_WhenGameOrTargetPathEmpty()
        {
            var tracker = new WindowTracker();
            var launcher = new GameLauncher(tracker);

            Assert.Throws<ArgumentNullException>(() => launcher.Launch(null));

            var invalidGame = new GameEntry { TargetPath = "" };
            Assert.Throws<ArgumentException>(() => launcher.Launch(invalidGame));
        }

        [Fact]
        public void GameLauncher_RegistersExecutableInWindowTracker()
        {
            var mockTracker = new MockWindowTracker();
            var launcher = new GameLauncher(mockTracker);

            var game = new GameEntry
            {
                Name = "Portal 2",
                LauncherName = "Steam.exe",
                MainExecutable = "portal2.exe",
                TargetPath = "cmd.exe",
                Arguments = "/c echo test"
            };

            // Inicia um comando leve do Windows (cmd.exe)
            using (var proc = launcher.Launch(game))
            {
                proc?.WaitForExit(1000);
            }

            Assert.Contains("portal2.exe", mockTracker.RegisteredExecutables);
            Assert.Contains("Steam.exe", mockTracker.RegisteredExecutables);
        }

        [Fact]
        public void GamesGridControl_LoadsGamesPlusAddSlot()
        {
            var grid = new GamesGridControl();
            var games = new[]
            {
                new GameEntry { Id = "g1", Name = "Game 1" },
                new GameEntry { Id = "g2", Name = "Game 2" }
            };

            grid.LoadGames(games);

            // 2 jogos + 1 slot [+]
            Assert.Equal(3, grid.TotalCards);
            Assert.Equal(0, grid.FocusedIndex);
        }

        [Fact]
        public void GamesGridControl_DPadNavigation_UpdatesFocusedIndex()
        {
            var grid = new GamesGridControl();
            var games = new[]
            {
                new GameEntry { Id = "g1", Name = "Game 1" },
                new GameEntry { Id = "g2", Name = "Game 2" },
                new GameEntry { Id = "g3", Name = "Game 3" }
            };

            grid.LoadGames(games); // Total 4 cards (0, 1, 2, 3)

            // D-Pad Right -> move para card 1
            var stateRight = new GamepadState(true, 1, GamepadButtons.DPadRight, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateRight, 100);
            Assert.Equal(1, grid.FocusedIndex);

            // Neutral
            var stateNeutral = new GamepadState(true, 2, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateNeutral, 150);

            // D-Pad Left -> volta para card 0
            var stateLeft = new GamepadState(true, 3, GamepadButtons.DPadLeft, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateLeft, 200);
            Assert.Equal(0, grid.FocusedIndex);
        }

        [Fact]
        public void GamesGridControl_ButtonA_OnGameCard_TriggersLaunchRequested()
        {
            var grid = new GamesGridControl();
            var game = new GameEntry { Id = "g1", Name = "Game 1" };
            grid.LoadGames(new[] { game });

            GameEntry launchedGame = null;
            grid.LaunchRequested += g => launchedGame = g;

            // Foco está no índice 0 (Game 1). Pressiona A
            var stateA = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateA, 100);

            Assert.Same(game, launchedGame);
        }

        [Fact]
        public void GamesGridControl_ButtonY_OnGameCard_TriggersConfigureRequested()
        {
            var grid = new GamesGridControl();
            var game = new GameEntry { Id = "g1", Name = "Game 1" };
            grid.LoadGames(new[] { game });

            GameEntry configGame = null;
            grid.ConfigureRequested += g => configGame = g;

            // Foco está no índice 0 (Game 1). Pressiona Y
            var stateY = new GamepadState(true, 1, GamepadButtons.Y, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateY, 100);

            Assert.Same(game, configGame);
        }

        [Fact]
        public void GamesGridControl_ButtonA_OnAddSlot_TriggersAddRequested()
        {
            var grid = new GamesGridControl();
            grid.LoadGames(new GameEntry[0]); // Apenas o slot [+] no índice 0

            bool addTriggered = false;
            grid.AddRequested += () => addTriggered = true;

            var stateA = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateA, 100);

            Assert.True(addTriggered);
        }

        [Fact]
        public void GamesGridControl_ButtonB_TriggersBackRequested()
        {
            var grid = new GamesGridControl();
            grid.LoadGames(new GameEntry[0]);

            bool backTriggered = false;
            grid.BackRequested += () => backTriggered = true;

            var stateB = new GamepadState(true, 1, GamepadButtons.B, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateB, 100);

            Assert.True(backTriggered);
        }

        [Fact]
        public void GamepadNavigationManager_SetFocus_SelectsRegisteredControl()
        {
            var nav = new GamepadNavigationManager();
            var b1 = new Button();
            var b2 = new Button();
            var nb1 = new ButtonNavigable("Btn1", b1);
            var nb2 = new ButtonNavigable("Btn2", b2);

            nav.RegisterControl(nb1);
            nav.RegisterControl(nb2);

            Assert.Equal(0, nav.FocusedIndex);

            nav.SetFocus(nb2);

            Assert.Equal(1, nav.FocusedIndex);
            Assert.Same(nb2, nav.FocusedControl);
        }

        [Fact]
        public void GamesGridControl_ResetInputState_PreventsButtonABleed()
        {
            var grid = new GamesGridControl();
            var game = new GameEntry { Id = "g1", Name = "Game 1" };
            grid.LoadGames(new[] { game });

            GameEntry launchedGame = null;
            grid.LaunchRequested += g => launchedGame = g;

            // Simula transição com botão A já pressionado da tela anterior
            grid.ResetInputState();

            // Enquanto o botão A estiver pressionado, não deve disparar
            var stateA = new GamepadState(true, 1, GamepadButtons.A, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateA, 100);
            grid.ProcessGamepad(stateA, 116);
            Assert.Null(launchedGame);

            // Solta o botão A
            var stateNeutral = new GamepadState(true, 2, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateNeutral, 132);
            Assert.Null(launchedGame);

            // Agora pressiona A novamente de forma intencional: deve disparar!
            grid.ProcessGamepad(stateA, 148);
            Assert.Same(game, launchedGame);
        }

        [Fact]
        public void GamesGridControl_ResetInputState_PreventsButtonYBleed()
        {
            var grid = new GamesGridControl();
            var game = new GameEntry { Id = "g1", Name = "Game 1" };
            grid.LoadGames(new[] { game });

            GameEntry configGame = null;
            grid.ConfigureRequested += g => configGame = g;

            // Simula transição com botão Y já pressionado (ex: atalho Y para abrir biblioteca)
            grid.ResetInputState();

            // Enquanto o botão Y estiver pressionado, não deve abrir configuração
            var stateY = new GamepadState(true, 1, GamepadButtons.Y, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateY, 100);
            grid.ProcessGamepad(stateY, 116);
            Assert.Null(configGame);

            // Solta o botão Y
            var stateNeutral = new GamepadState(true, 2, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateNeutral, 132);
            Assert.Null(configGame);

            // Agora pressiona Y intencionalmente: deve abrir configuração!
            grid.ProcessGamepad(stateY, 148);
            Assert.Same(game, configGame);
        }

        private sealed class MockWindowTracker : IWindowTracker
        {
            public System.Collections.Generic.List<string> RegisteredExecutables = new System.Collections.Generic.List<string>();

            public bool IsGameFocused => false;
            public string ActiveProcessName => string.Empty;
            public event Action<bool> FocusChanged { add { } remove { } }

            public void CheckActiveWindow() { }

            public void RegisterGameExecutable(string executableName)
            {
                RegisteredExecutables.Add(executableName);
            }
        }
    }
}
