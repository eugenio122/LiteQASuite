using LiteQASuite.Core.Notifications;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace LiteQASuite.Shell.Notifications;

/// <summary>
/// Implementação da casca do <see cref="INotificationService"/>. Empilha toasts
/// no canto inferior direito da área de trabalho, cada um com auto-dispensa.
/// Marshala para a thread de UI, então módulos podem chamá-lo de qualquer thread
/// (ex.: o LiteShot publicando de uma thread de segundo plano).
/// </summary>
public sealed class NotificationService : INotificationService
{
    private const double EdgeMargin = 16;
    private const double Gap = 8;

    private readonly Dispatcher _dispatcher;
    private readonly List<ToastWindow> _active = new();

    public NotificationService()
    {
        _dispatcher = Application.Current.Dispatcher;
    }

    public void Show(string message, NotificationKind kind = NotificationKind.Info)
    {
        // Fire-and-forget na thread de UI: não bloqueia quem publicou de fora dela.
        _dispatcher.BeginInvoke(() => ShowCore(message, kind));
    }

    private void ShowCore(string message, NotificationKind kind)
    {
        var toast = new ToastWindow(message, kind);
        toast.Closed += (_, _) =>
        {
            _active.Remove(toast);
            Reposition();
        };

        _active.Add(toast);
        toast.Show();     // Opacity 0: fica invisível enquanto mede e posiciona.
        Reposition();
        toast.FadeIn();   // Aparece já no lugar certo, sem flicker.
    }

    private void Reposition()
    {
        var area = SystemParameters.WorkArea;
        var y = area.Bottom - EdgeMargin;

        // Do mais novo (embaixo) para o mais antigo (em cima).
        for (var i = _active.Count - 1; i >= 0; i--)
        {
            var toast = _active[i];
            y -= toast.ActualHeight;
            toast.Left = area.Right - toast.ActualWidth - EdgeMargin;
            toast.Top = y;
            y -= Gap;
        }
    }
}