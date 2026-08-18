using System.Collections.Concurrent;

namespace LiteQASuite.Core.Session;

/// <summary>
/// Implementação em memória do <see cref="ISessionContext"/>. Usa
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> porque módulos com threads de
/// fundo (ex.: as de captura do LiteJson) podem ler e gravar concorrentemente
/// durante a sessão.
/// </summary>
public sealed class SessionContext : ISessionContext
{
    private readonly ConcurrentDictionary<string, object> _data = new();

    public void Set(string key, object value) => _data[key] = value;

    public object? Get(string key) => _data.TryGetValue(key, out var value) ? value : null;

    public bool TryGet<T>(string key, out T value)
    {
        if (_data.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }
}