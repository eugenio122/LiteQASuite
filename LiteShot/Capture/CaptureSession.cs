using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiteQASuite.Core.Localization;
using LiteQASuite.Core.Mvvm;
using LiteShot.Platform;

namespace LiteShot.Capture;

/// <summary>
/// O estado compartilhado de uma captura em andamento: a foto congelada da tela, a
/// área selecionada, as anotações e a ferramenta ativa.
///
/// <b>Todas as coordenadas aqui são pixels físicos da área de trabalho virtual</b>
/// — o mesmo sistema em que os monitores são enumerados. Um monitor à esquerda do
/// primário tem coordenada negativa, e isso é normal.
///
/// Esse detalhe é o que permite a seleção atravessar monitores de escalas
/// diferentes: existe uma seleção só, num sistema só, e cada janela de overlay
/// converte para as suas coordenadas locais na hora de desenhar.
///
/// A sessão não sabe copiar, salvar nem persistir: ela pede, através de eventos, e
/// o <see cref="CaptureCoordinator"/> executa.
/// </summary>
public sealed class CaptureSession : ViewModelBase
{
    /// <summary>O que o usuário está fazendo com o mouse neste momento.</summary>
    private enum DragAction
    {
        None,
        Create,
        Move,
        ResizeTopLeft, ResizeTop, ResizeTopRight, ResizeRight,
        ResizeBottomRight, ResizeBottom, ResizeBottomLeft, ResizeLeft,
        Annotate
    }

    /// <summary>
    /// O que existe sob o ponteiro — ou melhor, o que aconteceria se o usuário
    /// clicasse ali. A janela usa isto para escolher o cursor, de modo que a forma
    /// do ponteiro anuncie a ação em vez de ser decorativa.
    /// </summary>
    public enum PointerTarget
    {
        /// <summary>Nada sob o ponteiro: um clique começa uma seleção nova.</summary>
        None,

        /// <summary>Dentro da seleção, sem ferramenta ativa: um clique a move.</summary>
        Move,

        /// <summary>Sobre uma alça: um clique redimensiona naquela direção.</summary>
        ResizeTopLeft, ResizeTop, ResizeTopRight, ResizeRight,
        ResizeBottomRight, ResizeBottom, ResizeBottomLeft, ResizeLeft,

        /// <summary>Ferramenta ativa e ponteiro dentro da seleção: um clique desenha.</summary>
        Draw
    }

    /// <summary>Tolerância de agarre das alças, em pixels físicos.</summary>
    public const int HandleSize = 10;

    /// <summary>Abaixo disto, um arrasto é considerado clique acidental e a seleção é descartada.</summary>
    private const int MinimumSelection = 12;

    private const double MinimumThickness = 1;
    private const double MaximumThickness = 24;

    private readonly IModuleStrings _strings;

    /// <summary>Uma camada de anotações por perfil, indexada por <c>Id - 1</c>.</summary>
    private readonly AnnotationLayer[] _layers = new AnnotationLayer[2];

    private DragAction _action = DragAction.None;
    private int _dragOriginX, _dragOriginY;
    private (int Left, int Top, int Width, int Height) _selectionAtDragStart;

    private int _left, _top, _width, _height;

    private AnnotationKind? _activeTool;
    private Color _drawColor = Colors.Red;
    private Color _highlightColor = Colors.Yellow;
    private double _penWidth = 3;
    private int _activeProfileId = 1;
    private bool _navbarVertical;

