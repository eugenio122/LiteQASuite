using System.Windows.Interop;

namespace LiteShot.Platform;

/// <summary>
/// O "aluguel" das teclas do overlay: Ctrl+A, Ctrl+C, Ctrl+S e Esc passam a
/// pertencer ao LiteShot enquanto a lente está aberta, e voltam ao sistema quando
/// ela fecha.
///
/// <b>Por que registro global e não KeyBinding.</b> Um <c>KeyBinding</c> depende de
/// a janela ter foco de teclado. O overlay é uma janela sem borda, topmost, sobre
/// tudo — e programas que roubam <i>foreground</i> o tempo todo, como o Teams com
/// suas notificações de mensagem e chamada, derrubam esse foco. Quando isso
/// acontece, o Ctrl+C simplesmente para de chegar e o usuário fica preso na lente.
/// Foi o problema que motivou este modelo no LiteShot antigo.
///
/// O <c>RegisterHotKey</c> é imune porque não depende de foco: o Windows entrega a
/// mensagem a quem registrou, esteja quem estiver na frente.
///
/// <b>A devolução é garantida pelo <see cref="IDisposable"/>.</b> No código antigo
/// havia três chamadas de desregistro espalhadas — uma no fechamento e duas
/// marcadas como <i>failsafe</i> — porque qualquer caminho de saída novo corria o
/// risco de esquecer de devolver as teclas. Aqui há um dono só.
/// </summary>
public sealed class OverlayHotkeys : IDisposable
{
    /// <summary>Uma tecla alugada e o que ela significa para o overlay.</summary>
    public enum Command
    {
        SelectCurrentMonitor,
        Copy,
        Save,
        Undo,
        Redo,
        Cancel
    }

    // Ids acima de 100 para não colidir com o do disparo global (que é 1).
    private const int IdSelectAll = 101;
    private const int IdCopy = 102;
    private const int IdSave = 103;
    private const int IdCancel = 104;
    private const int IdUndo = 105;
    private const int IdRedo = 106;

    private const uint VkA = 0x41;
    private const uint VkC = 0x43;
    private const uint VkS = 0x53;
    private const uint VkY = 0x59;
    private const uint VkZ = 0x5A;
    private const uint VkEscape = 0x1B;

    private readonly HwndSource _source;
    private readonly List<int> _registered = new();
    private bool _disposed;

    /// <summary>Disparado quando uma das teclas alugadas é pressionada.</summary>
    public event Action<Command>? Triggered;

    /// <param name="source">
    /// A janela que recebe as mensagens. Com vários monitores há várias janelas de
    /// overlay; o aluguel é feito numa só — qualquer uma serve, porque a entrega
    /// não depende de foco.
    /// </param>
    public OverlayHotkeys(HwndSource source)
    {
        _source = source;
        _source.AddHook(OnWindowMessage);

        Rent(IdSelectAll, NativeMethods.MOD_CONTROL, VkA);
        Rent(IdCopy, NativeMethods.MOD_CONTROL, VkC);
        Rent(IdSave, NativeMethods.MOD_CONTROL, VkS);
        Rent(IdUndo, NativeMethods.MOD_CONTROL, VkZ);
        Rent(IdRedo, NativeMethods.MOD_CONTROL, VkY);
        Rent(IdCancel, NativeMethods.MOD_NONE, VkEscape);
    }

    private void Rent(int id, uint modifier, uint key)
    {
        if (NativeMethods.RegisterHotKey(_source.Handle, id, modifier, key))
            _registered.Add(id);

        // Uma tecla recusada não impede o overlay de funcionar — o usuário perde
        // aquele atalho específico e continua com os botões da barra.
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_HOTKEY)
            return IntPtr.Zero;

        var command = wParam.ToInt32() switch
        {
            IdSelectAll => (Command?)Command.SelectCurrentMonitor,
            IdCopy => Command.Copy,
            IdSave => Command.Save,
            IdUndo => Command.Undo,
            IdRedo => Command.Redo,
            IdCancel => Command.Cancel,
            _ => null
        };

        if (command is null)
            return IntPtr.Zero;

        handled = true;
        Triggered?.Invoke(command.Value);
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;

        foreach (var id in _registered)
            NativeMethods.UnregisterHotKey(_source.Handle, id);

        _registered.Clear();
        _source.RemoveHook(OnWindowMessage);

        _disposed = true;
    }
}