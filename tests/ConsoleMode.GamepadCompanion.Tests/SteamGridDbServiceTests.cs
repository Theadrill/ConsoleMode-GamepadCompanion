using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ConsoleMode.GamepadCompanion.Engine.Services;
using Xunit;

namespace ConsoleMode.GamepadCompanion.Tests
{
    public class SteamGridDbServiceTests
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
        public async Task ValidateApiKeyAsync_ReturnsTrue_OnHttp200()
        {
            string capturedAuth = null;
            var handler = new MockHttpMessageHandler
            {
                Handler = req =>
                {
                    capturedAuth = req.Headers.Authorization?.ToString();
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"success\":true,\"data\":{\"id\":1,\"name\":\"Half-Life\"}}")
                    };
                }
            };

            using (var httpClient = new HttpClient(handler))
            using (var service = new SteamGridDbService(httpClient))
            {
                bool isValid = await service.ValidateApiKeyAsync("my_secret_token_123");

                Assert.True(isValid);
                Assert.Equal("Bearer my_secret_token_123", capturedAuth);
            }
        }

        [Fact]
        public async Task ValidateApiKeyAsync_ReturnsFalse_OnHttp401()
        {
            var handler = new MockHttpMessageHandler
            {
                Handler = req => new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"success\":false,\"errors\":[\"Unauthorized\"]}")
                }
            };

            using (var httpClient = new HttpClient(handler))
            using (var service = new SteamGridDbService(httpClient))
            {
                bool isValid = await service.ValidateApiKeyAsync("invalid_token");

                Assert.False(isValid);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ValidateApiKeyAsync_ReturnsFalse_OnBlankKey(string key)
        {
            using (var service = new SteamGridDbService())
            {
                bool isValid = await service.ValidateApiKeyAsync(key);
                Assert.False(isValid);
            }
        }

        [Fact]
        public async Task SearchGamesAsync_ParsesResultsAndEncodesTerm()
        {
            string capturedUrl = null;
            var handler = new MockHttpMessageHandler
            {
                Handler = req =>
                {
                    capturedUrl = req.RequestUri.OriginalString;
                    string json = @"{
                        ""success"": true,
                        ""data"": [
                            { ""id"": 2254, ""name"": ""World of Warcraft"", ""types"": [""battle.net""], ""verified"": true },
                            { ""id"": 3050, ""name"": ""World of Warcraft: Classic"", ""types"": [""battle.net""], ""verified"": false }
                        ]
                    }";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json)
                    };
                }
            };

            using (var httpClient = new HttpClient(handler))
            using (var service = new SteamGridDbService(httpClient))
            {
                var games = await service.SearchGamesAsync("World of Warcraft", "api_key_abc");

                Assert.NotNull(games);
                Assert.Equal(2, games.Count);
                Assert.Equal(2254, games[0].id);
                Assert.Equal("World of Warcraft", games[0].name);
                Assert.True(games[0].verified);
                Assert.Equal("World of Warcraft: Classic", games[1].name);
                Assert.Contains("World%20of%20Warcraft", capturedUrl);
            }
        }

        [Fact]
        public async Task SearchGamesAsync_ReturnsEmptyList_OnNotFoundOrError()
        {
            var handler = new MockHttpMessageHandler
            {
                Handler = req => new HttpResponseMessage(HttpStatusCode.NotFound)
            };

            using (var httpClient = new HttpClient(handler))
            using (var service = new SteamGridDbService(httpClient))
            {
                var games = await service.SearchGamesAsync("UnknownGame", "api_key_abc");

                Assert.NotNull(games);
                Assert.Empty(games);
            }
        }

        [Fact]
        public async Task GetGameGridsAsync_EnforcesDimensionsAndMimesFiltering()
        {
            string capturedUrl = null;
            var handler = new MockHttpMessageHandler
            {
                Handler = req =>
                {
                    capturedUrl = req.RequestUri.ToString();
                    string json = @"{
                        ""success"": true,
                        ""data"": [
                            {
                                ""id"": 5001,
                                ""score"": 25,
                                ""style"": ""alternate"",
                                ""width"": 600,
                                ""height"": 900,
                                ""mime"": ""image/png"",
                                ""url"": ""https://cdn.steamgriddb.com/grid/wow_hires.png"",
                                ""thumb"": ""https://cdn.steamgriddb.com/thumb/wow_hires.png"",
                                ""author"": { ""id"": ""99"", ""name"": ""Artist1"" }
                            }
                        ]
                    }";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json)
                    };
                }
            };

            using (var httpClient = new HttpClient(handler))
            using (var service = new SteamGridDbService(httpClient))
            {
                var grids = await service.GetGameGridsAsync(2254, "api_key_abc");

                Assert.NotNull(grids);
                Assert.Single(grids);
                Assert.Equal(5001, grids[0].id);
                Assert.Equal("https://cdn.steamgriddb.com/grid/wow_hires.png", grids[0].url);
                Assert.Equal("Artist1", grids[0].author?.name);

                Assert.Contains("dimensions=600x900", capturedUrl);
                Assert.Contains("mimes=image/png,image/jpeg", capturedUrl);
            }
        }

        [Fact]
        public async Task DownloadCoverAsync_WritesStreamToDisk()
        {
            byte[] fakeImageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }; // PNG header
            var handler = new MockHttpMessageHandler
            {
                Handler = req => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(fakeImageBytes)
                }
            };

            string tempDir = Path.Combine(Path.GetTempPath(), "steamgrid_test_" + Guid.NewGuid().ToString("N"));
            string destinationFile = Path.Combine(tempDir, "cover.png");

            try
            {
                using (var httpClient = new HttpClient(handler))
                using (var service = new SteamGridDbService(httpClient))
                {
                    string resultPath = await service.DownloadCoverAsync("https://cdn.steamgriddb.com/grid/image.png", destinationFile);

                    Assert.Equal(destinationFile, resultPath);
                    Assert.True(File.Exists(destinationFile));
                    byte[] downloaded = File.ReadAllBytes(destinationFile);
                    Assert.Equal(fakeImageBytes, downloaded);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
