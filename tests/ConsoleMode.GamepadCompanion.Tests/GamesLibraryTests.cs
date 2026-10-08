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

        [Fact]
        public void FuzzySearchEngine_MatchesExactAndPartialAndTypo()
        {
            var game = new GameEntry
            {
                Id = "twow",
                Name = "Turtle WoW",
                MainExecutable = "WoW.exe"
            };

            // Prefixo parcial
            Assert.True(ConsoleMode.GamepadCompanion.Engine.Search.FuzzySearchEngine.IsMatch("turt", game, out int s1));
            Assert.True(s1 > 0);

            // Substring e sigla
            Assert.True(ConsoleMode.GamepadCompanion.Engine.Search.FuzzySearchEngine.IsMatch("wow", game, out int s2));
            Assert.True(s2 > 0);

            // Erro de digitação / typo ("turtl wow")
            Assert.True(ConsoleMode.GamepadCompanion.Engine.Search.FuzzySearchEngine.IsMatch("turtl wow", game, out int s3));
            Assert.True(s3 > 0);

            // Match pelo executável
            Assert.True(ConsoleMode.GamepadCompanion.Engine.Search.FuzzySearchEngine.IsMatch("wow.exe", game, out int s4));
            Assert.True(s4 > 0);

            // Busca não correspondente
            Assert.False(ConsoleMode.GamepadCompanion.Engine.Search.FuzzySearchEngine.IsMatch("xyz123nonexistent", game, out int s5));
            Assert.Equal(0, s5);
        }

        [Fact]
        public void FuzzySearchEngine_Filter_OrdersByRelevance()
        {
            var g1 = new GameEntry { Id = "1", Name = "Warcraft III", MainExecutable = "war3.exe" };
            var g2 = new GameEntry { Id = "2", Name = "Turtle WoW", MainExecutable = "WoW.exe" };
            var g3 = new GameEntry { Id = "3", Name = "Cyberpunk 2077", MainExecutable = "cyberpunk.exe" };

            var list = new[] { g2, g3, g1 };
            var results = System.Linq.Enumerable.ToList(ConsoleMode.GamepadCompanion.Engine.Search.FuzzySearchEngine.Filter(list, "warcraft"));

            Assert.Single(results);
            Assert.Equal("1", results[0].Id);

            // Busca com erro "warcrft"
            var resultsTypo = System.Linq.Enumerable.ToList(ConsoleMode.GamepadCompanion.Engine.Search.FuzzySearchEngine.Filter(list, "warcrft"));
            Assert.Single(resultsTypo);
            Assert.Equal("1", resultsTypo[0].Id);
        }

        [Fact]
        public void GamesGridControl_SearchFilter_LiveSearchAndEmptyState()
        {
            var grid = new GamesGridControl();
            var g1 = new GameEntry { Id = "1", Name = "Turtle WoW", MainExecutable = "WoW.exe" };
            var g2 = new GameEntry { Id = "2", Name = "Diablo II", MainExecutable = "D2R.exe" };

            grid.LoadGames(new[] { g1, g2 });

            // Inicial: 2 jogos + 1 slot [+] = 3
            Assert.Equal(3, grid.TotalCards);

            // Foca a busca e filtra
            grid.FocusSearch();
            Assert.True(grid.IsSearchFocused);

            // Simula digitação "Turtle"
            var searchControl = (SearchBarControl)grid.Controls[1].Controls[2];
            searchControl.SearchText = "Turtle";

            // Live search filtra para apenas Turtle WoW (sem slot [+])
            Assert.Equal(1, grid.TotalCards);

            // Busca que não encontra nada
            searchControl.SearchText = "NonExistentGame999";
            Assert.Equal(0, grid.TotalCards);

            // Processa botão B do controle: deve limpar a busca e restaurar os cards
            var stateB = new GamepadState(true, 1, GamepadButtons.B, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateB, 100);

            Assert.Equal(string.Empty, grid.SearchText);
            Assert.Equal(3, grid.TotalCards);
        }

        [Fact]
        public void GamesGridControl_GamepadButtonX_FocusesSearch_And_DownExits()
        {
            var grid = new GamesGridControl();
            var g1 = new GameEntry { Id = "1", Name = "Turtle WoW", MainExecutable = "WoW.exe" };
            grid.LoadGames(new[] { g1 });

            grid.ResetInputState();
            Assert.False(grid.IsSearchFocused);

            // 1. Apertar X foca a barra de pesquisa
            var stateNeutral = new GamepadState(true, 1, GamepadButtons.None, 0, 0, 0, 0, 0, 0);
            var stateX = new GamepadState(true, 2, GamepadButtons.X, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateNeutral, 100);
            grid.ProcessGamepad(stateX, 120);

            Assert.True(grid.IsSearchFocused);

            // 2. D-Pad Down sai da busca e retorna aos cards: deve focar no card 0
            var stateDown = new GamepadState(true, 3, GamepadButtons.DPadDown, 0, 0, 0, 0, 0, 0);
            grid.ProcessGamepad(stateNeutral, 200);
            grid.ProcessGamepad(stateDown, 220);

            Assert.False(grid.IsSearchFocused);
            Assert.Equal(0, grid.FocusedIndex);

            // 3. Continuar segurando D-Pad Down nos ticks seguintes NÃO deve pular para o card 1!
            grid.ProcessGamepad(stateDown, 236);
            grid.ProcessGamepad(stateDown, 252);
            grid.ProcessGamepad(stateDown, 300);
            Assert.Equal(0, grid.FocusedIndex);

            // 4. Apenas após soltar e apertar novamente para baixo é que move para o card 1
            grid.ProcessGamepad(stateNeutral, 320);
            grid.ProcessGamepad(stateDown, 340);
            Assert.Equal(1, grid.FocusedIndex);
        }

        [Fact]
        public void GameCoverCard_SingleClick_OnConfigureBadge_FiresConfigureClicked()
        {
            var game = new GameEntry { Id = "test-game", Name = "Portal" };
            using var card = new ConsoleMode.GamepadCompanion.UI.Controls.GameCoverCard(game);

            GameEntry configured = null;
            card.ConfigureClicked += g => configured = g;

            var cfgRect = card.ConfigBadgeRect;
            card.SimulateMouseClick(MouseButtons.Left, cfgRect.X + 10, cfgRect.Y + 10);

            Assert.Same(game, configured);
        }

        [Fact]
        public void GameCoverCard_SingleClick_OnLaunchBadge_FiresLaunchClicked()
        {
            var game = new GameEntry { Id = "test-game", Name = "Portal" };
            using var card = new ConsoleMode.GamepadCompanion.UI.Controls.GameCoverCard(game);

            GameEntry launched = null;
            card.LaunchClicked += g => launched = g;

            var launchRect = card.LaunchBadgeRect;
            card.SimulateMouseClick(MouseButtons.Left, launchRect.X + 10, launchRect.Y + 10);

            Assert.Same(game, launched);
        }

        [Fact]
        public void GameCoverCard_SingleClick_OnCardBody_FiresLaunchClicked()
        {
            var game = new GameEntry { Id = "test-game", Name = "Portal" };
            using var card = new ConsoleMode.GamepadCompanion.UI.Controls.GameCoverCard(game);

            GameEntry launched = null;
            card.LaunchClicked += g => launched = g;

            // Clica na área superior da capa (fora dos botões)
            card.SimulateMouseClick(MouseButtons.Left, 20, 20);

            Assert.Same(game, launched);
        }

        [Fact]
        public void GameCoverCard_SingleClick_OnAddSlot_FiresAddClicked()
        {
            using var card = new ConsoleMode.GamepadCompanion.UI.Controls.GameCoverCard(null);

            bool addFired = false;
            card.AddClicked += () => addFired = true;

            card.SimulateMouseClick(MouseButtons.Left, 50, 50);

            Assert.True(addFired);
        }

        [Fact]
        public void GameCoverCard_RightClick_FiresConfigureClicked()
        {
            var game = new GameEntry { Id = "test-game", Name = "Portal" };
            using var card = new ConsoleMode.GamepadCompanion.UI.Controls.GameCoverCard(game);

            GameEntry configured = null;
            card.ConfigureClicked += g => configured = g;

            card.SimulateMouseClick(MouseButtons.Right, 50, 50);

            Assert.Same(game, configured);
        }

        [Theory]
        [InlineData("\"C:\\Games\\OctoWoW\\OctoWoW.exe\"", "OctoWoW.exe", "OctoWoW")]
        [InlineData("C:/Games/OctoWoW/OctoWoW.exe", "OctoWoW.exe", "OctoWoW")]
        [InlineData("\"C:\\Games\\OctoWoW\\OctoWoW.exe\" -console", "OctoWoW.exe", "OctoWoW")]
        [InlineData("game.exe", "game.exe", "game")]
        [InlineData("\"D:\\Program Files (x86)\\Game\\Launcher.bat\"", "Launcher.bat", "Launcher")]
        public void GameConfigPanel_PathHelpers_SafelyExtractFileAndName(string rawPath, string expectedFile, string expectedName)
        {
            string file = ConsoleMode.GamepadCompanion.UI.Controls.GameConfigPanel.ExtractFileNameSafe(rawPath);
            string name = ConsoleMode.GamepadCompanion.UI.Controls.GameConfigPanel.ExtractNameWithoutExtensionSafe(rawPath);

            Assert.Equal(expectedFile, file);
            Assert.Equal(expectedName, name);
        }

        [Fact]
        public void GameConfigPanel_AutoFillsExecutableFromTargetPath_OnSave()
        {
            using var panel = new ConsoleMode.GamepadCompanion.UI.Controls.GameConfigPanel();
            panel.EditGame(null); // Novo jogo

            // Acessa via reflection os campos de texto para simular digitação
            var txtName = (TextBox)typeof(ConsoleMode.GamepadCompanion.UI.Controls.GameConfigPanel)
                .GetField("_txtName", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(panel);
            var txtTarget = (TextBox)typeof(ConsoleMode.GamepadCompanion.UI.Controls.GameConfigPanel)
                .GetField("_txtTargetPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(panel);
            var txtExe = (TextBox)typeof(ConsoleMode.GamepadCompanion.UI.Controls.GameConfigPanel)
                .GetField("_txtExecutable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(panel);

            txtName.Text = "OctoWoW";
            txtTarget.Text = "\"C:\\Jogos\\OctoWoW\\OctoWoW.exe\"";
            txtExe.Text = string.Empty; // Deixa o executável vazio!

            GameEntry saved = null;
            panel.SaveRequested += g => saved = g;

            // Invoca OnSaveClicked via reflection
            var onSaveMethod = typeof(ConsoleMode.GamepadCompanion.UI.Controls.GameConfigPanel)
                .GetMethod("OnSaveClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            onSaveMethod.Invoke(panel, null);

            Assert.NotNull(saved);
            Assert.Equal("OctoWoW", saved.Name);
            Assert.Equal("OctoWoW.exe", saved.MainExecutable);
            Assert.Equal("C:\\Jogos\\OctoWoW\\OctoWoW.exe", saved.TargetPath);
            Assert.Equal("C:\\Jogos\\OctoWoW", saved.WorkingDirectory);
        }

        [Fact]
        public void GameConfigPanel_ValidationFailed_WhenMissingRequiredFields()
        {
            using var panel = new ConsoleMode.GamepadCompanion.UI.Controls.GameConfigPanel();
            panel.EditGame(null);

            bool validationFired = false;
            panel.ValidationFailed += (title, msg) => validationFired = true;

            var onSaveMethod = typeof(ConsoleMode.GamepadCompanion.UI.Controls.GameConfigPanel)
                .GetMethod("OnSaveClicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            onSaveMethod.Invoke(panel, null);

            Assert.True(validationFired);
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
