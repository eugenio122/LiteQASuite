using System.Collections.ObjectModel;
using System.Windows.Input;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Modules;
using LiteQASuite.Core.Mvvm;
using LiteShot.Models;
using LiteShot.Platform;
using LiteShot.Settings;

namespace LiteShot.ViewModels;

/// <summary>
/// ViewModel da tela de configurações do LiteShot.
///
/// <b>Tudo na tela é commit único, pelo botão Salvar</b> — inclusive o bloco de
/// perfil. Não há gravação ao vivo em lugar nenhum.
///
/// Isso decorre de uma distinção que vale ter clara (e que está documentada em
/// <see cref="CaptureProfile"/>): o overlay escreve sozinho o <b>estado</b> do
/// perfil — geometria da seleção, posição da barra, cores — mas nunca a
/// <b>configuração</b>, que são os três checkboxes desta tela. Como os dois lados
/// mexem em campos diferentes, não há conflito, e a tela pode ser um formulário
/// comum.
///
/// O seletor <c>P1</c>/<c>P2</c> daqui é uma <b>aba de edição</b>: escolhe qual
/// perfil você está configurando. Quem alterna o perfil <i>ativo</i> é a barra de
/// ferramentas, durante a captura. São coisas diferentes, e por isso esta tela não
/// encosta em <see cref="LiteShotSettings.ActiveProfile"/>.
///
/// Os dois perfis são editados sobre <b>cópias</b>, aplicadas de volta só no
/// Salvar — assim trocar de aba não descarta o que foi mexido na outra.
/// </summary>
public sealed class LiteShotViewModel : ViewModelBase, IDisposable
{
    private static readonly (string Value, string Key)[] FixedResolutions =
    {
        ("3840x2160", "Resolution.4K"),
        ("2560x1440", "Resolution.QHD"),
        ("1920x1080", "Resolution.FHD"),
        ("1600x900",  "Resolution.1600"),
        ("1366x768",  "Resolution.1366"),
        ("1280x720",  "Resolution.720p"),
        ("854x480",   "Resolution.480p"),
    };

    private readonly IModuleStrings _strings;
    private readonly ILanguageManager _language;
    private readonly SettingsStore _store;
    private readonly LiteShotSettings _settings;
    private readonly Action _onLanguageChanged;

    /// <summary>
    /// Cópias de configuração dos dois perfis. A tela edita estas; o Salvar as
    /// aplica de volta nos perfis reais, sem tocar em geometria nem cores.
    /// </summary>
    private readonly List<CaptureProfile> _profileDrafts = new();

    // Estado pendente dos campos gerais, até o Salvar.
    private bool _showNotifications;
    private bool _captureCursor;
    private bool _hotkeyEnabled;
    private string _imageFormat = LiteShotSettings.DefaultImageFormat;
    private ResolutionOption? _selectedResolution;
    private uint _pendingHotkeyModifier;
    private uint _pendingHotkey;

    private int _editingProfileId = 1;

    private bool _isDirty;
    private string _statusMessage = string.Empty;
    private string _hotkeyMessage = string.Empty;

    /// <summary>A combinação escolhida é uma tecla solta que o sistema todo usa.</summary>
    private bool _hotkeyNeedsModifierWarning;

    /// <summary>O Windows recusou o registro — outro programa tem a combinação.</summary>
    private bool _hotkeyUnavailable;

    /// <summary>
    /// Silencia a marcação de pendência durante recargas programáticas. Sem isto,
    /// limpar a lista de resoluções (na troca de idioma) faria o ComboBox zerar o
    /// item selecionado, o setter marcaria pendência e o Salvar acenderia sozinho.
    /// </summary>
    private bool _suppressDirty;

    private bool _disposed;

