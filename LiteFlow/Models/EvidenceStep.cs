using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LiteFlow.Models;

/// <summary>
/// Um passo do cenário: o print que entrou, a nota que o descreve e as anotações
/// desenhadas por cima.
///
/// <b>A imagem não mora aqui.</b> Este objeto guarda só o <see cref="CachePath"/> —
/// o PNG vive num arquivo do cache de sessão, e o <c>ScenarioStore</c> transmite
/// esse arquivo direto para o base64 do <c>.lflow</c> na hora de salvar. Um
/// cenário de quarenta prints em 4K são centenas de megabytes; mantê-los como
/// <c>byte[]</c> em memória, ou pior, como string base64, é o que obrigava o
/// LiteFlow 1.x a chamar <c>GC.Collect()</c> a cada salvamento.
///
/// Sucessor dos dois tipos antigos <c>EvidenceData</c> (o que ia para o disco) e
/// <c>EvidenceItem</c> (o que vivia na tela). Eram dois porque o segundo carregava
/// um <c>PictureBox</c> dentro do modelo; com binding, a parte visual é do
/// ViewModel e sobra um tipo só.
/// </summary>
public sealed class EvidenceStep
{
    /// <summary>
    /// O id gerado pelo LiteShot no disparo da captura. É a chave que amarra este
    /// passo ao que o LiteJson viu na mesma hora — nunca regenerar ao carregar.
    /// </summary>
    public string StepId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Texto que acompanha a evidência no relatório.</summary>
    public string Note { get; set; } = "";

    /// <summary>
    /// <c>true</c> põe a nota depois da imagem no relatório; <c>false</c>, antes.
    /// Por passo, porque o mesmo relatório costuma ter os dois casos.
    /// </summary>
    public bool TextBelowImage { get; set; }

    /// <summary>
    /// Evidência que ilustra mas não é um passo executado: não entra na numeração
    /// do relatório. É o que faz a contagem pular de "3" para "4" com uma imagem
    /// no meio.
    /// </summary>
    public bool IsEvidenceOnly { get; set; }

    /// <summary>As anotações vetoriais desenhadas sobre este print.</summary>
    public List<Annotation> Annotations { get; set; } = new();

    /// <summary>
    /// Caminho do PNG no cache de sessão. <b>Não vai para o arquivo</b> — é estado
    /// de execução, e apontaria para uma pasta temporária que não existe mais na
    /// próxima abertura.
    /// </summary>
    [JsonIgnore]
    public string CachePath { get; set; } = "";

    /// <summary>Cópia independente, para o autosave em segundo plano não ler o que a UI está editando.</summary>
    public EvidenceStep Clone()
    {
        var copy = new EvidenceStep
        {
            StepId = StepId,
            Note = Note,
            TextBelowImage = TextBelowImage,
            IsEvidenceOnly = IsEvidenceOnly,
            CachePath = CachePath,
            Annotations = new List<Annotation>(Annotations.Count)
        };

        foreach (var annotation in Annotations)
            copy.Annotations.Add(annotation.Clone());

        return copy;
    }
}