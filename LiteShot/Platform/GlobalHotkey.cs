using System.Windows.Interop;

namespace LiteShot.Platform;

/// <summary>
/// O atalho global de disparo da captura. Registra a combinação no Windows e avisa
/// quando ela é pressionada, esteja qual programa estiver na frente.
///
/// Cria uma janela <i>message-only</i> própria — invisível, sem posição, fora da
/// árvore visual da casca. É o equivalente WPF do <c>HiddenMessageWindow</c> do
/// LiteShot antigo, e é plumbing interno do módulo: a casca não sabe que ela
/// existe.
///
/// Precisa ser criada na thread de UI (o <c>HwndSource</c> exige) e descartada no
/// <c>Shutdown</c> do módulo. Se o processo morrer de forma anormal, o Windows
/// libera a tecla sozinho — o <see cref="Dispose"/> é para a saída limpa.
/// </summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int HotkeyId = 1;

    private readonly HwndSource _source;
    private bool _registered;
    private bool _disposed;

    /// <summary>Disparado quando a combinação registrada é pressionada.</summary>
    public event Action? Pressed;

    /// <summary>
    /// <c>false</c> quando o Windows recusou o registro — normalmente porque outro
    /// programa já tem a combinação. O código antigo não checava isso, e o sintoma
    /// era um aplicativo que simplesmente não respondia à tecla, sem explicação.
    /// </summary>
    public bool IsRegistered => _registered;

    /// <summary>A combinação atualmente registrada.</summary>
    public uint Modifier { get; private set; }

    /// <summary>A tecla atualmente registrada.</summary>
    public uint Key { get; private set; }

    public GlobalHotkey()
    {
        var parameters = new HwndSourceParameters("LiteShot.HotkeyListener")
        {
            ParentWindow = NativeMethods.HWND_MESSAGE,
            Width = 0,
            Height = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(OnWindowMessage);
    }

    /// <summary>
    /// Troca a combinação registrada. Solta a anterior antes, então pode ser
    /// chamado quantas vezes for preciso — é o que acontece toda vez que o usuário
    /// salva a tela de configurações.
    /// </summary>
    /// <param name="enabled">
    /// Quando <c>false</c>, apenas devolve a tecla ao sistema e não registra nada.
    /// É o checkbox "capturar por atalho global" desligado.
    /// </param>
    /// <returns><c>true</c> se ficou registrado; <c>false</c> se foi recusado.</returns>
    public bool Apply(uint modifier, uint key, bool enabled)
    {
        Unregister();

        Modifier = modifier;
        Key = key;

        if (!enabled || key == 0)
            return false;

        _registered = NativeMethods.RegisterHotKey(_source.Handle, HotkeyId, modifier, key);
        return _registered;
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke();
        }

        return IntPtr.Zero;
    }

    private void Unregister()
    {
        if (!_registered) return;

        NativeMethods.UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    public void Dispose()
    {
        if (_disposed) return;

        Unregister();
        _source.RemoveHook(OnWindowMessage);
        _source.Dispose();

        _disposed = true;
    }
}
