namespace LiteShot.Capture;

/// <summary>
/// As anotações da captura atual, com desfazer e refazer.
///
/// Como as anotações são objetos e não pixels, o histórico é a própria lista: um
/// desfazer tira o último item, um refazer o devolve. Comparado ao
/// <c>Stack&lt;Bitmap&gt;</c> do código antigo — que clonava a tela inteira a cada
/// traço — o custo em memória deixou de existir, e com ele a necessidade de limitar
/// a profundidade do histórico.
/// </summary>
public sealed class AnnotationLayer
{
    private readonly List<Annotation> _items = new();
    private readonly Stack<Annotation> _undone = new();

    /// <summary>Disparado a cada mudança; o overlay se redesenha.</summary>
    public event Action? Changed;

    /// <summary>As anotações na ordem em que foram criadas — que é a ordem de desenho.</summary>
    public IReadOnlyList<Annotation> Items => _items;

    public bool CanUndo => _items.Count > 0;

    public bool CanRedo => _undone.Count > 0;

    /// <summary>
    /// A anotação sendo desenhada neste instante. Fica fora da lista até o usuário
    /// soltar o botão, para que um traço em andamento não possa ser desfeito pela
    /// metade nem entre no histórico se for abandonado.
    /// </summary>
    public Annotation? InProgress { get; private set; }

    /// <summary>Começa uma anotação. Ela ainda não conta para o histórico.</summary>
    public void Begin(Annotation annotation)
    {
        InProgress = annotation;
        Changed?.Invoke();
    }

    /// <summary>Avisa que a anotação em andamento mudou de forma.</summary>
    public void UpdateInProgress() => Changed?.Invoke();

    /// <summary>
    /// Confirma a anotação em andamento. Uma nova ação limpa o refazer — é o
    /// comportamento que todo editor tem: você não pode refazer um caminho do qual
    /// já se desviou.
    /// </summary>
    public void Commit()
    {
        if (InProgress is null)
            return;

        _items.Add(InProgress);
        InProgress = null;
        _undone.Clear();

        Changed?.Invoke();
    }

    /// <summary>Descarta a anotação em andamento sem registrá-la.</summary>
    public void AbortInProgress()
    {
        if (InProgress is null)
            return;

        InProgress = null;
        Changed?.Invoke();
    }

    /// <summary>Adiciona uma anotação pronta — é o caso do texto, que nasce completo.</summary>
    public void Add(Annotation annotation)
    {
        _items.Add(annotation);
        _undone.Clear();

        Changed?.Invoke();
    }

    public void Undo()
    {
        if (_items.Count == 0)
            return;

        var last = _items[^1];
        _items.RemoveAt(_items.Count - 1);
        _undone.Push(last);

        Changed?.Invoke();
    }

    public void Redo()
    {
        if (_undone.Count == 0)
            return;

        _items.Add(_undone.Pop());
        Changed?.Invoke();
    }
}   