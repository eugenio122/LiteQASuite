using LiteQASuite.Core.Notifications;
using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LiteQASuite.Shell.Notifications;

public partial class ToastWindow : Window
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(3.5);
    private readonly DispatcherTimer _timer;
    private bool _closing;

    public ToastWindow(string message, NotificationKind kind)
    {
        InitializeComponent();
        MessageText.Text = message;

        // 'kind' fica reservado para diferenciação visual (Warning/Error) quando
        // os brushes Brush.Warning/Danger chegarem no sistema de design. Por ora,
        // todas as naturezas usam a barra de acento padrão.

        _timer = new DispatcherTimer { Interval = Lifetime };
        _timer.Tick += (_, _) => BeginClose();
    }

    /// <summary>Anima a entrada e inicia a contagem para dispensa. Chamado após posicionar.</summary>
    public void FadeIn()
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        _timer.Start();
    }

    private void BeginClose()
    {
        if (_closing) return;
        _closing = true;
        _timer.Stop();

        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(220));
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }
}