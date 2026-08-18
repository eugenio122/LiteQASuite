using System;
using System.Collections.Generic;

namespace LiteQASuite.Core.Localization;

/// <summary>
/// Internacionalização (i18n) central do LiteQASuite. Carrega um arquivo JSON
/// por idioma (com as seções de cada módulo aninhadas), resolve textos com
/// fallback para pt-BR, e é a fonte do dropdown de idioma do Shell.
///
/// Substitui os cinco LanguageManagers estáticos do código antigo por uma única
/// instância, injetada pelo composition root. pt-BR é o idioma canônico: define
/// o conjunto de chaves e é a rede de segurança de qualquer chave faltante.
/// </summary>
public interface ILanguageManager
{
    /// <summary>Código do idioma ativo (ex.: "pt-BR").</summary>
    string CurrentLanguage { get; }

    /// <summary>Idiomas carregados, para popular o seletor de idioma.</summary>
    IReadOnlyList<LanguageInfo> AvailableLanguages { get; }

    /// <summary>Disparado após a troca de idioma; a UI reage relocalizando.</summary>
    event Action? LanguageChanged;

    /// <summary>
    /// Texto de <paramref name="key"/> na seção de <paramref name="moduleId"/>,
    /// no idioma ativo. Cai para pt-BR se faltar; devolve a própria chave se
    /// faltar até em pt-BR (debug visual — a tela nunca fica em branco).
    /// </summary>
    string GetString(string moduleId, string key);

    /// <summary>Igual, formatando o resultado com <paramref name="args"/> (ex.: "{0}").</summary>
    string GetString(string moduleId, string key, params object[] args);

    /// <summary>Troca o idioma ativo (se carregado) e dispara <see cref="LanguageChanged"/>.</summary>
    void SetLanguage(string code);

    /// <summary>Acessor de strings preso a um módulo — o que cada módulo recebe.</summary>
    IModuleStrings ForModule(string moduleId);

    /// <summary>
    /// Chaves presentes em pt-BR e ausentes em <paramref name="code"/>, no formato
    /// "Módulo.Chave". Mede o buraco de tradução de um idioma.
    /// </summary>
    IReadOnlyList<string> GetMissingKeys(string code);

    /// <summary>
    /// Escreve o pt-BR canônico (todas as chaves) em <paramref name="path"/> — o
    /// "modelo" que o usuário baixa para traduzir (ou pedir a uma IA que traduza).
    /// </summary>
    void ExportTemplate(string path);
}