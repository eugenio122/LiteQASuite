namespace LiteShot.Capture;

/// <summary>
/// As ferramentas de anotação do overlay. São as mesmas seis do LiteShot antigo —
/// confirmado na auditoria que nunca existiu borrão nem pixelate.
/// </summary>
public enum AnnotationKind
{
    /// <summary>Traço livre, seguindo o mouse.</summary>
    Pen,

    /// <summary>Linha reta entre dois pontos.</summary>
    Line,

    /// <summary>Linha reta com ponta de seta no fim.</summary>
    Arrow,

    /// <summary>Retângulo vazado.</summary>
    Shape,

    /// <summary>Traço livre grosso e translúcido, como marca-texto.</summary>
    Highlighter,

    /// <summary>Texto digitado sobre a imagem.</summary>
    Text
}