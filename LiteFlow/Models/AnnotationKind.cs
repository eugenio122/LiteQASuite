namespace LiteFlow.Models;

/// <summary>
/// As ferramentas de anotação do editor. São as mesmas seis do LiteShot, de
/// propósito: quem anota no overlay e quem anota depois, no LiteFlow, não deveria
/// ter que aprender dois vocabulários.
///
/// <b>Recorte e redimensionamento não estão aqui</b> — eles alteram o bitmap base
/// em vez de acrescentar um objeto por cima, então não são anotações.
/// </summary>
public enum AnnotationKind
{
    /// <summary>Traço livre: a sequência inteira de pontos do arrasto.</summary>
    Pen,

    /// <summary>Reta entre dois pontos.</summary>
    Line,

    /// <summary>Reta com ponta de seta no fim.</summary>
    Arrow,

    /// <summary>Retângulo vazado entre dois pontos.</summary>
    Shape,

    /// <summary>Marca-texto: traço largo e translúcido.</summary>
    Highlight,

    /// <summary>Texto ancorado num ponto.</summary>
    Text
}