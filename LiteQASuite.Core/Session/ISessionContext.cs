using LiteQASuite.Core.Events;

namespace LiteQASuite.Core.Session;

/// <summary>
/// Memória compartilhada da sessão: onde um módulo deixa um dado que outro pode
/// ler, sem que um conheça o outro (ex.: o estado de modo escuro que o Shell
/// publica e o LiteShot consome). Sucessor do antigo <c>ILiteHostContext</c> —
/// o "Host" morreu com a Nave-Mãe; agora é só o contexto da sessão.
///
/// Vive no Core e é injetado nos módulos pelo composition root, junto do
/// <see cref="IEventBus"/>. O barramento carrega eventos pontuais; aqui fica o
/// estado que persiste ao longo da sessão.
/// </summary>
public interface ISessionContext
{
    /// <summary>Grava (ou sobrescreve) um valor sob a chave informada.</summary>
    void Set(string key, object value);

    /// <summary>Lê o valor bruto da chave, ou <c>null</c> se não existir.</summary>
    object? Get(string key);

    /// <summary>
    /// Tenta ler a chave já convertida para <typeparamref name="T"/>. Retorna
    /// <c>false</c> (com <paramref name="value"/> no default) se a chave não
    /// existe ou o tipo não confere.
    /// </summary>
    bool TryGet<T>(string key, out T value);
}