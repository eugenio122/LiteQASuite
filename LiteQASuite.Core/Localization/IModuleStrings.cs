namespace LiteQASuite.Core.Localization;

/// <summary>
/// Acessor de strings já preso ao id de um módulo. O módulo chama
/// <see cref="GetString(string)"/> sem repetir o próprio id e sem alcançar a
/// seção de outro módulo. Obtido via <see cref="ILanguageManager.ForModule(string)"/>.
/// </summary>
public interface IModuleStrings
{
    /// <summary>Texto da chave no idioma ativo (cai para pt-BR, e por fim a própria chave).</summary>
    string GetString(string key);

    /// <summary>Igual, formatando com <paramref name="args"/> (ex.: "{0}").</summary>
    string GetString(string key, params object[] args);
}