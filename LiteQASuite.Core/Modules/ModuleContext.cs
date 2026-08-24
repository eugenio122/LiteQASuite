using LiteQASuite.Core.Events;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Notifications;
using LiteQASuite.Core.Session;
using LiteQASuite.Core.Workspace;

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
/// <param name="Notifications">
/// Notificações transitórias (toasts) da casca. O módulo decide o quê e quando
/// notificar; a casca decide como aparece.
/// </param>
/// <param name="Workspace">
/// Estrutura de pastas compartilhada (ciclos/cenários). O módulo resolve caminhos
/// por ela e é dono só do conteúdo dos próprios arquivos.
/// </param>
public sealed record ModuleContext(
    IEventBus Events,
    ISessionContext Session,
    ILanguageManager Language,
    INotificationService Notifications,
    IWorkspaceService Workspace);