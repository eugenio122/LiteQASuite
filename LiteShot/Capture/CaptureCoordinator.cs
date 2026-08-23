using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiteQASuite.Core.Events;
using LiteQASuite.Core.Localization;
using LiteShot.Models;
using LiteShot.Platform;
using LiteShot.Settings;
using LiteShot.Views;
using Microsoft.Win32;

namespace LiteShot.Capture;

/// <summary>
/// Orquestra uma captura de ponta a ponta: tirar a foto, abrir as janelas de
/// overlay, alugar as teclas, aplicar o perfil ativo e decidir o que fazer com o
/// resultado.
///
/// É a costura entre as peças. A <see cref="CaptureSession"/> sabe o que está
/// selecionado e desenhado; o <see cref="ImageComposer"/> sabe recortar e
/// codificar; o coordenador é quem conhece a ordem das coisas e é o único que
/// toca no arquivo de configuração.
///
/// A ordem importa mais do que parece. O trabalho caro — recortar, rasterizar,
/// codificar — só começa <b>depois</b> de o overlay sair da frente. E o foco volta
/// para a janela que estava ativa antes, para o Windows não trazer o LiteQASuite
/// para a frente sozinho.
/// </summary>
public sealed class CaptureCoordinator : IDisposable
{
    private readonly IModuleStrings _strings;
    private readonly SettingsStore _store;
    private readonly LiteShotSettings _settings;
    private readonly IEventBus _events;

    private readonly List<OverlayWindow> _windows = new();

    private CaptureSession? _session;
    private OverlayHotkeys? _rentedKeys;
    private IntPtr _previousForeground;
    private bool _disposed;

    public CaptureCoordinator(
        IModuleStrings strings, SettingsStore store, LiteShotSettings settings, IEventBus events)
    {
        _strings = strings;
        _store = store;
        _settings = settings;
        _events = events;
    }

    /// <summary>Uma captura está em andamento.</summary>
    public bool IsCapturing => _session is not null;

