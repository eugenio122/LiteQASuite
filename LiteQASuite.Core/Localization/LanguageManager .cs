using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LiteQASuite.Core.Localization;

/// <summary>
/// Implementação de <see cref="ILanguageManager"/>. Carrega, no arranque, todos
/// os arquivos <c>*.json</c> da pasta de idiomas (populada pelo composition root,
/// tipicamente <c>Lang/</c> ao lado do executável) e mantém tudo em memória.
///
/// Estrutura de um arquivo: um bloco <c>_meta</c> (code + name) e, ao lado, uma
/// seção por módulo (moduleId → chave → texto). pt-BR é obrigatório e canônico.
///
/// Thread-safety: os dicionários são preenchidos uma vez no construtor e nunca
/// mais mudam, então as leituras (<see cref="GetString(string,string)"/>) são
/// seguras sem lock. <see cref="SetLanguage"/> só troca uma referência de string
/// (atômica); no pior caso um leitor concorrente pega o idioma antigo ou o novo.
/// </summary>
public sealed class LanguageManager : ILanguageManager
{
    private const string Canonical = "pt-BR";
    private const string MetaSection = "_meta";

    /// <summary>Dados de um idioma: nome exibido + seções (moduleId → chave → texto).</summary>
    private sealed class LanguageData
    {
        public required string Name { get; init; }
        public required Dictionary<string, Dictionary<string, string>> Sections { get; init; }
    }

    private readonly Dictionary<string, LanguageData> _languages =
        new(StringComparer.OrdinalIgnoreCase);

    public string CurrentLanguage { get; private set; } = Canonical;

    public event Action? LanguageChanged;

    /// <summary>
    /// Carrega os idiomas da pasta informada e define o idioma inicial (que o
    /// composition root normalmente lê das configurações salvas).
    /// </summary>
    /// <param name="languageFolder">Pasta com os arquivos *.json (ex.: "Lang").</param>
    /// <param name="initialLanguage">Idioma inicial; cai para pt-BR se ausente.</param>
    /// <exception cref="DirectoryNotFoundException">Se a pasta não existe.</exception>
    /// <exception cref="InvalidOperationException">Se o pt-BR canônico não carregar.</exception>
    public LanguageManager(string languageFolder, string initialLanguage = Canonical)
    {
        if (!Directory.Exists(languageFolder))
            throw new DirectoryNotFoundException($"Pasta de idiomas não encontrada: {languageFolder}");

        foreach (var file in Directory.EnumerateFiles(languageFolder, "*.json"))
            TryLoadFile(file);

        if (!_languages.ContainsKey(Canonical))
            throw new InvalidOperationException(
                $"O idioma canônico '{Canonical}' não foi carregado de {languageFolder}. Ele é obrigatório.");

        CurrentLanguage = _languages.ContainsKey(initialLanguage) ? initialLanguage : Canonical;
    }

    private void TryLoadFile(string file)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            var root = doc.RootElement;

            string? code = null;
            string? name = null;
            var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name == MetaSection)
                {
                    code = prop.Value.TryGetProperty("code", out var c) ? c.GetString() : null;
                    name = prop.Value.TryGetProperty("name", out var n) ? n.GetString() : null;
                    continue;
                }

                var section = new Dictionary<string, string>();
                foreach (var entry in prop.Value.EnumerateObject())
                    section[entry.Name] = entry.Value.GetString() ?? string.Empty;

                sections[prop.Name] = section;
            }

            // Sem código não dá para indexar o idioma — arquivo ignorado.
            if (string.IsNullOrWhiteSpace(code))
                return;

            _languages[code] = new LanguageData
            {
                Name = string.IsNullOrWhiteSpace(name) ? code : name!,
                Sections = sections
            };
        }
        catch (JsonException)
        {
            // Arquivo malformado é ignorado: um idioma quebrado não derruba o app.
            // (Quando o logger do Core existir, isto vira um warning registrado.)
        }
    }

    public IReadOnlyList<LanguageInfo> AvailableLanguages =>
        _languages
            .Select(kv => new LanguageInfo(kv.Key, kv.Value.Name))
            .OrderByDescending(l => l.Code == Canonical)               // pt-BR primeiro
            .ThenBy(l => l.Name, StringComparer.CurrentCulture)
            .ToList();

    public string GetString(string moduleId, string key)
    {
        if (TryResolve(CurrentLanguage, moduleId, key, out var value))
            return value;

        if (CurrentLanguage != Canonical && TryResolve(Canonical, moduleId, key, out var fallback))
            return fallback;

        return key; // debug visual
    }

    public string GetString(string moduleId, string key, params object[] args)
    {
        var text = GetString(moduleId, key);
        return args is { Length: > 0 } ? string.Format(text, args) : text;
    }

    private bool TryResolve(string language, string moduleId, string key, out string value)
    {
        if (_languages.TryGetValue(language, out var data)
            && data.Sections.TryGetValue(moduleId, out var section)
            && section.TryGetValue(key, out var text))
        {
            value = text;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public void SetLanguage(string code)
    {
        if (!_languages.ContainsKey(code) || code == CurrentLanguage)
            return;

        CurrentLanguage = code;
        LanguageChanged?.Invoke();
    }

    public IModuleStrings ForModule(string moduleId) => new ModuleStrings(this, moduleId);

    public IReadOnlyList<string> GetMissingKeys(string code)
    {
        if (!_languages.TryGetValue(Canonical, out var canonical))
            return Array.Empty<string>();

        var target = _languages.TryGetValue(code, out var t) ? t : null;
        var missing = new List<string>();

        foreach (var section in canonical.Sections)
        {
            var targetSection = target != null && target.Sections.TryGetValue(section.Key, out var ts) ? ts : null;

            foreach (var key in section.Value.Keys)
            {
                if (targetSection == null || !targetSection.ContainsKey(key))
                    missing.Add($"{section.Key}.{key}");
            }
        }

        return missing;
    }

    public void ExportTemplate(string path)
    {
        if (!_languages.TryGetValue(Canonical, out var canonical))
            throw new InvalidOperationException($"Idioma canônico '{Canonical}' indisponível para exportar.");

        using var stream = File.Create(path);
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();

        writer.WritePropertyName(MetaSection);
        writer.WriteStartObject();
        writer.WriteString("code", Canonical);
        writer.WriteString("name", canonical.Name);
        writer.WriteEndObject();

        foreach (var section in canonical.Sections)
        {
            writer.WritePropertyName(section.Key);
            writer.WriteStartObject();
            foreach (var entry in section.Value)
                writer.WriteString(entry.Key, entry.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>Acessor preso a um módulo, devolvido por <see cref="ForModule"/>.</summary>
    private sealed class ModuleStrings : IModuleStrings
    {
        private readonly LanguageManager _owner;
        private readonly string _moduleId;

        public ModuleStrings(LanguageManager owner, string moduleId)
        {
            _owner = owner;
            _moduleId = moduleId;
        }

        public string GetString(string key) => _owner.GetString(_moduleId, key);

        public string GetString(string key, params object[] args) => _owner.GetString(_moduleId, key, args);
    }
}