    public CaptureSession(
        BitmapSource screenshot,
        (int Left, int Top, int Width, int Height) bounds,
        IReadOnlyList<MonitorInfo> monitors,
        IModuleStrings strings)
    {
        Screenshot = screenshot;
        Bounds = bounds;
        Monitors = monitors;
        _strings = strings;

        // Uma camada por perfil: P1 e P2 são espaços de trabalho separados, então
        // o que foi desenhado num não aparece no outro. As duas vivem só enquanto a
        // sessão existe — copiar, salvar ou cancelar descarta as duas juntas.
        for (var i = 0; i < _layers.Length; i++)
        {
            _layers[i] = new AnnotationLayer();
            _layers[i].Changed += () => Changed?.Invoke();
        }

        CopyCommand = new RelayCommand(_ => RequestCopy(), _ => HasSelection);
        SaveCommand = new RelayCommand(_ => RequestSave(), _ => HasSelection);
        CancelCommand = new RelayCommand(_ => RequestCancel());
        UndoCommand = new RelayCommand(_ => Annotations.Undo(), _ => Annotations.CanUndo);
        RedoCommand = new RelayCommand(_ => Annotations.Redo(), _ => Annotations.CanRedo);
        SelectToolCommand = new RelayCommand(ToggleTool);
        SelectProfileCommand = new RelayCommand(RequestProfile);
    }

    // ================================================================
    // O QUE FOI CAPTURADO
    // ================================================================

    /// <summary>
    /// Identificador desta captura, gerado no disparo. Viaja nos três eventos do
    /// pipeline e é o que amarra "começou" a "terminou": quem cria um passo
    /// pendente no evento de início sabe, pelo id, qual confirmar ou descartar
    /// depois.
    /// </summary>
    public string StepId { get; } = Guid.NewGuid().ToString();

    /// <summary>A foto congelada de toda a área de trabalho, tirada antes do overlay aparecer.</summary>
    public BitmapSource Screenshot { get; }

    /// <summary>A união física dos monitores — origem e tamanho da <see cref="Screenshot"/>.</summary>
    public (int Left, int Top, int Width, int Height) Bounds { get; }

    /// <summary>Os monitores, para o "selecionar esta tela" do Ctrl+A.</summary>
    public IReadOnlyList<MonitorInfo> Monitors { get; }

    /// <summary>
    /// As anotações do <b>perfil ativo</b>. Cada perfil tem a sua camada: alternar
    /// entre P1 e P2 troca também o que está desenhado, porque são espaços de
    /// trabalho independentes — o traço feito no P1 não deve reaparecer quando a
    /// seleção do P2 passar por cima daquela região.
    /// </summary>
    public AnnotationLayer Annotations => _layers[Math.Clamp(_activeProfileId, 1, 2) - 1];

    // ================================================================
    // EVENTOS
    // ================================================================

    /// <summary>Disparado sempre que algo visível muda; as janelas se redesenham.</summary>
    public event Action? Changed;

    /// <summary>O usuário confirmou: área de transferência e, depois, o pipeline.</summary>
    public event Action? CopyRequested;

    /// <summary>O usuário quer gravar um arquivo local.</summary>
    public event Action? SaveRequested;

    /// <summary>O usuário desistiu.</summary>
    public event Action? CancelRequested;

    /// <summary>O usuário clicou com a ferramenta de texto; a janela abre a caixa de digitação.</summary>
    public event Action<Point>? TextRequested;

    /// <summary>O usuário trocou de perfil na barra; o coordenador aplica e persiste.</summary>
    public event Action<int>? ProfileChangeRequested;

    // ================================================================
    // COMANDOS
    // ================================================================

    public ICommand CopyCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand SelectToolCommand { get; }
    public ICommand SelectProfileCommand { get; }

    // ================================================================
    // SELEÇÃO
    // ================================================================

    /// <summary>Há uma área escolhida com tamanho útil.</summary>
    public bool HasSelection => _width >= MinimumSelection && _height >= MinimumSelection;

    /// <summary>A área escolhida, em coordenadas virtuais.</summary>
    public (int Left, int Top, int Width, int Height) Selection => (_left, _top, _width, _height);

    /// <summary>
    /// Restaura uma seleção guardada num perfil. Ignora se ela não intersecta mais
    /// a área de trabalho atual — o usuário pode ter desconectado o monitor onde
    /// aquela área ficava.
    /// </summary>
    public void RestoreSelection(int left, int top, int width, int height)
    {
        if (width < MinimumSelection || height < MinimumSelection)
            return;

        var intersects = left < Bounds.Left + Bounds.Width
                      && left + width > Bounds.Left
                      && top < Bounds.Top + Bounds.Height
                      && top + height > Bounds.Top;

        if (!intersects)
            return;

        SetSelection(left, top, width, height);
    }

