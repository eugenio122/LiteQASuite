using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using LiteShot.Capture;
using LiteShot.Platform;

namespace LiteShot.Views;

/// <summary>
/// Uma janela de overlay, cobrindo exatamente um monitor. Numa área de trabalho com
/// três telas, existem três destas, todas olhando para a mesma
/// <see cref="CaptureSession"/>.
///
/// <b>Por que uma por monitor.</b> Uma janela WPF só, atravessando monitores de
/// escalas diferentes, tem comportamento de escala inconsistente — as propriedades
/// de posição e tamanho estão em unidades independentes de dispositivo. Com uma
/// janela por monitor, cada uma tem um fator de escala único e previsível.
///
/// <b>Por que o desenho é em code-behind.</b> A tela de configurações é um
/// formulário e ganha com binding; um overlay de recorte é interação pixel a pixel,
/// onde cada coordenada passa por uma conversão física ↔ lógica. O estado continua
/// num objeto observável de verdade (a sessão), e os botões continuam sendo
/// comandos.
/// </summary>
public partial class OverlayWindow : Window
{
    /// <summary>
    /// A paleta da ferramenta de cor. Doze tons cobrem o que uma anotação de QA
    /// precisa — vermelho para apontar erro, verde para confirmar, amarelo para
    /// marcar. O LiteShot antigo abria o seletor completo do Windows, que vem do
    /// WinForms e ficou fora por decisão da suíte.
    /// </summary>
    private static readonly Color[] Palette =
    {
        Color.FromRgb(0xEF, 0x44, 0x44), Color.FromRgb(0xF9, 0x73, 0x16),
        Color.FromRgb(0xFA, 0xCC, 0x15), Color.FromRgb(0x22, 0xC5, 0x5E),
        Color.FromRgb(0x14, 0xB8, 0xA6), Color.FromRgb(0x25, 0x63, 0xEB),
        Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0xEC, 0x48, 0x99),
        Color.FromRgb(0xFF, 0xFF, 0xFF), Color.FromRgb(0x9C, 0xA3, 0xAF),
        Color.FromRgb(0x37, 0x41, 0x51), Color.FromRgb(0x00, 0x00, 0x00)
    };

    private readonly CaptureSession _session;
    private readonly MonitorInfo _monitor;
    private readonly Rectangle[] _handles = new Rectangle[8];
    private readonly AnnotationHost _annotations;

    /// <summary>Fator que converte pixel físico em unidade lógica do WPF neste monitor.</summary>
    private double _deviceToDip = 1.0;

    private double _dipWidth;
    private double _dipHeight;

    private bool _draggingToolbar;
    private Point _gripOffset;

    private Point _textAnchor;
    private bool _closingTextInput;

    public OverlayWindow(CaptureSession session, MonitorInfo monitor)
    {
        InitializeComponent();

        _session = session;
        _monitor = monitor;
        DataContext = session;

        _annotations = new AnnotationHost(session);
        // Logo acima do escurecimento e abaixo da moldura: as anotações aparecem em
        // brilho normal dentro do recorte, e as alças continuam por cima delas.
        Root.Children.Insert(2, _annotations);

        CreateHandles();
        BuildPalette();
        WireToolbarDrag();
        WireTextInput();

        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Closed += OnClosed;

        _session.Changed += Refresh;
        _session.TextRequested += OnTextRequested;
        _session.TextEditCancelled += HideTextInput;
    }

    /// <summary>O <c>HwndSource</c> desta janela, que o aluguel de teclas usa.</summary>
    public HwndSource? Source { get; private set; }

    // ================================================================
    // CICLO DE VIDA
    // ================================================================

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        Source = HwndSource.FromHwnd(handle);

        // A matriz de conversão deste monitor. Com PerMonitorV2, cada janela tem a
        // sua — é por isso que a conversão precisa ser por janela e não global.
        if (Source?.CompositionTarget is { } target)
            _deviceToDip = target.TransformFromDevice.M11;

        _dipWidth = _monitor.Width * _deviceToDip;
        _dipHeight = _monitor.Height * _deviceToDip;

        NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HWND_TOPMOST,
            _monitor.Left, _monitor.Top, _monitor.Width, _monitor.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        Root.Width = _dipWidth;
        Root.Height = _dipHeight;

        ScreenshotLayer.Source = BitmapInterop.Crop(
            _session.Screenshot,
            _monitor.Left - _session.Bounds.Left,
            _monitor.Top - _session.Bounds.Top,
            _monitor.Width,
            _monitor.Height);

        ScreenshotLayer.Width = _dipWidth;
        ScreenshotLayer.Height = _dipHeight;

        _annotations.Width = _dipWidth;
        _annotations.Height = _dipHeight;
        _annotations.SetTransform(_deviceToDip, _monitor.Left, _monitor.Top);

        Refresh();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _session.Changed -= Refresh;
        _session.TextRequested -= OnTextRequested;
        _session.TextEditCancelled -= HideTextInput;
    }

    // ================================================================
    // MOUSE E TECLADO
    // ================================================================

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (_draggingToolbar)
            return;

        // Um clique fora da caixa de texto confirma o que foi digitado, como em
        // qualquer editor.
        CommitTextInput();

        var (x, y) = ToVirtual(e.GetPosition(Root));
        _session.PointerDown(x, y);

        // A captura do mouse é o que permite arrastar para além deste monitor: os
        // eventos continuam chegando aqui mesmo com o ponteiro na tela vizinha.
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_draggingToolbar)
            return;

        var (x, y) = ToVirtual(e.GetPosition(Root));

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _session.PointerMove(x, y);
            return;
        }

        // Com o botão solto, o ponteiro anuncia o que um clique faria. Durante o
        // arrasto o cursor fica congelado de propósito: recalcular no meio do
        // gesto o faria piscar toda vez que o ponteiro saísse da alça.
        UpdateCursor(x, y);
    }

    /// <summary>
    /// A forma do ponteiro conta a ação disponível: seta para desenhar ou começar
    /// uma seleção nova, cruz de setas para mover, e setas duplas na orientação
    /// certa para redimensionar.
    /// </summary>
    private void UpdateCursor(int x, int y)
    {
        Cursor = _session.HitTest(x, y) switch
        {
            CaptureSession.PointerTarget.Move => Cursors.SizeAll,

            CaptureSession.PointerTarget.ResizeTopLeft
                or CaptureSession.PointerTarget.ResizeBottomRight => Cursors.SizeNWSE,

            CaptureSession.PointerTarget.ResizeTopRight
                or CaptureSession.PointerTarget.ResizeBottomLeft => Cursors.SizeNESW,

            CaptureSession.PointerTarget.ResizeTop
                or CaptureSession.PointerTarget.ResizeBottom => Cursors.SizeNS,

            CaptureSession.PointerTarget.ResizeLeft
                or CaptureSession.PointerTarget.ResizeRight => Cursors.SizeWE,

            // Desenhar e criar seleção nova usam a seta comum.
            _ => Cursors.Arrow
        };
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_draggingToolbar)
            return;

        ReleaseMouseCapture();
        _session.PointerUp();
    }

    /// <summary>
    /// Ctrl+(+) e Ctrl+(−) ajustam a espessura do traço — e, com ela, o tamanho do
    /// texto. Estes dois não são alugados: ao contrário do Ctrl+C e do Esc, perder
    /// o ajuste de espessura porque a janela perdeu foco é um aborrecimento, não
    /// uma ferramenta travada.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
            return;

        switch (e.Key)
        {
            case Key.OemPlus or Key.Add:
                _session.AdjustThickness(1);
                e.Handled = true;
                break;

            case Key.OemMinus or Key.Subtract:
                _session.AdjustThickness(-1);
                e.Handled = true;
                break;
        }
    }

    /// <summary>Ponto local em unidades lógicas → coordenada virtual em pixels físicos.</summary>
    private (int X, int Y) ToVirtual(Point local) =>
        (_monitor.Left + (int)Math.Round(local.X / _deviceToDip),
         _monitor.Top + (int)Math.Round(local.Y / _deviceToDip));

    /// <summary>Coordenada virtual em pixels físicos → ponto local em unidades lógicas.</summary>
    private Point ToLocal(double virtualX, double virtualY) =>
        new((virtualX - _monitor.Left) * _deviceToDip,
            (virtualY - _monitor.Top) * _deviceToDip);

    // ================================================================
    // BARRA DE FERRAMENTAS
    // ================================================================

    /// <summary>
    /// A barra se arrasta pela pega, e não pelo fundo: com o fundo arrastável,
    /// qualquer clique que errasse um botão moveria a barra sem querer.
    /// </summary>
    private void WireToolbarDrag()
    {
        Grip.MouseLeftButtonDown += (_, e) =>
        {
            _draggingToolbar = true;
            _gripOffset = e.GetPosition(Toolbar);
            Grip.CaptureMouse();
            e.Handled = true;
        };

        Grip.MouseMove += (_, e) =>
        {
            if (!_draggingToolbar) return;

            var pointer = e.GetPosition(Root);
            var (x, y) = ToVirtual(new Point(pointer.X - _gripOffset.X, pointer.Y - _gripOffset.Y));
            _session.MoveNavbar(new Point(x, y));

            e.Handled = true;
        };

        Grip.MouseLeftButtonUp += (_, e) =>
        {
            if (!_draggingToolbar) return;

            _draggingToolbar = false;
            Grip.ReleaseMouseCapture();
            e.Handled = true;
        };
    }

    private void BuildPalette()
    {
        foreach (var color in Palette)
        {
            var swatch = new Button
            {
                Width = 26,
                Height = 26,
                Margin = new Thickness(2),
                Background = new SolidColorBrush(color),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x3A, 0x43)),
                BorderThickness = new Thickness(1),
                Tag = color
            };

            swatch.Click += (s, _) =>
            {
                if (s is Button { Tag: Color picked })
                    _session.SetCurrentColor(picked);

                ColorPalette.IsOpen = false;
            };

            PaletteGrid.Children.Add(swatch);
        }
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        ColorPalette.PlacementTarget = ColorButton;
        ColorPalette.IsOpen = true;
    }

    // ================================================================
    // FERRAMENTA DE TEXTO
    // ================================================================

    private void WireTextInput()
    {
        TextInput.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;

            e.Handled = true;
            CommitTextInput();
        };

        TextInput.LostFocus += (_, _) => CommitTextInput();
    }

    private void OnTextRequested(Point virtualPosition)
    {
        // Só a janela do monitor onde o clique caiu abre a caixa.
        if (!_monitor.Contains((int)virtualPosition.X, (int)virtualPosition.Y))
            return;

        _textAnchor = virtualPosition;

        var local = ToLocal(virtualPosition.X, virtualPosition.Y);
        Canvas.SetLeft(TextInput, local.X);
        Canvas.SetTop(TextInput, local.Y);

        TextInput.FontSize = AnnotationRenderer.FontSize(_session.PenWidth) * _deviceToDip;
        TextInput.Foreground = new SolidColorBrush(_session.DrawColor);
        TextInput.Text = string.Empty;
        TextInput.Visibility = Visibility.Visible;

        _session.IsEditingText = true;
        TextInput.Focus();
    }

    private void CommitTextInput()
    {
        if (!_session.IsEditingText || _closingTextInput)
            return;

        var text = TextInput.Text;
        HideTextInput();

        _session.CommitText(_textAnchor, text);
    }

    private void HideTextInput()
    {
        if (!_session.IsEditingText)
            return;

        // O LostFocus dispara ao esconder a caixa; sem esta trava, o texto seria
        // confirmado duas vezes.
        _closingTextInput = true;

        TextInput.Visibility = Visibility.Collapsed;
        TextInput.Text = string.Empty;
        _session.IsEditingText = false;

        Focus();
        _closingTextInput = false;
    }

    // ================================================================
    // DESENHO
    // ================================================================

    private void CreateHandles()
    {
        for (var i = 0; i < _handles.Length; i++)
        {
            var handle = new Rectangle
            {
                Fill = Brushes.White,
                Stroke = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)),
                StrokeThickness = 1,
                Visibility = Visibility.Collapsed
            };

            _handles[i] = handle;
            HandleLayer.Children.Add(handle);
        }
    }

    private void Refresh()
    {
        if (!IsLoaded)
            return;

        var selection = _session.Selection;
        var visible = _session.HasSelection;

        DrawMask(selection, visible);
        DrawSelection(selection, visible);
        DrawHandles(selection, visible);
        PlaceToolbar(selection, visible);

        _annotations.Refresh();
        ColorSwatch.Background = new SolidColorBrush(_session.CurrentColor);
        ToolbarPanel.Orientation = _session.NavbarVertical ? Orientation.Vertical : Orientation.Horizontal;
        UpdateSeparators();

        Hint.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;

        if (!visible)
        {
            Hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(Hint, (_dipWidth - Hint.DesiredSize.Width) / 2);
            Canvas.SetTop(Hint, _dipHeight - Hint.DesiredSize.Height - 48);
        }
    }

    /// <summary>Os separadores viram linhas horizontais quando a barra fica vertical.</summary>
    private void UpdateSeparators()
    {
        var vertical = _session.NavbarVertical;

        foreach (var separator in new[] { Separator1, Separator2 })
        {
            separator.Width = vertical ? 22 : 1;
            separator.Height = vertical ? 1 : 22;
            separator.Margin = vertical ? new Thickness(0, 4, 0, 4) : new Thickness(4, 0, 4, 0);
        }
    }

    private void DrawMask((int Left, int Top, int Width, int Height) selection, bool visible)
    {
        var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
        group.Children.Add(new RectangleGeometry(new Rect(0, 0, _dipWidth, _dipHeight)));

        if (visible)
            group.Children.Add(new RectangleGeometry(ToLocalRect(selection)));

        MaskLayer.Data = group;
    }

    private void DrawSelection((int Left, int Top, int Width, int Height) selection, bool visible)
    {
        if (!visible)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            return;
        }

        var rect = ToLocalRect(selection);

        SelectionBorder.Visibility = Visibility.Visible;
        SelectionBorder.Width = rect.Width;
        SelectionBorder.Height = rect.Height;
        Canvas.SetLeft(SelectionBorder, rect.X);
        Canvas.SetTop(SelectionBorder, rect.Y);
    }

    private void DrawHandles((int Left, int Top, int Width, int Height) selection, bool visible)
    {
        // Com uma ferramenta de desenho ativa as alças somem: o mouse ali dentro
        // desenha, e mostrar alças que não respondem seria mentira visual.
        if (!visible || _session.ActiveTool is not null)
        {
            foreach (var handle in _handles)
                handle.Visibility = Visibility.Collapsed;
            return;
        }

        var size = CaptureSession.HandleSize * _deviceToDip;
        var rect = ToLocalRect(selection);

        var midX = rect.X + rect.Width / 2;
        var midY = rect.Y + rect.Height / 2;
        var right = rect.X + rect.Width;
        var bottom = rect.Y + rect.Height;

        // Na mesma ordem em que as alças foram criadas: canto superior esquerdo,
        // topo, superior direito, direita, inferior direito, base, inferior
        // esquerdo, esquerda.
        var positions = new[]
        {
            new Point(rect.X, rect.Y), new Point(midX, rect.Y), new Point(right, rect.Y),
            new Point(right, midY),    new Point(right, bottom), new Point(midX, bottom),
            new Point(rect.X, bottom), new Point(rect.X, midY)
        };

        for (var i = 0; i < _handles.Length; i++)
        {
            var handle = _handles[i];
            handle.Visibility = Visibility.Visible;
            handle.Width = size;
            handle.Height = size;
            Canvas.SetLeft(handle, positions[i].X - size / 2);
            Canvas.SetTop(handle, positions[i].Y - size / 2);
        }
    }

    /// <summary>
    /// A barra aparece só na janela do monitor onde ela deve ficar — senão, num
    /// setup com duas telas, o usuário veria duas barras.
    /// </summary>
    private void PlaceToolbar((int Left, int Top, int Width, int Height) selection, bool visible)
    {
        if (!visible)
        {
            Toolbar.Visibility = Visibility.Collapsed;
            return;
        }

        Toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = Toolbar.DesiredSize;
        var margin = 8 * _deviceToDip;

        double left, top;

        if (_session.NavbarPosition is { } custom)
        {
            // Posição escolhida pelo usuário: só é desenhada pela janela do monitor
            // que a contém.
            if (!_monitor.Contains((int)custom.X, (int)custom.Y))
            {
                Toolbar.Visibility = Visibility.Collapsed;
                return;
            }

            var local = ToLocal(custom.X, custom.Y);
            left = local.X;
            top = local.Y;
        }
        else
        {
            var anchorX = Math.Min(selection.Left + selection.Width, _monitor.Right - 1);
            var anchorY = Math.Min(selection.Top + selection.Height, _monitor.Bottom - 1);

            if (!_monitor.Contains(anchorX, anchorY))
            {
                Toolbar.Visibility = Visibility.Collapsed;
                return;
            }

            var rect = ToLocalRect(selection);
            left = rect.X + rect.Width - size.Width;
            top = rect.Y + rect.Height + margin;

            if (top + size.Height > _dipHeight)
                top = Math.Max(0, rect.Y + rect.Height - size.Height - margin);
        }

        // A barra nunca sai da tela — o LiteShot antigo tinha essa mesma garantia
        // por matemática absoluta.
        left = Math.Clamp(left, 0, Math.Max(0, _dipWidth - size.Width));
        top = Math.Clamp(top, 0, Math.Max(0, _dipHeight - size.Height));

        Toolbar.Visibility = Visibility.Visible;
        Canvas.SetLeft(Toolbar, left);
        Canvas.SetTop(Toolbar, top);
    }

    private Rect ToLocalRect((int Left, int Top, int Width, int Height) selection)
    {
        var origin = ToLocal(selection.Left, selection.Top);
        return new Rect(origin.X, origin.Y, selection.Width * _deviceToDip, selection.Height * _deviceToDip);
    }
}