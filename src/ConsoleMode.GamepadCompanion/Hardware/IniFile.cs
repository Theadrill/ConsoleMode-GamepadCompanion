using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ConsoleMode.GamepadCompanion.Hardware
{
    /// <summary>
    /// Parser e escritor de arquivos INI puro (sem dependências externas).
    /// </summary>
    public sealed class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> _sections =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        public void Load(string filePath)
        {
            _sections.Clear();
            if (!File.Exists(filePath)) return;

            string currentSection = string.Empty;
            foreach (string rawLine in File.ReadAllLines(filePath, Encoding.UTF8))
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    if (!_sections.ContainsKey(currentSection))
                        _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    continue;
                }

                int eqIdx = line.IndexOf('=');
                if (eqIdx > 0)
                {
                    string key = line.Substring(0, eqIdx).Trim();
                    string val = line.Substring(eqIdx + 1).Trim();

                    if (!_sections.ContainsKey(currentSection))
                        _sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    _sections[currentSection][key] = val;
                }
            }
        }

        public string GetValue(string section, string key, string defaultValue = "")
        {
            if (_sections.TryGetValue(section, out var keys) && keys.TryGetValue(key, out var val))
                return val;
            return defaultValue;
        }

        public int GetInt(string section, string key, int defaultValue)
        {
            string val = GetValue(section, key);
            return int.TryParse(val, out int res) ? res : defaultValue;
        }

        public bool GetBool(string section, string key, bool defaultValue)
        {
            string val = GetValue(section, key);
            return bool.TryParse(val, out bool res) ? res : defaultValue;
        }

        public void SetValue(string section, string key, string value)
        {
            if (!_sections.ContainsKey(section))
                _sections[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            _sections[section][key] = value ?? string.Empty;
        }

        public IEnumerable<string> GetSections()
        {
            return new List<string>(_sections.Keys);
        }

        public bool RemoveSection(string section)
        {
            if (string.IsNullOrEmpty(section)) return false;
            return _sections.Remove(section);
        }

        public void Save(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            foreach (var sec in _sections)
            {
                sb.AppendLine($"[{sec.Key}]");
                foreach (var kv in sec.Value)
                {
                    sb.AppendLine($"{kv.Key}={kv.Value}");
                }
                sb.AppendLine();
            }
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
