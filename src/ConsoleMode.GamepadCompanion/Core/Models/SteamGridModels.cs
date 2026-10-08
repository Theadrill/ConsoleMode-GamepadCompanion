using System;
using System.Collections.Generic;

namespace ConsoleMode.GamepadCompanion.Core.Models
{
    /// <summary>
    /// Resposta genérica da API v2 do SteamGridDB.
    /// </summary>
    public sealed class SteamGridResponse<T>
    {
        public bool success { get; set; }
        public T data { get; set; }
        public string[] errors { get; set; }
    }

    /// <summary>
    /// Representa um jogo retornado pela busca/autocomplete do SteamGridDB.
    /// </summary>
    public sealed class SteamGridGame
    {
        public int id { get; set; }
        public string name { get; set; }
        public string[] types { get; set; }
        public bool verified { get; set; }
    }

    /// <summary>
    /// Informações do autor do asset enviado ao SteamGridDB.
    /// </summary>
    public sealed class SteamGridAuthor
    {
        public string id { get; set; }
        public string name { get; set; }
    }

    /// <summary>
    /// Representa uma capa vertical (grid) do SteamGridDB.
    /// </summary>
    public sealed class SteamGridAsset
    {
        public int id { get; set; }
        public int score { get; set; }
        public string style { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public bool nsfw { get; set; }
        public bool humorous { get; set; }
        public string mime { get; set; }
        public string language { get; set; }
        public string url { get; set; }
        public string thumb { get; set; }
        public SteamGridAuthor author { get; set; }
    }
}
