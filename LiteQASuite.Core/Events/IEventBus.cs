using System;

namespace LiteQASuite.Core.Events;

/// <summary>
/// Barramento de eventos (Mediator / Pub-Sub) do LiteQASuite. Permite que os
/// módulos conversem sem se conhecerem: um publica um evento, quem se importa
/// assina. Vive no Core justamente para ser o único ponto de contato entre
/// módulos que, de resto, não se referenciam.
/// </summary>
public interface IEventBus
{
    /// <summary>Assina eventos do tipo <typeparamref name="TEvent"/>.</summary>
    void Subscribe<TEvent>(Action<TEvent> handler);

    /// <summary>Publica um evento para todos os assinantes daquele tipo.</summary>
    void Publish<TEvent>(TEvent eventItem);
}