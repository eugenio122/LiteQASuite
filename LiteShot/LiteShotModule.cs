using System.Windows;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Modules;
using LiteShot.Capture;
using LiteShot.Models;
using LiteShot.Platform;
using LiteShot.Settings;
using LiteShot.ViewModels;
using LiteShot.Views;

namespace LiteShot;

/// <summary>
/// A porta de entrada do LiteShot no LiteQASuite. Sucessor do
/// <c>LiteShotPlugin : ILitePlugin</c> do mundo antigo — sem Reflection, sem DLL
/// carregada em runtime: o composition root dá <c>new</c> nesta classe e entrega
/// ao Shell apenas como <see cref="IModule"/>.
///
/// <b>O módulo é dono de dois recursos que vivem fora da tela:</b> o atalho global
/// e o coordenador de captura. Ambos nascem no construtor, e não na primeira
/// abertura da <see cref="View"/> — o usuário precisa poder apertar PrintScreen sem
/// nunca ter navegado até aqui. É por isso que o composition root instancia todos
/// os módulos no arranque.
/// </summary>
public sealed class LiteShotModule : IModule
{
    /// <summary>
    /// Chave estável do módulo. É usada em <c>ForModule</c> e é também o nome da
    /// seção nos arquivos <c>Lang/*.json</c> — os dois lados precisam da mesma
    /// string, exatamente.
    /// </summary>
    public const string ModuleId = "LiteShot";

    private readonly ModuleContext _context;
    private readonly IModuleStrings _strings;
    private readonly SettingsStore _store;
    private readonly LiteShotSettings _settings;
    private readonly CaptureCoordinator _coordinator;
    private readonly GlobalHotkey _hotkey;

    private LiteShotView? _view;
    private LiteShotViewModel? _viewModel;

    public LiteShotModule(ModuleContext context)
    {
        _context = context;
        _strings = context.Language.ForModule(ModuleId);

        _store = new SettingsStore();
        _settings = _store.Load();

        _coordinator = new CaptureCoordinator(_strings, _store, _settings, context.Events);

        _hotkey = new GlobalHotkey();
        _hotkey.Pressed += _coordinator.Start;

        ApplyHotkey();
    }

    public string Id => ModuleId;

    public string DisplayName => _strings.GetString("Module.Name");

    /// <summary>
    /// A tela de configurações, criada no primeiro acesso e cacheada. O ViewModel
    /// fica guardado num campo porque o <see cref="Shutdown"/> precisa alcançá-lo
    /// para cancelar assinaturas.
    /// </summary>
    public FrameworkElement View => _view ??= CreateView();

    private LiteShotView CreateView()
    {
        _viewModel = new LiteShotViewModel(_context, _store, _settings);
        _viewModel.SettingsSaved += OnSettingsSaved;

        // Se o registro falhou lá no arranque, a tela é o primeiro lugar onde dá
        // para contar isso ao usuário — não existia interface naquele momento.
        _viewModel.ReportHotkeyState(_hotkey.IsRegistered);

        return new LiteShotView { DataContext = _viewModel };
    }

    /// <summary>
    /// O usuário salvou as configurações: o atalho pode ter mudado, ou ter sido
    /// desligado. O ViewModel não sabe o que é uma hotkey — ele só avisa que salvou,
    /// e quem é dono do recurso reage.
    /// </summary>
    private void OnSettingsSaved()
    {
        ApplyHotkey();
        _viewModel?.ReportHotkeyState(_hotkey.IsRegistered);
    }

    private void ApplyHotkey() =>
        _hotkey.Apply(_settings.HotkeyModifier, _settings.Hotkey, _settings.HotkeyEnabled);

    /// <summary>
    /// Encerramento da aplicação: solta o atalho global, fecha qualquer overlay
    /// aberto e cancela a assinatura do evento de idioma.
    ///
    /// O <c>?.</c> no ViewModel cobre o caso de a tela nunca ter sido aberta — a
    /// View é preguiçosa, e o contrato exige que isto seja seguro mesmo assim.
    /// </summary>
    public void Shutdown()
    {
        _hotkey.Pressed -= _coordinator.Start;
        _hotkey.Dispose();

        _coordinator.Dispose();

        if (_viewModel is not null)
        {
            _viewModel.SettingsSaved -= OnSettingsSaved;
            _viewModel.Dispose();
            _viewModel = null;
        }
    }
}