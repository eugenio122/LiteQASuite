using System.Windows;
using LiteFlow.Models;
using LiteFlow.Storage;
using LiteFlow.ViewModels;
using LiteFlow.Views;
using LiteQASuite.Core.Events;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Modules;

namespace LiteFlow;

/// <summary>
/// A porta de entrada do LiteFlow no LiteQASuite — o editor de evidências.
///
/// <b>O ViewModel nasce no construtor, junto do módulo.</b> É a diferença
/// deliberada em relação ao <c>LiteShotModule</c>, onde só a View é preguiçosa e o
/// ViewModel vem com ela: lá a tela é configuração, aqui ela é o destino das
/// capturas. Um print tirado enquanto o usuário está no LiteJson tem que entrar no
/// cenário do mesmo jeito, então alguém precisa estar de pé segurando a sessão
/// desde o arranque. A View continua preguiçosa — o que é caro é a tela, não o
/// estado.
///
/// <b>O que o módulo assina:</b> só o <c>CaptureCompletedEvent</c>. O "started" é
/// sinal para quem congela o estado da tela (o LiteJson); o "canceled" fala de um
/// passo pendente que o LiteFlow nunca criou — aqui a evidência só existe quando a
/// captura é confirmada, então não há nada a limpar.
/// </summary>
public sealed class LiteFlowModule : IModule
{
    /// <summary>
    /// Chave estável do módulo. É usada em <c>ForModule</c>, é o nome da seção nos
    /// <c>Lang/*.json</c> e é a chave do ícone (prancheta) no mapa do Shell — as
    /// três precisam desta string exata.
    /// </summary>
    public const string ModuleId = "LiteFlow";

    private readonly IModuleStrings _strings;
    private readonly SettingsStore _settingsStore;
    private readonly LiteFlowSettings _settings;
    private readonly LiteFlowViewModel _viewModel;

    private LiteFlowView? _view;

    public LiteFlowModule(ModuleContext context)
    {
        _strings = context.Language.ForModule(ModuleId);

        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();

        _viewModel = new LiteFlowViewModel(context, _settingsStore, _settings);

        // O barramento invoca na thread de quem publicou — e o LiteShot publica de
        // uma thread de fundo. Quem marshala é o assinante; o ViewModel faz isso
        // logo na entrada do método.
        context.Events.Subscribe<CaptureCompletedEvent>(_viewModel.OnCaptureCompleted);
    }

    public string Id => ModuleId;

    public string DisplayName => _strings.GetString("Module.Name");

    /// <summary>A tela do editor, criada no primeiro acesso e cacheada.</summary>
    public FrameworkElement View => _view ??= new LiteFlowView { DataContext = _viewModel };

    /// <summary>
    /// Encerramento da aplicação: grava o cenário aberto e devolve a pasta de
    /// cache. Seguro mesmo que a <see cref="View"/> nunca tenha sido criada — o
    /// ViewModel existe desde o construtor.
    ///
    /// Não há como cancelar a assinatura: o <c>IEventBus</c> do Core oferece
    /// <c>Subscribe</c> e <c>Publish</c>, e nenhum <c>Unsubscribe</c>. Como o
    /// módulo vive enquanto o aplicativo vive, o ViewModel apenas passa a ignorar
    /// o que chegar depois do encerramento.
    /// </summary>
    public void Shutdown() => _viewModel.Shutdown();
}