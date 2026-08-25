namespace LiteFlow.Models;

/// <summary>
/// Os nomes de campo e a versão do formato <c>.lflow</c>, num lugar só.
///
/// Existe para que o leitor e o escritor do <c>ScenarioStore</c> não repitam
/// literais de string — errar uma letra num dos dois lados produz o pior tipo de
/// defeito silencioso: um arquivo que grava certo e carrega vazio.
/// </summary>
public static class ScenarioSchema
{
    /// <summary>
    /// Versão atual. A <b>1</b> é o formato do LiteFlow 1.x (sem este campo e sem
    /// anotações); a <b>2</b> acrescenta <c>SchemaVersion</c>, <c>ScenarioId</c> e
    /// <c>Annotations</c> — tudo aditivo, então a leitura da 1 continua válida.
    /// </summary>
    public const int CurrentVersion = 2;

    public const string SchemaVersion = "SchemaVersion";
    public const string ScenarioId = "ScenarioId";

    /// <summary>Nome antigo do <see cref="ScenarioId"/>. Lido e gravado para compatibilidade.</summary>
    public const string FileName = "FileName";

    public const string TemplatePath = "TemplatePath";
    public const string FilePrefix = "FilePrefix";
    public const string TestCaseName = "TestCaseName";
    public const string QAName = "QAName";
    public const string TestDate = "TestDate";
    public const string Comments = "Comments";
    public const string ReportLayout = "ReportLayout";
    public const string MobileColumns = "MobileColumns";
    public const string Steps = "Steps";

    public const string StepId = "StepId";
    public const string ImageData = "ImageDataBase64";
    public const string Note = "Note";
    public const string TextBelowImage = "TextBelowImage";
    public const string IsEvidenceOnly = "IsEvidenceOnly";
    public const string Annotations = "Annotations";
}