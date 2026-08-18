using LiteQASuite.Core.Events;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Session;

namespace LiteQASuite.Core.Modules;

/// <summary>
/// Tudo o que um módulo recebe para conversar com o resto da aplicação, entregue
/// pelo composition root ao construí-lo. É o único parâmetro do construtor de
/// qualquer <see cref="IModule"/>: bundlar os serviços aqui mantém a assinatura
/// estável entre os módulos — e entre os chats que os desenvolvem —, porque
/// acrescentar um serviço no futuro não muda o construtor de ninguém.
///
/// Todos os membros são contratos do Core; o módulo nunca conhece a implementação
/// concreta nem qualquer tipo do Shell.
/// </summary>
/// <param name="Events">
/// Barramento de eventos, para falar com outros módulos sem conhecê-los
/// (publicar/assinar tipos de evento compartilhados).
/// </param>
/// <param name="Session">
/// Memória compartilhada da sessão: estado que persiste enquanto o app roda
/// e que um módulo pode ler de outro.
/// </param>
/// <param name="Language">
/// i18n central. O módulo obtém suas strings já escopadas chamando
/// <c>Language.ForModule(Id)</c> no próprio construtor, e reage à troca de idioma
/// assinando <c>Language.LanguageChanged</c>.
/// </param>
public sealed record ModuleContext(
    IEventBus Events,
    ISessionContext Session,
    ILanguageManager Language);