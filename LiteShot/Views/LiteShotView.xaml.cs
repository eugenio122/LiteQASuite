using System.Windows.Controls;
using System.Windows.Input;
using LiteShot.Platform;
using LiteShot.ViewModels;

namespace LiteShot.Views;

/// <summary>
/// Tela de configurações do LiteShot.
///
/// O code-behind faz uma coisa só: capturar a tecla digitada na caixinha de
/// atalho e repassá-la ao ViewModel. Interceptar teclado não se faz por binding,
/// e isto é plumbing de entrada — nenhuma regra mora aqui.
/// </summary>
public partial class LiteShotView : UserControl
{
    public LiteShotView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Captura a maioria das teclas. As modificadoras sozinhas são ignoradas:
    /// pressionar só o Ctrl não é um atalho, é o começo de um.
    /// </summary>
    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Alt chega como Key.System; a tecla real vem no SystemKey.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (IsModifierKey(key))
            return;

        // O PrintScreen é tratado na soltura — ver HotkeyBox_PreviewKeyUp.
        if (key == Key.Snapshot)
            return;

        Send(key);
    }

    /// <summary>
    /// Captura o PrintScreen.
    ///
    /// O Windows não entrega o PrintScreen na pressão para aplicativos comuns,
    /// só na soltura — é exatamente por isso que remapear o atalho de volta para
    /// PrintScreen era difícil no LiteShot antigo, e por que existe o botão
    /// "Padrão". Tratando o KeyUp, a tecla passa a poder ser escolhida
    /// normalmente; o botão continua ali como caminho rápido.
    /// </summary>
    private void HotkeyBox_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key != Key.Snapshot)
            return;

        e.Handled = true;
        Send(key);
    }

    private void Send(Key key)
    {
        if (DataContext is not LiteShotViewModel viewModel)
            return;

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        var modifiers = HotkeyRules.ToModifierFlags(Keyboard.Modifiers);

        viewModel.ApplyHotkey(modifiers, virtualKey);
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt
            or Key.LWin or Key.RWin
            or Key.System;
}