using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ConsoleMode.GamepadCompanion.Core.Models;
using ConsoleMode.GamepadCompanion.Engine.Services;
using ConsoleMode.GamepadCompanion.UI.Controls;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class SteamGridUiTests
    {
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage> Handler { get; set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = Handler != null ? Handler(request) : new HttpResponseMessage(HttpStatusCode.NotFound);
                return Task.FromResult(response);
            }
        }

        [Fact]
        public void GameConfigPanel_HasSteamGridDbButton_AndFiresRequestedEvent()
        {
            string capturedName = null;
            using (var panel = new GameConfigPanel())
            {
                panel.SteamGridDbRequested += name => capturedName = name;

                panel.EditGame(new GameEntry { Name = "OctoWoW" });

                // Procura o botão SteamGridDB nos controles
                Button steamGridBtn = FindButtonByText(panel, "SteamGridDB");
                Assert.NotNull(steamGridBtn);

                steamGridBtn.PerformClick();
                Assert.Equal("OctoWoW", capturedName);

                // Testa SetCoverPath
                panel.SetCoverPath("covers/octowow.png");
                TextBox coverTb = FindTextBoxByText(panel, "covers/octowow.png");
                Assert.NotNull(coverTb);
            }
        }

        [Fact]
        public async Task SteamGridApiKeyDialogControl_ValidatesAndEnablesConfirm()
        {
            var handler = new MockHttpMessageHandler
            {
                Handler = req => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"success\":true,\"data\":{\"id\":1}}")
                }
            };

            using (var httpClient = new HttpClient(handler))
            using (var service = new SteamGridDbService(httpClient))
            using (var dialog = new SteamGridApiKeyDialogControl(service))
            {
                string confirmedKey = null;
                dialog.KeyConfirmed += k => confirmedKey = k;

                dialog.SetInitialKey("valid_token_123");
                await dialog.ValidateKeyLiveAsync("valid_token_123");

                Button btnConfirm = FindButtonByText(dialog, "[A] Confirmar");
                Assert.NotNull(btnConfirm);
                Assert.True(btnConfirm.Enabled);

                btnConfirm.PerformClick();
                Assert.Equal("valid_token_123", confirmedKey);
            }
        }

        [Fact(Timeout = 5000)]
        public async Task SteamGridDbBrowserControl_TransitionsFromGameListToCoverGallery_AndSelects()
        {
            var handler = new MockHttpMessageHandler
            {
                Handler = req =>
                {
                    string url = req.RequestUri.ToString();
                    if (url.Contains("/search/autocomplete"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"success\":true,\"data\":[{\"id\":100,\"name\":\"World of Warcraft\"}]}")
                        };
                    }
                    if (url.Contains("/grids/game"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"success\":true,\"data\":[{\"id\":500,\"url\":\"https://example.com/cover.png\",\"mime\":\"image/png\"}]}")
                        };
                    }
                    // Download
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 })
                    };
                }
            };

            using (var httpClient = new HttpClient(handler))
            using (var service = new SteamGridDbService(httpClient))
            using (var browser = new SteamGridDbBrowserControl(service))
            {
                bool backRequested = false;
                browser.BackToConfigRequested += () => backRequested = true;

                // Inicia busca
                await browser.StartSearchAsync("World of Warcraft", "api_token");

                Assert.Equal(SteamGridBrowserMode.GameList, browser.CurrentMode);

                // Seleciona o jogo
                await browser.SelectGameAsync(new SteamGridGame { id = 100, name = "World of Warcraft" });

                Assert.Equal(SteamGridBrowserMode.CoverGallery, browser.CurrentMode);

                // Pressiona Gamepad B: deve voltar para a lista de jogos (Tela 1)
                var stateB = new GamepadState(
                    isConnected: true,
                    packetNumber: 1,
                    buttons: GamepadButtons.B,
                    leftTrigger: 0,
                    rightTrigger: 0,
                    leftThumbX: 0,
                    leftThumbY: 0,
                    rightThumbX: 0,
                    rightThumbY: 0
                );

                browser.ProcessGamepad(stateB, 1000);
                Assert.Equal(SteamGridBrowserMode.GameList, browser.CurrentMode);

                // Pressiona Gamepad B novamente na Tela 1: deve pedir para voltar à configuração
                browser.ResetInputState();
                browser.ProcessGamepad(stateB, 2000);
                Assert.True(backRequested);
            }
        }

        [Fact(Timeout = 5000)]
        public async Task SteamGridDbBrowserControl_WhenNoGamesFound_GamepadBXY_AreResponsive()
        {
            var handler = new MockHttpMessageHandler
            {
                Handler = req => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"success\":true,\"data\":[]}")
                }
            };

            using (var httpClient = new HttpClient(handler))
            using (var service = new SteamGridDbService(httpClient))
            using (var browser = new SteamGridDbBrowserControl(service))
            {
                bool backRequested = false;
                bool newSearchRequested = false;
                bool changeKeyRequested = false;

                browser.BackToConfigRequested += () => backRequested = true;
                browser.NewSearchRequested += () => newSearchRequested = true;
                browser.ChangeApiKeyRequested += () => changeKeyRequested = true;

                await browser.StartSearchAsync("NonExistentGame12345", "test_key");

                // Botão B deve funcionar mesmo sem jogos
                var stateB = new GamepadState(true, 1, GamepadButtons.B, 0, 0, 0, 0, 0, 0);
                browser.ProcessGamepad(stateB, 100);
                Assert.True(backRequested);

                // Botão X deve funcionar
                browser.ResetInputState();
                var stateX = new GamepadState(true, 2, GamepadButtons.X, 0, 0, 0, 0, 0, 0);
                browser.ProcessGamepad(stateX, 200);
                Assert.True(newSearchRequested);

                // Botão Y deve funcionar
                browser.ResetInputState();
                var stateY = new GamepadState(true, 3, GamepadButtons.Y, 0, 0, 0, 0, 0, 0);
                browser.ProcessGamepad(stateY, 300);
                Assert.True(changeKeyRequested);
            }
        }

        [Fact(Timeout = 5000)]
        public async Task SteamGridDbBrowserControl_WhenNoCoversFound_GamepadB_ReturnsToGameList()
        {
            var handler = new MockHttpMessageHandler
            {
                Handler = req =>
                {
                    string url = req.RequestUri.ToString();
                    if (url.Contains("/search/autocomplete"))
                    {
                        return new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"success\":true,\"data\":[{\"id\":42,\"name\":\"ObscureGame\"}]}")
                        };
                    }
                    // Grids vazias
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"success\":true,\"data\":[]}")
                    };
                }
            };

            using (var httpClient = new HttpClient(handler))
            using (var service = new SteamGridDbService(httpClient))
            using (var browser = new SteamGridDbBrowserControl(service))
            {
                await browser.StartSearchAsync("ObscureGame", "test_key");
                await browser.SelectGameAsync(new SteamGridGame { id = 42, name = "ObscureGame" });

                Assert.Equal(SteamGridBrowserMode.CoverGallery, browser.CurrentMode);

                // Botão B deve voltar para GameList mesmo sem capas
                var stateB = new GamepadState(true, 1, GamepadButtons.B, 0, 0, 0, 0, 0, 0);
                browser.ProcessGamepad(stateB, 100);
                Assert.Equal(SteamGridBrowserMode.GameList, browser.CurrentMode);
            }
        }

        [Fact(Timeout = 5000)]
        public async Task SteamGridDbBrowserControl_CoverGallery_GamepadNavigation_ScrollsViewport()
        {
            var coversList = new System.Text.StringBuilder();
            coversList.Append("{\"success\":true,\"data\":[");
            for (int i = 0; i < 24; i++)
            {
                if (i > 0) coversList.Append(",");
                coversList.Append($"{{\"id\":{i + 1},\"url\":\"https://example.com/cover{i}.png\",\"mime\":\"image/png\"}}");
            }
            coversList.Append("]}");

            var handler = new MockHttpMessageHandler
            {
                Handler = req => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(coversList.ToString())
                }
            };

            using (var httpClient = new HttpClient(handler))
            using (var service = new SteamGridDbService(httpClient))
            using (var browser = new SteamGridDbBrowserControl(service))
            {
                browser.SetBounds(0, 0, 600, 400);

                await browser.StartSearchAsync("ManyCoversGame", "test_key");
                await browser.SelectGameAsync(new SteamGridGame { id = 99, name = "ManyCoversGame" });
                Assert.Equal(SteamGridBrowserMode.CoverGallery, browser.CurrentMode);
                Assert.Equal(0, browser.FocusedCoverIndex);

                // Navega para baixo repetidamente para linhas inferiores
                for (int step = 1; step <= 8; step++)
                {
                    browser.ResetInputState();
                    var stateDown = new GamepadState(true, (uint)step, GamepadButtons.DPadDown, 0, 0, 0, 0, 0, 0);
                    browser.ProcessGamepad(stateDown, step * 100);
                }

                Assert.True(browser.FocusedCoverIndex > 0);
            }
        }

        [Fact]
        public void SteamGridDbBrowserControl_IsHiddenByDefault()
        {
            using (var browser = new SteamGridDbBrowserControl())
            {
                Assert.False(browser.Visible);
            }
        }

        [Fact]
        public void FocusOverlayPanel_SteamGridApiKeyDialog_ShowsAndHidesCleanly()
        {
            using (var form = new Form { Width = 800, Height = 600 })
            using (var overlay = new FocusOverlayPanel())
            {
                form.Controls.Add(overlay);
                Assert.False(overlay.Visible);
                Assert.False(overlay.IsSteamGridApiKeyDialogOpen);

                overlay.ShowSteamGridApiKeyDialog(form, "test_key", key => { }, () => { });

                Assert.True(overlay.IsSteamGridApiKeyDialogOpen);
                Assert.Equal(form.ClientSize.Width, overlay.Width);
                Assert.Equal(form.ClientSize.Height, overlay.Height);

                overlay.CloseSteamGridApiKeyDialog();

                Assert.False(overlay.IsSteamGridApiKeyDialogOpen);
            }
        }

        private static Button FindButtonByText(Control root, string text)
        {
            if (root is Button b && b.Text.Contains(text)) return b;
            foreach (Control child in root.Controls)
            {
                var found = FindButtonByText(child, text);
                if (found != null) return found;
            }
            return null;
        }

        private static TextBox FindTextBoxByText(Control root, string text)
        {
            if (root is TextBox tb && tb.Text == text) return tb;
            foreach (Control child in root.Controls)
            {
                var found = FindTextBoxByText(child, text);
                if (found != null) return found;
            }
            return null;
        }
    }
}
