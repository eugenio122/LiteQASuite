namespace LiteFlow.Models;

/// <summary>
/// As preferências do LiteFlow — o que atravessa cenários, e não o que pertence a
/// um cenário específico. Vive em <c>%AppData%\LiteQASuite\liteflow_settings.json</c>.
///
/// Sucessor do <c>LiteFlow_Data\settings.ini</c> do 1.x, que era um arquivo de
/// linhas <b>posicionais</b>: <c>lines[12]</c> era o idioma, <c>lines[13]</c> a
/// pasta de exportação, e inserir um campo no meio embaralhava tudo o que vinha
/// depois. Como JSON nomeado, acrescentar campo deixa de ser risco.
///
/// <b>Some daqui o que virou da casca:</b> modo escuro e idioma são globais do
/// LiteQASuite agora — o módulo não guarda nem opina.
/// </summary>
public sealed class LiteFlowSettings
{
    /// <summary>Template <c>.docx</c> padrão, oferecido a cada cenário novo.</summary>
    public string DefaultTemplatePath { get; set; } = "";

    /// <summary>Nome do executor sugerido no diálogo de iniciar cenário.</summary>
    public string DefaultExecutor { get; set; } = "";

    /// <summary>Prefixo sugerido no diálogo de iniciar cenário.</summary>
    public string DefaultPrefix { get; set; } = "";

    /// <summary>Layout sugerido no diálogo de iniciar cenário.</summary>
    public ReportLayout DefaultLayout { get; set; } = ReportLayout.Padrao;

    /// <summary>Número de colunas sugerido quando o layout é Mobile.</summary>
    public int DefaultMobileColumns { get; set; } = 2;

    /// <summary>
    /// Último squad/projeto usado, para o diálogo já abrir nele. Quem passa a
    /// semana cobrindo outro time não quer reescolher a cada cenário.
    /// </summary>
    public string LastSquad { get; set; } = "";

    /// <summary>
    /// Último ciclo usado, para o diálogo já abrir nele. Quem testa uma sprint
    /// inteira cria dezenas de cenários no mesmo ciclo.
    /// </summary>
    public string LastCycle { get; set; } = "";

    /// <summary>Cor da caneta, em <c>#AARRGGBB</c>. Usada a partir da fatia 2.</summary>
    public string PenColor { get; set; } = "#FFFF0000";

    /// <summary>Espessura do traço. Usada a partir da fatia 2.</summary>
    public double PenThickness { get; set; } = 4;

    /// <summary>Fonte da ferramenta de texto. Usada a partir da fatia 2.</summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>Corpo da fonte da ferramenta de texto. Usada a partir da fatia 2.</summary>
    public double FontSize { get; set; } = 14;

    /// <summary>
    /// Corrige valores impossíveis vindos de um arquivo editado à mão ou de uma
    /// versão anterior. Nunca lança: um arquivo estranho vira o padrão, não uma
    /// tela que não abre.
    /// </summary>
    public void Normalize()
    {
        if (MobileColumnsOutOfRange()) DefaultMobileColumns = 2;
        if (PenThickness is < 1 or > 40) PenThickness = 4;
        if (FontSize is < 6 or > 200) FontSize = 14;
        if (string.IsNullOrWhiteSpace(FontFamily)) FontFamily = "Segoe UI";
        if (string.IsNullOrWhiteSpace(PenColor)) PenColor = "#FFFF0000";
        if (!System.Enum.IsDefined(typeof(ReportLayout), DefaultLayout)) DefaultLayout = ReportLayout.Padrao;

        DefaultTemplatePath ??= "";
        DefaultExecutor ??= "";
        DefaultPrefix ??= "";
        LastSquad ??= "";
        LastCycle ??= "";
    }

    private bool MobileColumnsOutOfRange() => DefaultMobileColumns is < 1 or > 3;
}