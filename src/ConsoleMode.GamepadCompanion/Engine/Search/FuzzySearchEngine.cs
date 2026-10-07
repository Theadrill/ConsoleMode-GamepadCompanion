using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleMode.GamepadCompanion.Core.Models;

namespace ConsoleMode.GamepadCompanion.Engine.Search
{
    /// <summary>
    /// Motor de busca fuzzy tolerante a erros de digitação e buscas parciais.
    /// Avalia o Nome do Jogo e o Executável Principal.
    /// </summary>
    public static class FuzzySearchEngine
    {
        public static IEnumerable<GameEntry> Filter(IEnumerable<GameEntry> games, string query)
        {
            if (games == null) return Enumerable.Empty<GameEntry>();
            if (string.IsNullOrWhiteSpace(query)) return games;

            string normalizedQuery = query.Trim();

            var matches = new List<(GameEntry Game, int Score)>();
            foreach (var game in games)
            {
                if (game == null) continue;
                if (IsMatch(normalizedQuery, game, out int score))
                {
                    matches.Add((game, score));
                }
            }

            return matches
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.Game.Name)
                .Select(m => m.Game);
        }

        public static bool IsMatch(string query, GameEntry game, out int score)
        {
            score = 0;
            if (game == null) return false;
            if (string.IsNullOrWhiteSpace(query))
            {
                score = 1;
                return true;
            }

            int nameScore = ScoreText(query, game.Name);
            int exeScore = ScoreText(query, game.MainExecutable);

            score = Math.Max(nameScore, exeScore);
            return score > 0;
        }

        public static int ScoreText(string query, string target)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(target))
                return 0;

            query = query.ToLowerInvariant();
            target = target.ToLowerInvariant();

            // 1. Match exato
            if (target.Equals(query, StringComparison.OrdinalIgnoreCase))
                return 1000;

            // 2. Prefixo exato
            if (target.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                return 800 - query.Length;

            // 3. Substring contida diretamente
            int subIdx = target.IndexOf(query, StringComparison.OrdinalIgnoreCase);
            if (subIdx >= 0)
                return 600 - subIdx;

            // 4. Tokenização e comparação por palavra
            string[] queryTokens = query.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
            string[] targetTokens = target.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);

            if (queryTokens.Length > 1)
            {
                int matchedTokenScores = 0;
                int matchedCount = 0;
                foreach (var qToken in queryTokens)
                {
                    int bestTokenScore = 0;
                    foreach (var tToken in targetTokens)
                    {
                        int s = ScoreSingleToken(qToken, tToken);
                        if (s > bestTokenScore) bestTokenScore = s;
                    }
                    if (bestTokenScore > 0)
                    {
                        matchedTokenScores += bestTokenScore;
                        matchedCount++;
                    }
                }

                if (matchedCount == queryTokens.Length)
                {
                    return 500 + (matchedTokenScores / queryTokens.Length);
                }
            }
            else if (queryTokens.Length == 1)
            {
                string singleQuery = queryTokens[0];
                int bestTokenScore = 0;
                foreach (var tToken in targetTokens)
                {
                    int s = ScoreSingleToken(singleQuery, tToken);
                    if (s > bestTokenScore) bestTokenScore = s;
                }
                if (bestTokenScore > 0)
                    return bestTokenScore;
            }

            // 5. Subsequência (todas as letras da query em ordem no target)
            int subseqScore = CalculateSubsequenceScore(query, target);
            if (subseqScore > 0)
                return subseqScore;

            // 6. Distância de Levenshtein para a string inteira se tamanho similar
            int dist = LevenshteinDistance(query, target);
            int maxAllowedDist = query.Length <= 4 ? 1 : 2;
            if (dist <= maxAllowedDist)
            {
                return 200 - (dist * 50);
            }

            return 0;
        }

        private static int ScoreSingleToken(string qToken, string tToken)
        {
            if (tToken.Equals(qToken, StringComparison.OrdinalIgnoreCase))
                return 500;
            if (tToken.StartsWith(qToken, StringComparison.OrdinalIgnoreCase))
                return 400;
            if (tToken.IndexOf(qToken, StringComparison.OrdinalIgnoreCase) >= 0)
                return 300;

            int dist = LevenshteinDistance(qToken, tToken);
            int maxAllowed = qToken.Length <= 3 ? 0 : (qToken.Length <= 6 ? 1 : 2);
            if (dist <= maxAllowed)
            {
                return 250 - (dist * 40);
            }

            return 0;
        }

        private static int CalculateSubsequenceScore(string query, string target)
        {
            int qIdx = 0;
            int tIdx = 0;
            int consecutiveBonus = 0;

            while (qIdx < query.Length && tIdx < target.Length)
            {
                if (query[qIdx] == target[tIdx])
                {
                    qIdx++;
                    consecutiveBonus += 5;
                }
                tIdx++;
            }

            if (qIdx == query.Length)
            {
                return 150 + consecutiveBonus - (target.Length - query.Length);
            }

            return 0;
        }

        public static int LevenshteinDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length;
            int m = t.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
    }
}