    public LiteShotViewModel(ModuleContext context, SettingsStore store, LiteShotSettings settings)
    {
        _language = context.Language;
        _strings = _language.ForModule(LiteShotModule.ModuleId);
        _store = store;
        _settings = settings;

        ResolutionOptions = new ObservableCollection<ResolutionOption>();
        ImageFormats = new[] { "PNG", "JPEG", "BMP" };

        SaveCommand = new RelayCommand(_ => Save(), _ => IsDirty);
        ResetHotkeyCommand = new RelayCommand(_ => ResetHotkey());
        SelectProfileCommand = new RelayCommand(SelectProfileToEdit);

        LoadFromSettings();

        // Guardado em campo para poder cancelar no Dispose. O StubViewModel usa
        // lambda inline e avisa, no próprio comentário, que num módulo real isso
        // precisa ser desfeito.
        _onLanguageChanged = HandleLanguageChanged;
        _language.LanguageChanged += _onLanguageChanged;
    }

    // -------------------------------------------------------------- Eventos

    /// <summary>
    /// As configurações foram gravadas com sucesso. Quem é dono do atalho global —
    /// o módulo — reage re-registrando a tecla.
    ///
    /// O ViewModel de propósito não sabe o que é uma hotkey: ele só anuncia que
    /// salvou. Isso mantém a tela livre de P/Invoke e o recurso de sistema com um
    /// dono só.
    /// </summary>
    public event Action? SettingsSaved;

    // ------------------------------------------------------------ Comandos

    /// <summary>Grava a tela inteira. Habilitado só quando há pendência.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>
    /// Devolve o atalho para PrintScreen. Sobrevive como caminho rápido, mas já
    /// não é a única saída: o PrintScreen agora pode ser digitado normalmente na
    /// caixinha, porque a View trata a soltura da tecla (o Windows não entrega o
    /// PrintScreen na pressão para aplicativos comuns).
    /// </summary>
    public ICommand ResetHotkeyCommand { get; }

    /// <summary>Troca a aba de edição de perfil. Recebe "1" ou "2".</summary>
    public ICommand SelectProfileCommand { get; }

    // ------------------------------------------------------- Campos gerais

    public bool ShowNotifications
    {
        get => _showNotifications;
        set { if (SetProperty(ref _showNotifications, value)) MarkDirty(); }
    }

    public bool CaptureCursor
    {
        get => _captureCursor;
        set { if (SetProperty(ref _captureCursor, value)) MarkDirty(); }
    }

    public bool HotkeyEnabled
    {
        get => _hotkeyEnabled;
        set { if (SetProperty(ref _hotkeyEnabled, value)) MarkDirty(); }
    }

    public string ImageFormat
    {
        get => _imageFormat;
        set { if (SetProperty(ref _imageFormat, value)) MarkDirty(); }
    }

    public ResolutionOption? SelectedResolution
    {
        get => _selectedResolution;
        set { if (SetProperty(ref _selectedResolution, value)) MarkDirty(); }
    }

    /// <summary>Formatos disponíveis. Não são traduzidos — são nomes de formato.</summary>
    public IReadOnlyList<string> ImageFormats { get; }

    /// <summary>
    /// Opções do limitador. É <see cref="ObservableCollection{T}"/> porque a lista
    /// é reconstruída quando o idioma muda: os rótulos são localizados e os itens,
    /// sendo objetos simples, não se relocalizam sozinhos.
    /// </summary>
    public ObservableCollection<ResolutionOption> ResolutionOptions { get; }

    /// <summary>A combinação de teclas pendente, escrita por extenso.</summary>
    public string HotkeyDisplay => HotkeyRules.Describe(_pendingHotkeyModifier, _pendingHotkey);

    // ---------------------------------------------- Bloco de perfil (aba)

    /// <summary>A cópia que os três checkboxes estão editando.</summary>
    private CaptureProfile EditingProfile =>
        _profileDrafts.First(p => p.Id == _editingProfileId);