    /// <summary>Seleciona o monitor onde o mouse está. É o Ctrl+A.</summary>
    public void SelectMonitorUnderCursor()
    {
        var monitor = Platform.Monitors.UnderCursor(Monitors);
        SetSelection(monitor.Left, monitor.Top, monitor.Width, monitor.Height);
    }

    // ================================================================
    // FERRAMENTAS
    // ================================================================

    /// <summary>
    /// A ferramenta ativa, ou <c>null</c> quando o mouse está ajustando a seleção.
    /// Clicar de novo no mesmo botão desliga — como no LiteShot antigo.
    /// </summary>
    public AnnotationKind? ActiveTool
    {
        get => _activeTool;
        private set
        {
            if (_activeTool == value) return;
            _activeTool = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentColor));

            // Uma propriedade por ferramenta, para os botões da barra saberem qual
            // está apertado sem precisar de conversor no XAML.
            OnPropertyChanged(nameof(IsPenActive));
            OnPropertyChanged(nameof(IsLineActive));
            OnPropertyChanged(nameof(IsArrowActive));
            OnPropertyChanged(nameof(IsShapeActive));
            OnPropertyChanged(nameof(IsHighlighterActive));
            OnPropertyChanged(nameof(IsTextActive));

            Changed?.Invoke();
        }
    }

    public bool IsPenActive => _activeTool == AnnotationKind.Pen;
    public bool IsLineActive => _activeTool == AnnotationKind.Line;
    public bool IsArrowActive => _activeTool == AnnotationKind.Arrow;
    public bool IsShapeActive => _activeTool == AnnotationKind.Shape;
    public bool IsHighlighterActive => _activeTool == AnnotationKind.Highlighter;
    public bool IsTextActive => _activeTool == AnnotationKind.Text;

    /// <summary>Cor das ferramentas de traço.</summary>
    public Color DrawColor
    {
        get => _drawColor;
        set
        {
            if (_drawColor == value) return;
            _drawColor = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentColor));
        }
    }

    /// <summary>Cor do marcador, separada porque um amarelo de marca-texto não serve de caneta.</summary>
    public Color HighlightColor
    {
        get => _highlightColor;
        set
        {
            if (_highlightColor == value) return;
            _highlightColor = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurrentColor));
        }
    }

    /// <summary>
    /// A cor que o botão de cor está editando: a do marcador quando ele é a
    /// ferramenta ativa, a de traço nos demais casos.
    /// </summary>
    public Color CurrentColor => ActiveTool == AnnotationKind.Highlighter ? HighlightColor : DrawColor;

    /// <summary>Aplica uma cor à ferramenta ativa.</summary>
    public void SetCurrentColor(Color color)
    {
        if (ActiveTool == AnnotationKind.Highlighter)
            HighlightColor = color;
        else
            DrawColor = color;

        Changed?.Invoke();
    }

    /// <summary>Espessura do traço, também usada para derivar o tamanho do texto.</summary>
    public double PenWidth
    {
        get => _penWidth;
        set
        {
            var clamped = Math.Clamp(value, MinimumThickness, MaximumThickness);
            if (Math.Abs(_penWidth - clamped) < 0.01) return;

            _penWidth = clamped;
            OnPropertyChanged();
            Changed?.Invoke();
        }
    }

    /// <summary>Ctrl+(+) e Ctrl+(−).</summary>
    public void AdjustThickness(double delta) => PenWidth += delta;

    private void ToggleTool(object? parameter)
    {
        if (parameter is not string name || !Enum.TryParse<AnnotationKind>(name, out var kind))
            return;

        ActiveTool = ActiveTool == kind ? null : kind;
    }

    /// <summary>Para o realce do botão na barra.</summary>
    public bool IsToolActive(AnnotationKind kind) => ActiveTool == kind;

    // ================================================================
    // PERFIL E BARRA
    // ================================================================

    /// <summary>Qual perfil está em uso. Os botões P1/P2 da barra alternam isto.</summary>
    public int ActiveProfileId
    {
        get => _activeProfileId;
        set
        {
            if (_activeProfileId == value) return;

            // Um traço em andamento pertence ao perfil onde começou; deixá-lo
            // pendurado o faria terminar no perfil errado.
            Annotations.AbortInProgress();

            _activeProfileId = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsProfile1Active));
            OnPropertyChanged(nameof(IsProfile2Active));

            // A camada mudou junto — quem desenha lê daqui.
            OnPropertyChanged(nameof(Annotations));

            Changed?.Invoke();
        }
    }

    public bool IsProfile1Active => _activeProfileId == 1;
    public bool IsProfile2Active => _activeProfileId == 2;

    /// <summary>Orientação da barra flutuante neste perfil.</summary>
    public bool NavbarVertical
    {
        get => _navbarVertical;
        set
        {
            if (_navbarVertical == value) return;
            _navbarVertical = value;
            OnPropertyChanged();
            Changed?.Invoke();
        }
    }

    /// <summary>
    /// Posição da barra em coordenadas virtuais, quando o usuário a arrastou. Nula
    /// significa "calcule a partir da seleção".
    /// </summary>
    public Point? NavbarPosition { get; private set; }

    /// <summary>Registra a barra numa posição escolhida pelo usuário.</summary>
    public void MoveNavbar(Point virtualPosition)
    {
        NavbarPosition = virtualPosition;
        Changed?.Invoke();
    }

    /// <summary>Restaura a posição guardada num perfil.</summary>
    public void RestoreNavbar(Point virtualPosition) => NavbarPosition = virtualPosition;

    /// <summary>Volta ao posicionamento automático, junto da seleção.</summary>
    public void ResetNavbar() => NavbarPosition = null;

    private void RequestProfile(object? parameter)
    {
        if (parameter is int id)
            ProfileChangeRequested?.Invoke(id);
        else if (int.TryParse(parameter?.ToString(), out var parsed))
            ProfileChangeRequested?.Invoke(parsed);
    }

    // ================================================================
    // TEXTO
    // ================================================================

    /// <summary>
    /// A caixa de digitação está aberta. Enquanto estiver, o Esc pertence a ela —
    /// cancelar o texto não pode cancelar a captura inteira.
    /// </summary>
    public bool IsEditingText { get; set; }

    /// <summary>O Esc chegou enquanto a caixa de texto estava aberta.</summary>
    public event Action? TextEditCancelled;

    /// <summary>
    /// O Esc, venha do botão Fechar ou do atalho alugado. Só cancela a captura se
    /// não houver texto sendo digitado — mesma regra do LiteShot antigo, onde o
    /// tratador do Esc checava se a caixa estava visível antes de desistir de tudo.
    /// </summary>
    public void RequestCancel()
    {
        if (IsEditingText)
        {
            TextEditCancelled?.Invoke();
            return;
        }

        CancelRequested?.Invoke();
    }

    /// <summary>Cria a anotação de texto depois que o usuário terminou de digitar.</summary>
    public void CommitText(Point position, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Annotations.Add(new Annotation
        {
            Kind = AnnotationKind.Text,
            Color = DrawColor,
            Thickness = PenWidth,
            Start = position,
            End = position,
            Text = text
        });
    }

    // ================================================================
    // MÁQUINA DE ARRASTO
    // ================================================================

    /// <summary>
    /// O que aconteceria se o usuário clicasse neste ponto. Só consulta — não muda
    /// nada. A janela chama a cada movimento do mouse para escolher o cursor.
    ///
    /// A ordem das perguntas é a mesma do <see cref="PointerDown"/>, de propósito:
    /// se as duas divergissem, o cursor prometeria uma coisa e o clique faria
    /// outra.
    /// </summary>
    public PointerTarget HitTest(int x, int y)
    {
        if (ActiveTool is not null)
            return HasSelection && IsInsideSelection(x, y) ? PointerTarget.Draw : PointerTarget.None;

        if (!HasSelection)
            return PointerTarget.None;

        var handle = HitTestHandle(x, y);

        if (handle != DragAction.None)
        {
            return handle switch
            {
                DragAction.ResizeTopLeft => PointerTarget.ResizeTopLeft,
                DragAction.ResizeTop => PointerTarget.ResizeTop,
                DragAction.ResizeTopRight => PointerTarget.ResizeTopRight,
                DragAction.ResizeRight => PointerTarget.ResizeRight,
                DragAction.ResizeBottomRight => PointerTarget.ResizeBottomRight,
                DragAction.ResizeBottom => PointerTarget.ResizeBottom,
                DragAction.ResizeBottomLeft => PointerTarget.ResizeBottomLeft,
                DragAction.ResizeLeft => PointerTarget.ResizeLeft,
                _ => PointerTarget.None
            };
        }

        return IsInsideSelection(x, y) ? PointerTarget.Move : PointerTarget.None;
    }

    /// <summary>Botão pressionado, em coordenadas virtuais.</summary>
    public void PointerDown(int x, int y)
    {
        _dragOriginX = x;
        _dragOriginY = y;
        _selectionAtDragStart = Selection;

        // Com uma ferramenta ativa, o mouse desenha em vez de mexer na seleção —
        // mas só dentro da área escolhida, que é o que vai virar imagem.
        if (ActiveTool is { } tool && HasSelection && IsInsideSelection(x, y))
        {
            if (tool == AnnotationKind.Text)
            {
                TextRequested?.Invoke(new Point(x, y));
                return;
            }

            BeginAnnotation(tool, x, y);
            _action = DragAction.Annotate;
            return;
        }

        if (HasSelection)
        {
            var handle = HitTestHandle(x, y);
            if (handle != DragAction.None)
            {
                _action = handle;
                return;
            }

            if (IsInsideSelection(x, y))
            {
                _action = DragAction.Move;
                return;
            }
        }

        _action = DragAction.Create;
        SetSelection(x, y, 0, 0);
    }

    /// <summary>Mouse movido, em coordenadas virtuais.</summary>
    public void PointerMove(int x, int y)
    {
        if (_action == DragAction.None)
            return;

        if (_action == DragAction.Annotate)
        {
            UpdateAnnotation(x, y);
            return;
        }

        if (_action == DragAction.Create)
        {
            SetSelection(
                Math.Min(_dragOriginX, x),
                Math.Min(_dragOriginY, y),
                Math.Abs(x - _dragOriginX),
                Math.Abs(y - _dragOriginY));
            return;
        }

        if (_action == DragAction.Move)
        {
            SetSelection(
                _selectionAtDragStart.Left + (x - _dragOriginX),
                _selectionAtDragStart.Top + (y - _dragOriginY),
                _selectionAtDragStart.Width,
                _selectionAtDragStart.Height);
            return;
        }

        ResizeTo(x, y);
    }

    /// <summary>Botão solto. Descarta seleções pequenas demais para serem intencionais.</summary>
    public void PointerUp()
    {
        if (_action == DragAction.Annotate)
        {
            Annotations.Commit();
            _action = DragAction.None;
            return;
        }

        if (_action == DragAction.Create && !HasSelection)
            SetSelection(0, 0, 0, 0);

        _action = DragAction.None;
        Changed?.Invoke();
    }

    private void BeginAnnotation(AnnotationKind kind, int x, int y)
    {
        var origin = new Point(x, y);
        var freehand = kind is AnnotationKind.Pen or AnnotationKind.Highlighter;

        Annotations.Begin(new Annotation
        {
            Kind = kind,
            Color = kind == AnnotationKind.Highlighter ? HighlightColor : DrawColor,
            Thickness = PenWidth,
            Start = origin,
            End = origin,
            Path = freehand ? new List<Point> { origin } : null
        });
    }

    private void UpdateAnnotation(int x, int y)
    {
        if (Annotations.InProgress is not { } annotation)
            return;

        var point = new Point(x, y);
        annotation.End = point;
        annotation.Path?.Add(point);

        Annotations.UpdateInProgress();
    }

    private void ResizeTo(int x, int y)
    {
        var start = _selectionAtDragStart;

        var left = start.Left;
        var top = start.Top;
        var right = start.Left + start.Width;
        var bottom = start.Top + start.Height;

        switch (_action)
        {
            case DragAction.ResizeTopLeft: left = x; top = y; break;
            case DragAction.ResizeTop: top = y; break;
            case DragAction.ResizeTopRight: right = x; top = y; break;
            case DragAction.ResizeRight: right = x; break;
            case DragAction.ResizeBottomRight: right = x; bottom = y; break;
            case DragAction.ResizeBottom: bottom = y; break;
            case DragAction.ResizeBottomLeft: left = x; bottom = y; break;
            case DragAction.ResizeLeft: left = x; break;
        }

        // Arrastar uma alça para além da borda oposta inverte o retângulo em vez de
        // travar — é o que o usuário espera de uma ferramenta de recorte.
        SetSelection(
            Math.Min(left, right),
            Math.Min(top, bottom),
            Math.Abs(right - left),
            Math.Abs(bottom - top));
    }

    private DragAction HitTestHandle(int x, int y)
    {
        var left = _left;
        var top = _top;
        var right = _left + _width;
        var bottom = _top + _height;
        var midX = _left + _width / 2;
        var midY = _top + _height / 2;

        if (Near(x, y, left, top)) return DragAction.ResizeTopLeft;
        if (Near(x, y, midX, top)) return DragAction.ResizeTop;
        if (Near(x, y, right, top)) return DragAction.ResizeTopRight;
        if (Near(x, y, right, midY)) return DragAction.ResizeRight;
        if (Near(x, y, right, bottom)) return DragAction.ResizeBottomRight;
        if (Near(x, y, midX, bottom)) return DragAction.ResizeBottom;
        if (Near(x, y, left, bottom)) return DragAction.ResizeBottomLeft;
        if (Near(x, y, left, midY)) return DragAction.ResizeLeft;

        return DragAction.None;
    }

    private static bool Near(int x, int y, int targetX, int targetY) =>
        Math.Abs(x - targetX) <= HandleSize && Math.Abs(y - targetY) <= HandleSize;

    private bool IsInsideSelection(int x, int y) =>
        x > _left && x < _left + _width && y > _top && y < _top + _height;

    private void SetSelection(int left, int top, int width, int height)
    {
        _left = left;
        _top = top;
        _width = Math.Max(0, width);
        _height = Math.Max(0, height);

        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(HasSelection));
        Changed?.Invoke();
    }

    private void RequestCopy()
    {
        if (HasSelection) CopyRequested?.Invoke();
    }

    private void RequestSave()
    {
        if (HasSelection) SaveRequested?.Invoke();
    }

    // ================================================================
    // RÓTULOS
    // ================================================================

    // O overlay é a sua própria tela, então os rótulos dele moram aqui. Não há
    // troca de idioma no meio de uma captura: a sessão dura segundos.
    public string LabelPen => _strings.GetString("Toolbar.Pen");
    public string LabelLine => _strings.GetString("Toolbar.Line");
    public string LabelArrow => _strings.GetString("Toolbar.Arrow");
    public string LabelShape => _strings.GetString("Toolbar.Shape");
    public string LabelHighlighter => _strings.GetString("Toolbar.Highlighter");
    public string LabelText => _strings.GetString("Toolbar.Text");
    public string LabelColor => _strings.GetString("Toolbar.Color");
    public string LabelUndo => _strings.GetString("Toolbar.Undo");
    public string LabelRedo => _strings.GetString("Toolbar.Redo");
    public string LabelCopy => _strings.GetString("Toolbar.Copy");
    public string LabelSave => _strings.GetString("Toolbar.Save");
    public string LabelClose => _strings.GetString("Toolbar.Close");
    public string LabelProfile1 => _strings.GetString("Toolbar.Profile1");
    public string LabelProfile2 => _strings.GetString("Toolbar.Profile2");
    public string LabelHint => _strings.GetString("Overlay.Hint");
}