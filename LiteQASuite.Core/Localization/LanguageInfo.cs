namespace LiteQASuite.Core.Localization;

/// <summary>
/// Identidade de um idioma disponível, lida do bloco <c>_meta</c> de cada
/// arquivo de tradução. Alimenta o dropdown de idioma do Shell.
/// </summary>
/// <param name="Code">Código do idioma (ex.: "pt-BR").</param>
/// <param name="Name">Nome exibido (ex.: "Português (Brasil)").</param>
public sealed record LanguageInfo(string Code, string Name);