    /// <summary>
    /// Começa uma captura. Chamado pelo atalho global.
    ///
    /// Se já houver uma em andamento, não faz nada — apertar o atalho duas vezes
    /// não deve empilhar overlays.
    /// </summary>
    public void Start()
    {
        if (_disposed || IsCapturing)
            return;

        try
        {
            _previousForeground = NativeMethods.GetForegroundWindow();

            var monitors = Monitors.All();
            var bounds = Monitors.VirtualBounds(monitors);

            // O grab é síncrono e rápido. A rasterização e a codificação, que são o
            // caro, ficam para depois de o overlay fechar.
            var screenshot = ScreenGrabber.Capture(bounds, _settings.CaptureCursor);
            var source = BitmapInterop.ToBitmapSource(screenshot);
            screenshot.Dispose();

            _session = new CaptureSession(source, bounds, monitors, _strings);
            _session.CopyRequested += OnCopyRequested;
            _session.SaveRequested += OnSaveRequested;
            _session.CancelRequested += OnCancelRequested;
            _session.ProfileChangeRequested += OnProfileChangeRequested;

            _session.PenWidth = _settings.PenWidth;
            _session.ActiveProfileId = _settings.ActiveProfile;
            ApplyProfile(_settings.GetActiveProfile());

            // SÍNCRONO, e antes de o overlay aparecer. Quem assina precisa congelar
            // o estado da tela — árvore de UI, DOM — enquanto ela ainda está
            // intacta. Publicar em Task.Run aqui deixaria a lente escurecer antes
            // de o assinante terminar, e era essa a fragilidade do fluxo antigo.
            _events.Publish(new CaptureStartedEvent(_session.StepId, DateTime.Now));

            OpenOverlays(monitors);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LiteShot] Falha ao iniciar a captura: {ex.Message}");

            // Se o evento de início chegou a sair, alguém já criou um passo
            // pendente. Falhar em silêncio o deixaria pendurado para sempre.
            if (_session is not null)
                PublishCanceled(_session.StepId, CaptureCancelReason.UserCanceled);

            CloseOverlays(restoreForeground: true);
            Cleanup();
        }
    }

    // ================================================================
    // PERFIS
    // ================================================================

    /// <summary>
    /// Traz o perfil para a sessão: cores, orientação e posição da barra, e a área
    /// de seleção guardada. É o que faz alternar entre P1 e P2 valer a pena — o
    /// espaço de trabalho inteiro vem junto.
    /// </summary>
    private void ApplyProfile(CaptureProfile profile)
    {
        if (_session is null)
            return;

        _session.NavbarVertical = profile.NavbarVertical;
        _session.DrawColor = ParseColor(profile.LastColor, Colors.Red);
        _session.HighlightColor = ParseColor(profile.LastHighlightColor, Colors.Yellow);

        if (profile.KeepSelection && profile.HasSelection)
        {
            _session.RestoreSelection(
                profile.SelectionX, profile.SelectionY,
                profile.SelectionWidth, profile.SelectionHeight);
        }

        if (profile.KeepNavbarPosition && profile.HasNavbarPosition)
            _session.RestoreNavbar(new Point(profile.NavbarX, profile.NavbarY));
        else
            _session.ResetNavbar();
    }

    /// <summary>
    /// Guarda no perfil o que ele aprende sozinho: as cores sempre, e a geometria
    /// quando o perfil pede para lembrar. A configuração — os três checkboxes — é
    /// da tela e nunca é tocada aqui.
    /// </summary>
    private void PersistProfile(CaptureProfile profile)
    {
        if (_session is null)
            return;

        profile.LastColor = ToHex(_session.DrawColor);
        profile.LastHighlightColor = ToHex(_session.HighlightColor);

        if (profile.KeepSelection && _session.HasSelection)
        {
            var selection = _session.Selection;
            profile.SelectionX = selection.Left;
            profile.SelectionY = selection.Top;
            profile.SelectionWidth = selection.Width;
            profile.SelectionHeight = selection.Height;
        }

        if (profile.KeepNavbarPosition && _session.NavbarPosition is { } navbar)
        {
            profile.NavbarX = (int)navbar.X;
            profile.NavbarY = (int)navbar.Y;
        }

        _settings.PenWidth = (int)Math.Round(_session.PenWidth);
        _store.Save(_settings);
    }

    /// <summary>
    /// O usuário trocou de perfil no meio da captura. O perfil que sai leva consigo
    /// o que aprendeu; o que entra traz o seu espaço de trabalho.
    /// </summary>
    private void OnProfileChangeRequested(int id)
    {
        if (_session is null || id is not (1 or 2) || id == _settings.ActiveProfile)
            return;

        PersistProfile(_settings.GetActiveProfile());

        _settings.ActiveProfile = id;
        _session.ActiveProfileId = id;

        ApplyProfile(_settings.GetActiveProfile());
        _store.Save(_settings);
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        try
        {
            if (ColorConverter.ConvertFromString(hex) is Color color)
                return color;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LiteShot] Cor inválida no perfil ('{hex}'): {ex.Message}");
        }

        return fallback;
    }

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    // ================================================================
    // ABERTURA
    // ================================================================

    private void OpenOverlays(IReadOnlyList<MonitorInfo> monitors)
    {
        foreach (var monitor in monitors)
        {
            var window = new OverlayWindow(_session!, monitor);
            _windows.Add(window);
            window.Show();
        }

        // A primeira recebe o foco e hospeda o aluguel das teclas. Qualquer uma
        // serve para o aluguel: o RegisterHotKey entrega a mensagem à janela que
        // registrou, independentemente de quem está em primeiro plano — que é
        // exatamente a razão de usá-lo em vez de KeyBinding.
        var first = _windows.FirstOrDefault();
        if (first is null)
            return;

        first.Activate();

        if (first.Source is { } source)
        {
            _rentedKeys = new OverlayHotkeys(source);
            _rentedKeys.Triggered += OnRentedKey;
        }
    }

    private void OnRentedKey(OverlayHotkeys.Command command)
    {
        if (_session is null)
            return;

        switch (command)
        {
            case OverlayHotkeys.Command.SelectCurrentMonitor:
                _session.SelectMonitorUnderCursor();
                break;

            case OverlayHotkeys.Command.Copy:
                if (_session.HasSelection) OnCopyRequested();
                break;

            case OverlayHotkeys.Command.Save:
                if (_session.HasSelection) OnSaveRequested();
                break;

            case OverlayHotkeys.Command.Undo:
                _session.Annotations.Undo();
                break;

            case OverlayHotkeys.Command.Redo:
                _session.Annotations.Redo();
                break;

            case OverlayHotkeys.Command.Cancel:
                // A sessão decide: com a caixa de texto aberta, o Esc é dela.
                _session.RequestCancel();
                break;
        }
    }

    // ================================================================
    // DESFECHO
    // ================================================================

    /// <summary>
    /// Copiar: área de transferência e, quando os eventos do Core existirem,
    /// também o LiteFlow. É o commit da captura no cenário.
    /// </summary>
    private void OnCopyRequested()
    {
        var session = _session;
        if (session is null || !session.HasSelection)
            return;

        var stepId = session.StepId;
        var selection = session.Selection;
        var screenshot = session.Screenshot;
        var bounds = session.Bounds;
        var annotations = session.Annotations;
        var limit = _settings.CaptureResolution;

        PersistProfile(_settings.GetActiveProfile());
        CloseOverlays(restoreForeground: true);

        BitmapSource? composed = null;

        try
        {
            // A composição fica na thread de interface: ela usa DrawingVisual e
            // RenderTargetBitmap, que têm afinidade de thread. É barata — recorta e
            // rasteriza só a área escolhida. O caro é a codificação, e essa vai
            // para segundo plano logo abaixo.
            composed = ImageComposer.Compose(screenshot, bounds, selection, limit, annotations);

            // O clipboard exige thread STA — a de interface.
            Clipboard.SetImage(composed);
        }
        catch (Exception ex)
        {
            // O Windows às vezes segura a área de transferência por alguns
            // milissegundos. Travar a captura por causa disso seria pior do que
            // perder a cópia.
            Debug.WriteLine($"[LiteShot] Não foi possível copiar para a área de transferência: {ex.Message}");
        }

        if (composed is not null)
            PublishCompleted(stepId, composed, screenshot);

        Cleanup();
    }

    /// <summary>
    /// Salvar: exportação local pura. Não entra no cenário — quando os eventos
    /// existirem, este caminho publica o "cancelado" com motivo <c>SavedLocally</c>,
    /// para o LiteJson não ficar com um passo pendente pendurado.
    /// </summary>
    private void OnSaveRequested()
    {
        var session = _session;
        if (session is null || !session.HasSelection)
            return;

        var stepId = session.StepId;
        var selection = session.Selection;
        var screenshot = session.Screenshot;
        var bounds = session.Bounds;
        var annotations = session.Annotations;
        var limit = _settings.CaptureResolution;
        var preferredFormat = _settings.ImageFormat;

        PersistProfile(_settings.GetActiveProfile());

        // O overlay sai da frente antes do diálogo — mas o foco só volta depois,
        // senão a janela de gravar nasce atrás de outro programa.
        CloseOverlays(restoreForeground: false);

        try
        {
            var dialog = new SaveFileDialog
            {
                Title = "LiteShot",
                Filter = BuildFilter(preferredFormat),
                FileName = $"LiteShot_{DateTime.Now:yyyyMMdd_HHmmss}"
            };

            if (dialog.ShowDialog() == true)
            {
                var image = ImageComposer.Compose(screenshot, bounds, selection, limit, annotations);
                var format = ImageComposer.FormatFromExtension(dialog.FileName, preferredFormat);
                ImageComposer.SaveToFile(image, dialog.FileName, format);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LiteShot] Falha ao salvar a imagem: {ex.Message}");
        }

        RestoreForeground();

        // Gravar em disco é exportação, não participação no cenário. Sem este
        // aviso, o passo pendente criado no início ficaria pendurado para sempre —
        // era exatamente o que acontecia no fluxo antigo.
        PublishCanceled(stepId, CaptureCancelReason.SavedLocally);

        Cleanup();
    }

    private void OnCancelRequested()
    {
        var stepId = _session?.StepId;

        // Mesmo desistindo, o perfil guarda as cores escolhidas: elas são
        // preferência, não resultado da captura.
        PersistProfile(_settings.GetActiveProfile());

        CloseOverlays(restoreForeground: true);

        if (stepId is not null)
            PublishCanceled(stepId, CaptureCancelReason.UserCanceled);

        Cleanup();
    }

    // ================================================================
    // PIPELINE
    // ================================================================

    /// <summary>
    /// Codifica as duas imagens e anuncia a captura confirmada. Em segundo plano:
    /// o overlay já saiu da frente e o usuário voltou ao que estava fazendo, então
    /// nada aqui disputa a atenção dele.
    ///
    /// As duas imagens chegam congeladas — é o que torna seguro usá-las fora da
    /// thread de interface.
    /// </summary>
    private void PublishCompleted(string stepId, BitmapSource composed, BitmapSource cleanScreenshot)
    {
        Task.Run(() =>
        {
            try
            {
                // Sempre PNG, independentemente do formato preferido do usuário:
                // aquele ajuste é sobre o arquivo que ele salva, não sobre o que
                // trafega entre módulos. O contrato tem um formato só.
                var image = ImageComposer.Encode(composed, "PNG");
                var clean = ImageComposer.Encode(cleanScreenshot, "PNG");

                _events.Publish(new CaptureCompletedEvent(stepId, image, clean, DateTime.Now));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LiteShot] Falha ao publicar a captura: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// Anuncia que a captura terminou sem virar passo. Barato, mas assíncrono do
    /// mesmo jeito: o handler do assinante roda na thread de quem publica, e não
    /// vale segurar o usuário enquanto outro módulo limpa o estado dele.
    /// </summary>
    private void PublishCanceled(string stepId, CaptureCancelReason reason)
    {
        Task.Run(() =>
        {
            try
            {
                _events.Publish(new CaptureCanceledEvent(stepId, reason, DateTime.Now));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LiteShot] Falha ao publicar o cancelamento: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// O formato preferido vira o filtro inicial do diálogo. No LiteShot antigo a
    /// configuração era órfã: existia, era gravada, e o diálogo a ignorava.
    /// </summary>
    private static string BuildFilter(string preferredFormat)
    {
        const string png = "PNG|*.png";
        const string jpg = "JPEG|*.jpg";
        const string bmp = "BMP|*.bmp";

        return preferredFormat.ToUpperInvariant() switch
        {
            "JPEG" or "JPG" => $"{jpg}|{png}|{bmp}",
            "BMP" => $"{bmp}|{png}|{jpg}",
            _ => $"{png}|{jpg}|{bmp}"
        };
    }

    private void CloseOverlays(bool restoreForeground)
    {
        _rentedKeys?.Dispose();
        _rentedKeys = null;

        foreach (var window in _windows)
        {
            try { window.Close(); }
            catch (Exception ex) { Debug.WriteLine($"[LiteShot] Falha ao fechar overlay: {ex.Message}"); }
        }

        _windows.Clear();

        if (restoreForeground)
            RestoreForeground();
    }

    /// <summary>
    /// Devolve o foco para quem estava na frente antes da captura. Sem isto, o
    /// Windows decide sozinho — e costuma decidir por trazer o LiteQASuite.
    /// </summary>
    private void RestoreForeground()
    {
        if (_previousForeground != IntPtr.Zero)
            NativeMethods.SetForegroundWindow(_previousForeground);

        _previousForeground = IntPtr.Zero;
    }

    private void Cleanup()
    {
        if (_session is not null)
        {
            _session.CopyRequested -= OnCopyRequested;
            _session.SaveRequested -= OnSaveRequested;
            _session.CancelRequested -= OnCancelRequested;
            _session.ProfileChangeRequested -= OnProfileChangeRequested;
            _session = null;
        }

        if (_rentedKeys is not null)
        {
            _rentedKeys.Triggered -= OnRentedKey;
            _rentedKeys.Dispose();
            _rentedKeys = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        CloseOverlays(restoreForeground: false);
        Cleanup();

        _disposed = true;
    }
}