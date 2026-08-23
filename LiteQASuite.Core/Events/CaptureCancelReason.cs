namespace LiteQASuite.Core.Events;

/// <summary>
/// Por que uma captura terminou sem virar um passo do cenário.
///
/// A distinção existe porque os dois casos parecem iguais para quem assina — em
/// ambos o passo pendente deve ser descartado — mas contam histórias diferentes no
/// log. Sem ela, um usuário que salvou vinte prints locais apareceria como alguém
/// que cancelou vinte capturas.
/// </summary>
public enum CaptureCancelReason
{
    /// <summary>O usuário desistiu: Esc ou o botão Fechar.</summary>
    UserCanceled,

    /// <summary>
    /// O usuário gravou um arquivo local em vez de confirmar. A imagem existe no
    /// disco, mas não entra no cenário.
    /// </summary>
    SavedLocally
}