using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Engine.Services
{
    /// <summary>
    /// Serviço de integração com a API v2 do SteamGridDB (https://www.steamgriddb.com).
    /// Fornece validação de credencial, pesquisa de títulos, consulta de capas verticais e download.
    /// </summary>
    public sealed class SteamGridDbService : IDisposable
    {
        private const string BaseApiUrl = "https://www.steamgriddb.com/api/v2";
        private const string UserAgent = "ConsoleMode-GamepadCompanion/1.0";

        private static readonly HttpClient DefaultClient = CreateConfiguredClient();
        private readonly HttpClient _client;
        private readonly bool _disposeClient;
        private readonly JavaScriptSerializer _serializer;

        public SteamGridDbService(HttpClient client = null)
        {
            if (client != null)
            {
                _client = client;
                _disposeClient = false;
            }
            else
            {
                _client = DefaultClient;
                _disposeClient = false;
            }

            _serializer = new JavaScriptSerializer
            {
                MaxJsonLength = 10 * 1024 * 1024 // 10MB
            };
        }

        private static HttpClient CreateConfiguredClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
            return client;
        }

        /// <summary>
        /// Valida se a chave de API fornecida é aceita pelo SteamGridDB.
        /// Faz uma consulta rápida e retorna true se responder HTTP 200 OK.
        /// </summary>
        public async Task<bool> ValidateApiKeyAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseApiUrl}/games/id/1"))
                {
                    ConfigureRequestHeaders(request, apiKey);
                    using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                    {
                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Pesquisa jogos por termo via endpoint de autocomplete.
        /// </summary>
        public async Task<List<SteamGridGame>> SearchGamesAsync(string term, string apiKey)
        {
            if (string.IsNullOrWhiteSpace(term) || string.IsNullOrWhiteSpace(apiKey))
                return new List<SteamGridGame>();

            string encodedTerm = Uri.EscapeDataString(term.Trim());
            string url = $"{BaseApiUrl}/search/autocomplete/{encodedTerm}";

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                ConfigureRequestHeaders(request, apiKey);
                using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return new List<SteamGridGame>();

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var parsed = _serializer.Deserialize<SteamGridResponse<List<SteamGridGame>>>(json);
                    return parsed?.data ?? new List<SteamGridGame>();
                }
            }
        }

        /// <summary>
        /// Retorna as capas verticais (600x900) disponíveis para o jogo informado.
        /// Restringe estritamente a PNG e JPEG para compatibilidade com GDI+ do Windows Forms.
        /// </summary>
        public async Task<List<SteamGridAsset>> GetGameGridsAsync(int gameId, string apiKey)
        {
            if (gameId <= 0 || string.IsNullOrWhiteSpace(apiKey))
                return new List<SteamGridAsset>();

            string url = $"{BaseApiUrl}/grids/game/{gameId}?dimensions=600x900&mimes=image/png,image/jpeg&types=static";

            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                ConfigureRequestHeaders(request, apiKey);
                using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return new List<SteamGridAsset>();

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var parsed = _serializer.Deserialize<SteamGridResponse<List<SteamGridAsset>>>(json);
                    return parsed?.data ?? new List<SteamGridAsset>();
                }
            }
        }

        /// <summary>
        /// Faz o download da imagem em alta resolução e salva no caminho de destino.
        /// </summary>
        public async Task<string> DownloadCoverAsync(string imageUrl, string destinationPath)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new ArgumentNullException(nameof(imageUrl));
            if (string.IsNullOrWhiteSpace(destinationPath))
                throw new ArgumentNullException(nameof(destinationPath));

            string directory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var request = new HttpRequestMessage(HttpMethod.Get, imageUrl))
            {
                request.Headers.UserAgent.ParseAdd(UserAgent);
                using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await stream.CopyToAsync(fileStream).ConfigureAwait(false);
                    }
                }
            }

            return destinationPath;
        }

        /// <summary>
        /// Baixa os bytes brutos de uma URL (ex: miniatura de capa).
        /// </summary>
        public async Task<byte[]> GetByteArrayAsync(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                return null;

            using (var request = new HttpRequestMessage(HttpMethod.Get, imageUrl))
            {
                request.Headers.UserAgent.ParseAdd(UserAgent);
                using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return null;

                    return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
            }
        }

        private static void ConfigureRequestHeaders(HttpRequestMessage request, string apiKey)
        {
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public void Dispose()
        {
            if (_disposeClient)
            {
                _client?.Dispose();
            }
        }
    }
}
