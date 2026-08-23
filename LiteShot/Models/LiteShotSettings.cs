namespace LiteShot.Models;

/// <summary>
/// Estrutura completa do <c>liteshot_settings.json</c>, gravado em
/// <c>%AppData%\LiteQASuite\</c>. É a memória do LiteShot entre execuções.
///
/// O modelo já nasce com os perfis, embora a interface deles só chegue na fatia
/// das anotações: escrever agora sem eles obrigaria, depois, a migrar o arquivo
/// na máquina de quem já estivesse usando.
///
/// Some do arquivo antigo: <c>IsDarkMode</c> e <c>Language</c> (o Shell manda nos
/// dois) e <c>FullScreenMode</c> (o Ctrl+A já selecionava o monitor atual, então
/// a opção era redundante).
/// </summary>
public sealed class LiteShotSettings
{
    /// <summary>Código virtual da tecla PrintScreen.</summary>
    public const uint DefaultHotkey = 0x2C;

    /// <summary>Sem modificador (nem Ctrl, nem Alt, nem Shift).</summary>
    public const uint DefaultHotkeyModifier = 0;

    /// <summary>Espessura inicial do traço das ferramentas de desenho.</summary>
    public const int DefaultPenWidth = 3;

    /// <summary>Formato de imagem inicial.</summary>
    public const string DefaultImageFormat = "PNG";

    /// <summary>Quantidade de cores personalizadas que o seletor de cor memoriza.</summary>
    public const int CustomColorSlots = 16;

    // ---------------------------------------------------------------- Global

    /// <summary>Exibir aviso após copiar ou salvar uma imagem.</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>Desenhar o cursor do mouse na imagem capturada.</summary>
    public bool CaptureCursor { get; set; }

    /// <summary>
    /// Se o atalho global está ativo. Desligar devolve a tecla ao sistema sem
    /// precisar reiniciar o aplicativo — é a única forma de fazer isso, já que
    /// desativar módulo em runtime não é suportado nesta versão.
    /// </summary>
    public bool HotkeyEnabled { get; set; } = true;

    /// <summary>Formato de arquivo preferido: PNG, JPEG ou BMP.</summary>
    public string ImageFormat { get; set; } = DefaultImageFormat;

    /// <summary>
    /// Limite de resolução aplicado ao recorte final ("Auto" ou "LARGURAxALTURA").
    /// Vazio significa "ainda não resolvido": na primeira execução o ViewModel
    /// calcula a partir do monitor primário (acima de Full HD limita a 1920x1080;
    /// abaixo disso usa a resolução do próprio monitor).
    /// </summary>
    public string CaptureResolution { get; set; } = string.Empty;

    /// <summary>Modificador do atalho global (combinação dos MOD_* do Windows).</summary>
    public uint HotkeyModifier { get; set; } = DefaultHotkeyModifier;

    /// <summary>Código virtual da tecla do atalho global.</summary>
    public uint Hotkey { get; set; } = DefaultHotkey;

    /// <summary>
    /// Espessura do traço. Passou a ser persistida: no código antigo era um campo
    /// estático do formulário, ajustável com Ctrl +/− mas perdido a cada reinício.
    /// </summary>
    public int PenWidth { get; set; } = DefaultPenWidth;

    /// <summary>
    /// Paleta pessoal do seletor de cor. É global, e não por perfil, porque são as
    /// cores que o usuário gosta de usar — não um atributo do contexto de trabalho.
    /// </summary>
    public int[] CustomColors { get; set; } = new int[CustomColorSlots];

    // --------------------------------------------------------------- Perfis

    /// <summary>Id do perfil ativo: 1 ou 2.</summary>
    public int ActiveProfile { get; set; } = 1;

    /// <summary>Os dois perfis de espaço de trabalho.</summary>
    public List<CaptureProfile> Profiles { get; set; } = CreateDefaultProfiles();

    // ---------------------------------------------------------------- Apoio

    /// <summary>
    /// O perfil apontado por <see cref="ActiveProfile"/>. É chamado a cada leitura
    /// dos campos do bloco de perfil, então o caminho feliz é uma busca simples —
    /// só reconstrói o arquivo (via <see cref="Normalize"/>) se algo estiver
    /// realmente inconsistente.
    /// </summary>
    public CaptureProfile GetActiveProfile()
    {
        var profile = Profiles.FirstOrDefault(p => p.Id == ActiveProfile);

        if (profile is null)
        {
            Normalize();
            profile = Profiles.First(p => p.Id == ActiveProfile);
        }

        return profile;
    }

    /// <summary>
    /// Garante as invariantes do arquivo: exatamente dois perfis, com ids 1 e 2,
    /// um <see cref="ActiveProfile"/> válido, uma paleta com o tamanho certo e
    /// campos de texto não nulos.
    ///
    /// Chamado ao ler e ao gravar, para que um JSON editado à mão (ou escrito por
    /// uma versão anterior) não derrube a tela.
    /// </summary>
    public void Normalize()
    {
        Profiles ??= new List<CaptureProfile>();

        for (int id = 1; id <= 2; id++)
        {
            if (Profiles.All(p => p.Id != id))
                Profiles.Add(CaptureProfile.CreateDefault(id));
        }

        Profiles = Profiles
            .Where(p => p.Id is 1 or 2)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .OrderBy(p => p.Id)
            .ToList();

        foreach (var profile in Profiles)
        {
            profile.LastColor = string.IsNullOrWhiteSpace(profile.LastColor)
                ? CaptureProfile.DefaultColor
                : profile.LastColor;

            profile.LastHighlightColor = string.IsNullOrWhiteSpace(profile.LastHighlightColor)
                ? CaptureProfile.DefaultHighlightColor
                : profile.LastHighlightColor;
        }

        if (ActiveProfile is not (1 or 2))
            ActiveProfile = 1;

        if (CustomColors is null || CustomColors.Length != CustomColorSlots)
            CustomColors = new int[CustomColorSlots];

        if (string.IsNullOrWhiteSpace(ImageFormat))
            ImageFormat = DefaultImageFormat;

        if (PenWidth <= 0)
            PenWidth = DefaultPenWidth;

        CaptureResolution ??= string.Empty;
    }

    private static List<CaptureProfile> CreateDefaultProfiles() => new()
    {
        CaptureProfile.CreateDefault(1),
        CaptureProfile.CreateDefault(2)
    };
}