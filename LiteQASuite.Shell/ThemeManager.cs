using System;
using System.Linq;
using System.Windows;

namespace LiteQASuite.Shell;

/// <summary>
/// Gerencia o tema visual (Claro/Escuro) trocando, em runtime, o
/// <see cref="ResourceDictionary"/> de tema mesclado nos recursos da aplicação.
/// Como as telas referenciam as cores por <c>{DynamicResource Brush.*}</c>, a
/// troca aplica-se ao vivo, sem cada controle precisar ser reconfigurado na mão.
/// </summary>
public sealed class ThemeManager
{
    private const string LightUri = "pack://application:,,,/LiteQASuite.Shell;component/Themes/Theme.Light.xaml";
    private const string DarkUri = "pack://application:,,,/LiteQASuite.Shell;component/Themes/Theme.Dark.xaml";
    private const string ThemePathMarker = "/Themes/Theme.";

    private readonly ResourceDictionary _appResources;

    /// <summary><c>true</c> se o tema ativo é o Escuro.</summary>
    public bool IsDark { get; private set; }

    /// <param name="appResources">
    /// Recursos da aplicação (tipicamente <c>Application.Current.Resources</c>),
    /// onde o dicionário de tema é mesclado.
    /// </param>
    public ThemeManager(ResourceDictionary appResources)
        => _appResources = appResources ?? throw new ArgumentNullException(nameof(appResources));

    /// <summary>
    /// Aplica o tema Claro ou Escuro, substituindo qualquer tema já mesclado
    /// (o default do App.xaml ou o anterior).
    /// </summary>
    public void Apply(bool dark)
    {
        var next = new ResourceDictionary
        {
            Source = new Uri(dark ? DarkUri : LightUri, UriKind.Absolute)
        };

        var existing = _appResources.MergedDictionaries
            .FirstOrDefault(d => d.Source is { } source && source.OriginalString.Contains(ThemePathMarker));

        if (existing != null)
            _appResources.MergedDictionaries.Remove(existing);

        _appResources.MergedDictionaries.Add(next);
        IsDark = dark;
    }

    /// <summary>Alterna entre Claro e Escuro.</summary>
    public void Toggle() => Apply(!IsDark);
}