    /// <summary>
    /// Qual perfil a tela está configurando. É estado só da interface: não é
    /// persistido, não marca pendência e <b>não</b> altera o perfil ativo da
    /// captura — isso é papel dos botões da barra de ferramentas.
    /// </summary>
    public int EditingProfileId
    {
        get => _editingProfileId;
        set
        {
            if (value is not (1 or 2) || _editingProfileId == value)
                return;

            _editingProfileId = value;

            // Trocar de aba não muda três valores: muda qual objeto os três leem.
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsProfile1Active));
            OnPropertyChanged(nameof(IsProfile2Active));
            OnPropertyChanged(nameof(NavbarVertical));
            OnPropertyChanged(nameof(KeepSelection));
            OnPropertyChanged(nameof(KeepNavbarPosition));
        }
    }

    public bool IsProfile1Active => _editingProfileId == 1;

    public bool IsProfile2Active => _editingProfileId == 2;

    public bool NavbarVertical
    {
        get => EditingProfile.NavbarVertical;
        set
        {
            if (EditingProfile.NavbarVertical == value) return;
            EditingProfile.NavbarVertical = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public bool KeepSelection
    {
        get => EditingProfile.KeepSelection;
        set
        {
            if (EditingProfile.KeepSelection == value) return;
            EditingProfile.KeepSelection = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    public bool KeepNavbarPosition
    {
        get => EditingProfile.KeepNavbarPosition;
        set
        {
            if (EditingProfile.KeepNavbarPosition == value) return;
            EditingProfile.KeepNavbarPosition = value;
            OnPropertyChanged();
            MarkDirty();
        }
    }

    // -------------------------------------------------------------- Estado

    /// <summary>Há campos alterados e não gravados, em qualquer parte da tela.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    /// <summary>
    /// Aviso do salvamento, ao lado do botão Salvar. Substitui o toast do código
    /// antigo — o serviço de notificação da casca só chega na fatia de captura, e
    /// esta tela não precisa dele.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Aviso da tecla de atalho, logo abaixo do campo. Separado do
    /// <see cref="StatusMessage"/> de propósito: são assuntos diferentes, e o
    /// aviso precisa estar ao lado do campo que o provocou para ser lido.
    /// </summary>
    public string HotkeyMessage
    {
        get => _hotkeyMessage;
        private set => SetProperty(ref _hotkeyMessage, value);
    }

    // -------------------------------------------------------------- Rótulos

    public string Title => _strings.GetString("Settings.Title");
    public string LabelShowNotifications => _strings.GetString("Settings.ShowNotifications");
    public string LabelCaptureCursor => _strings.GetString("Settings.CaptureCursor");
    public string LabelHotkeyEnabled => _strings.GetString("Settings.HotkeyEnabled");
    public string LabelImageFormat => _strings.GetString("Settings.ImageFormat");
    public string LabelResolution => _strings.GetString("Settings.Resolution");
    public string LabelHotkey => _strings.GetString("Settings.Hotkey");
    public string LabelHotkeyReset => _strings.GetString("Settings.Hotkey.Reset");
    public string LabelHotkeyHint => _strings.GetString("Settings.Hotkey.Hint");
    public string LabelSave => _strings.GetString("Settings.Save");
    public string LabelProfileTitle => _strings.GetString("Settings.Profile.Title");
    public string LabelProfileHint => _strings.GetString("Settings.Profile.Hint");
    public string LabelProfileEditing => _strings.GetString("Settings.Profile.Editing");
    public string LabelProfile1 => _strings.GetString("Settings.Profile.1");
    public string LabelProfile2 => _strings.GetString("Settings.Profile.2");
    public string LabelNavbarVertical => _strings.GetString("Settings.NavbarVertical");
    public string LabelKeepSelection => _strings.GetString("Settings.KeepSelection");
    public string LabelKeepNavbarPosition => _strings.GetString("Settings.KeepNavbarPosition");

    // --------------------------------------------------------------- Ações

    /// <summary>
    /// Recebe uma tecla capturada pela caixinha de atalho. Chamado pelo code-behind
    /// da View: interceptar teclado não se faz por binding.
    ///
    /// Só as teclas reservadas são recusadas — usá-las como disparo global
    /// sequestraria comandos do sistema inteiro (um Ctrl+C que abre o overlay em
    /// vez de copiar), e ainda colidiria com o aluguel que o overlay faz dessas
    /// mesmas teclas. Tecla sem modificador é aceita, mas **avisa**: registrar a
    /// letra "P" sozinha faz o P parar de digitar em qualquer outro programa.
    /// </summary>
    public void ApplyHotkey(uint modifier, uint virtualKey)
    {
        if (virtualKey == 0)
            return;

        if (HotkeyRules.IsReserved(virtualKey))
        {
            HotkeyMessage = _strings.GetString("Settings.Hotkey.Reserved");
            return;
        }

        _pendingHotkeyModifier = modifier;
        _pendingHotkey = virtualKey;

        OnPropertyChanged(nameof(HotkeyDisplay));
        MarkDirty();

        // A tecla mudou, então a recusa anterior do Windows não vale mais — ela só
        // se confirma no próximo Salvar.
        _hotkeyUnavailable = false;
        _hotkeyNeedsModifierWarning =
            modifier == NativeMethods.MOD_NONE && !HotkeyRules.CanStandAlone(virtualKey);

        RefreshHotkeyMessage();
    }

    /// <summary>
    /// O módulo informa se o registro no Windows deu certo. Acontece na abertura da
    /// tela (o registro ocorreu lá no arranque, quando não havia interface para
    /// avisar) e depois de cada Salvar.
    /// </summary>
    public void ReportHotkeyState(bool registered)
    {
        _hotkeyUnavailable = !registered && _hotkeyEnabled;
        RefreshHotkeyMessage();
    }

    /// <summary>
    /// Duas condições podem valer ao mesmo tempo; a recusa do Windows manda, porque
    /// é a que impede o atalho de funcionar de fato.
    /// </summary>
    private void RefreshHotkeyMessage()
    {
        if (_hotkeyUnavailable)
        {
            HotkeyMessage = _strings.GetString("Settings.Hotkey.Unavailable");
            return;
        }

        HotkeyMessage = _hotkeyNeedsModifierWarning
            ? _strings.GetString("Settings.Hotkey.NoModifier")
            : string.Empty;
    }

    private void ResetHotkey()
    {
        _pendingHotkeyModifier = LiteShotSettings.DefaultHotkeyModifier;
        _pendingHotkey = LiteShotSettings.DefaultHotkey;

        OnPropertyChanged(nameof(HotkeyDisplay));
        MarkDirty();

        _hotkeyUnavailable = false;
        _hotkeyNeedsModifierWarning = false;
        RefreshHotkeyMessage();
    }

    private void SelectProfileToEdit(object? parameter)
    {
        if (parameter is int id)
            EditingProfileId = id;
        else if (int.TryParse(parameter?.ToString(), out var parsed))
            EditingProfileId = parsed;
    }

    private void Save()
    {
        _settings.ShowNotifications = _showNotifications;
        _settings.CaptureCursor = _captureCursor;
        _settings.HotkeyEnabled = _hotkeyEnabled;
        _settings.ImageFormat = _imageFormat;
        _settings.CaptureResolution = _selectedResolution?.Value ?? "Auto";
        _settings.HotkeyModifier = _pendingHotkeyModifier;
        _settings.Hotkey = _pendingHotkey;

        // Só a configuração volta para os perfis reais. A geometria e as cores
        // continuam como o overlay as deixou — inclusive se ele tiver capturado
        // enquanto esta tela estava aberta com edições pendentes.
        foreach (var draft in _profileDrafts)
        {
            var real = _settings.Profiles.FirstOrDefault(p => p.Id == draft.Id);
            real?.ApplyConfigFrom(draft);
        }

        var ok = _store.Save(_settings);

        StatusMessage = _strings.GetString(ok ? "Settings.Saved" : "Settings.SaveFailed");
        IsDirty = !ok;

        // O módulo re-registra o atalho e devolve o resultado por ReportHotkeyState.
        if (ok)
            SettingsSaved?.Invoke();
    }

    // ------------------------------------------------------------- Interno

    private void LoadFromSettings()
    {
        _suppressDirty = true;

        _showNotifications = _settings.ShowNotifications;
        _captureCursor = _settings.CaptureCursor;
        _hotkeyEnabled = _settings.HotkeyEnabled;
        _imageFormat = _settings.ImageFormat;
        _pendingHotkeyModifier = _settings.HotkeyModifier;
        _pendingHotkey = _settings.Hotkey;

        _profileDrafts.Clear();
        foreach (var profile in _settings.Profiles)
            _profileDrafts.Add(profile.CloneConfig());

        // Abre configurando o perfil que está em uso — é o mais provável de querer
        // ajustar. Daqui em diante os dois são independentes.
        _editingProfileId = _settings.ActiveProfile;

        RebuildResolutionOptions();

        _suppressDirty = false;
        IsDirty = false;
    }

    /// <summary>
    /// Refaz a lista do combo preservando o valor escolhido. Necessário na troca de
    /// idioma, porque os rótulos são localizados e os itens são objetos simples.
    /// </summary>
    private void RebuildResolutionOptions()
    {
        var wasSuppressed = _suppressDirty;
        _suppressDirty = true;

        var current = _selectedResolution?.Value
                      ?? (string.IsNullOrWhiteSpace(_settings.CaptureResolution)
                            ? ResolveDefaultResolution()
                            : _settings.CaptureResolution);

        ResolutionOptions.Clear();
        ResolutionOptions.Add(new ResolutionOption("Auto", _strings.GetString("Resolution.Auto")));

        foreach (var (value, key) in FixedResolutions)
            ResolutionOptions.Add(new ResolutionOption(value, _strings.GetString(key)));

        // Se a resolução escolhida não é uma das fixas (ex.: um monitor 1440x900),
        // entra uma opção própria. A chave Resolution.Custom existia no código
        // antigo e nunca chegou a ser usada.
        if (ResolutionOptions.All(option => option.Value != current))
        {
            ResolutionOptions.Add(new ResolutionOption(
                current, current + _strings.GetString("Resolution.Custom")));
        }

        _selectedResolution = ResolutionOptions.First(option => option.Value == current);
        OnPropertyChanged(nameof(SelectedResolution));

        _suppressDirty = wasSuppressed;
    }

    /// <summary>
    /// Padrão do limitador na primeira execução: acima de Full HD limita a
    /// 1920x1080; abaixo disso usa a resolução do próprio monitor.
    ///
    /// Depende de o processo estar declarado PerMonitorV2 para ler pixels físicos.
    /// Esse manifest chega junto da fatia de captura — até lá, num monitor com
    /// escala, o valor calculado sai menor que o real. Afeta só a primeira
    /// execução, antes de existir arquivo salvo.
    /// </summary>
    private static string ResolveDefaultResolution()
    {
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

        if (width <= 0 || height <= 0)
            return "Auto";

        return width > 1920 ? "1920x1080" : $"{width}x{height}";
    }

    private void MarkDirty()
    {
        if (_suppressDirty) return;

        StatusMessage = string.Empty;
        IsDirty = true;
    }

    private void HandleLanguageChanged()
    {
        RebuildResolutionOptions();

        // Os rótulos são propriedades calculadas sobre IModuleStrings; notificar
        // tudo faz a tela inteira se relocalizar. Mesmo padrão do StubViewModel.
        OnPropertyChanged(null);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _language.LanguageChanged -= _onLanguageChanged;
        _disposed = true;
    }
}