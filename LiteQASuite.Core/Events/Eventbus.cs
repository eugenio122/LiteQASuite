using System;
using System.Collections.Generic;

namespace LiteQASuite.Core.Events;

/// <summary>
/// Implementação em memória do <see cref="IEventBus"/>. Mantém, por tipo de
/// evento, a lista de handlers assinados.
///
/// Thread-safety: o dicionário é protegido por lock, porque há módulos que
/// publicam de threads de fundo (ex.: as threads de captura/hidratação do
/// LiteJson). A invocação acontece sobre um snapshot, fora do lock, para não
/// travar o barramento nem causar reentrância caso um handler assine/publique
/// outro evento durante o disparo.
///
/// O barramento NÃO faz marshaling para a thread de UI: um handler que toca a
/// tela é responsável por usar o Dispatcher. Isso mantém o Core sem conhecer WPF.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
    private readonly object _gate = new();

    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers = new List<Delegate>();
                _subscribers[typeof(TEvent)] = handlers;
            }
            handlers.Add(handler);
        }
    }

    public void Publish<TEvent>(TEvent eventItem)
    {
        Delegate[] snapshot;
        lock (_gate)
        {
            if (!_subscribers.TryGetValue(typeof(TEvent), out var handlers))
                return;
            snapshot = handlers.ToArray();
        }

        foreach (var handler in snapshot)
            ((Action<TEvent>)handler)(eventItem);
    }
}