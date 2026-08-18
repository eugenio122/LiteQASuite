using System.Windows;

namespace LiteQASuite.Core.Modules;

/// <summary>
/// Contrato que todo módulo do LiteQASuite (LiteShot, LiteFlow, LiteJson,
/// LiteAutomation) implementa para ser hospedado pelo Shell.
///
/// Substitui o antigo <c>ILitePlugin</c>: sem Reflection e sem carregamento
/// dinâmico de DLL. Os módulos são referências de projeto, instanciados pelo
/// composition root (o executável <c>LiteQASuite</c>) e entregues ao Shell
/// apenas como <see cref="IModule"/> — o Shell nunca conhece a implementação
/// concreta nem os tipos internos do módulo.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Chave estável e única do módulo (ex.: "LiteShot"). Não é rótulo de tela:
    /// serve para o Shell persistir estado (ativado/desativado) e referenciar o
    /// módulo sem depender do nome exibido. Não muda entre versões nem idiomas.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Nome exibido na navegação, já localizado pelo próprio módulo
    /// (via LanguageManager). Pode variar por idioma; nunca usar como chave.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// A view raiz que o Shell hospeda. Ser <see cref="FrameworkElement"/> permite
    /// hospedar, pela mesma porta, tanto XAML nativo (módulo já migrado) quanto um
    /// <c>UserControl</c> WinForms embrulhado num <c>WindowsFormsHost</c> (módulo
    /// ainda legado) — é o que viabiliza migrar um módulo por vez. Construída sob
    /// demanda e cacheada pela implementação (o Shell pode acessá-la mais de uma vez).
    /// </summary>
    FrameworkElement View { get; }

    /// <summary>
    /// Chamado pelo Shell no encerramento da aplicação. O módulo libera aqui os
    /// recursos de SO que não somem sozinhos: hooks de teclado (LiteShot), threads
    /// de captura e <c>CancellationTokenSource</c> (LiteJson), e afins. Deve ser
    /// seguro chamar mesmo que a <see cref="View"/> nunca tenha sido criada.
    /// </summary>
    void Shutdown();
}