namespace LiteQASuite.Core.Notifications;

/// <summary>
/// Serviço de notificações transitórias (toasts) da casca, oferecido a todos os
/// módulos via <c>ModuleContext</c>. O módulo decide o quê e quando notificar
/// (ex.: o LiteShot ao copiar/salvar, respeitando sua própria opção "mostrar
/// notificações"); a casca decide como isso aparece na tela.
///
/// Pode ser chamado de qualquer thread — a implementação marshala para a UI.
/// </summary>
public interface INotificationService
{
    /// <summary>Exibe uma notificação transitória com a mensagem e a natureza informadas.</summary>
    void Show(string message, NotificationKind kind = NotificationKind.Info);
}