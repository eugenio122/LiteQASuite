namespace LiteShot.ViewModels;

/// <summary>
/// Uma entrada do combo de limitador de resolução: o valor que vai para o arquivo
/// de configuração e o rótulo que o usuário lê.
///
/// A igualdade é pelo <see cref="Value"/>, e não por referência, porque a lista é
/// reconstruída quando o idioma muda (os rótulos são localizados) — sem isso, o
/// ComboBox perderia a seleção a cada troca de idioma.
/// </summary>
public sealed class ResolutionOption : IEquatable<ResolutionOption>
{
    /// <summary>Valor persistido: <c>"Auto"</c> ou <c>"LARGURAxALTURA"</c>.</summary>
    public string Value { get; }

    /// <summary>Texto exibido, já no idioma ativo.</summary>
    public string Label { get; }

    public ResolutionOption(string value, string label)
    {
        Value = value;
        Label = label;
    }

    public bool Equals(ResolutionOption? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as ResolutionOption);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Label;
}