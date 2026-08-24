namespace LiteQASuite.Core.Notifications;

/// <summary>
/// Natureza de uma notificação, para a casca escolher o tratamento visual.
/// A diferenciação de <see cref="Warning"/>/<see cref="Error"/> depende dos
/// brushes `Brush.Warning`/`Brush.Danger` — enquanto eles não existem, a casca
/// trata todas com o acento padrão.
/// </summary>
public enum NotificationKind
{
    Info,
    Success,
    Warning,
    Error
}