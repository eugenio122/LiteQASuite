using System.Collections.Generic;

namespace LiteFlow.Models;

/// <summary>
/// O conteúdo do <c>.lflow</c> — os metadados do relatório mais os passos. É o
/// artefato do qual o LiteFlow é dono do nascimento à morte; o <c>.json</c> irmão,
/// na mesma pasta, é do LiteJson e não é assunto deste tipo.
///
/// <b>Os nomes das propriedades são os do formato 1.x, de propósito.</b> Manter
/// <c>FilePrefix</c>, <c>QAName</c> e <c>TestDate</c> — em vez de rebatizá-los para
/// algo mais bonito — é o que faz os cenários já gravados abrirem no 2.0 sem
/// conversão. O único campo que ganhou nome novo é o <see cref="ScenarioId"/>
/// (antigo "Nome do Arquivo"), e ele é lido dos dois jeitos e gravado dos dois
/// jeitos, para a compatibilidade valer nas duas direções.
/// </summary>
public sealed class ScenarioDocument
{
    /// <summary>
    /// Versão do formato. Ausente nos arquivos do 1.x, que são lidos como versão 1.
    /// </summary>
    public int SchemaVersion { get; set; } = ScenarioSchema.CurrentVersion;

    /// <summary>
    /// O ID do cenário — é ele que nomeia a pasta e o próprio arquivo. Gravado
    /// também como <c>FileName</c> por compatibilidade.
    /// </summary>
    public string ScenarioId { get; set; } = "";

    /// <summary>Caminho do template <c>.docx</c> usado na exportação. Vazio = documento em branco.</summary>
    public string TemplatePath { get; set; } = "";

    /// <summary>Prefixo do nome do arquivo exportado (não da pasta do cenário).</summary>
    public string FilePrefix { get; set; } = "";

    /// <summary>Caso de teste — vira a tag <c>{CASO}</c> no template.</summary>
    public string TestCaseName { get; set; } = "";

    /// <summary>Quem executou — vira a tag <c>{QA}</c>. O nome do campo é herança do 1.x.</summary>
    public string QAName { get; set; } = "";

    /// <summary>
    /// Data da execução — vira a tag <c>{DATA}</c>. É <c>string</c>, e não
    /// <c>DateTime</c>, porque o usuário digita o formato que o time usa e o
    /// template só reproduz o texto; converter para data e de volta só criaria
    /// oportunidade de o valor mudar sozinho.
    /// </summary>
    public string TestDate { get; set; } = "";

    /// <summary>Observações — vira a tag <c>{OBS}</c>.</summary>
    public string Comments { get; set; } = "";

    /// <summary>Como as evidências se organizam no relatório.</summary>
    public ReportLayout ReportLayout { get; set; } = ReportLayout.Padrao;

    /// <summary>Quantidade de colunas quando o layout é Mobile.</summary>
    public int MobileColumns { get; set; } = 2;

    /// <summary>Os passos, na ordem em que aparecem no relatório.</summary>
    public List<EvidenceStep> Steps { get; set; } = new();

    /// <summary>
    /// Cópia independente do documento inteiro. O autosave roda em segundo plano
    /// enquanto o usuário continua digitando e arrastando: sem esta fotografia,
    /// o escritor percorreria uma lista que a interface está alterando.
    /// </summary>
    public ScenarioDocument Clone()
    {
        var copy = new ScenarioDocument
        {
            SchemaVersion = SchemaVersion,
            ScenarioId = ScenarioId,
            TemplatePath = TemplatePath,
            FilePrefix = FilePrefix,
            TestCaseName = TestCaseName,
            QAName = QAName,
            TestDate = TestDate,
            Comments = Comments,
            ReportLayout = ReportLayout,
            MobileColumns = MobileColumns,
            Steps = new List<EvidenceStep>(Steps.Count)
        };

        foreach (var step in Steps)
            copy.Steps.Add(step.Clone());

        return copy;
    }
}