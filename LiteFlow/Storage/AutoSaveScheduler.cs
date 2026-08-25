using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LiteFlow.Storage;

/// <summary>
/// Decide <b>quando</b> salvar — não sabe salvar. Junta o "está sujo", o atraso e a
/// garantia de que dois salvamentos não se atropelam num lugar só, para essa
/// contabilidade não ficar espalhada pelo ViewModel como estava no 1.x.
///
/// <b>O atraso é de três segundos, e não de um e meio como no 1.x.</b> Com as
/// imagens embutidas no <c>.lflow</c>, cada salvamento reescreve o arquivo inteiro:
/// disparar a cada pausa curta de digitação faria um cenário de trezentos megas ser
/// regravado dezenas de vezes enquanto o usuário escreve uma nota. Três segundos é
/// o tempo em que a pessoa parou de digitar de verdade.
///
/// Também só dispara quando algo mudou. O 1.x reiniciava o timer em cada
/// <c>TextChanged</c> e salvava mesmo sem alteração efetiva.
/// </summary>
public sealed class AutoSaveScheduler : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Func<Task> _save;

    private bool _isDirty;
    private bool _isSaving;
    private bool _isDisposed;

    /// <param name="save">O salvamento propriamente dito. Chamado na thread de interface.</param>
    /// <param name="delay">Quanto tempo de silêncio antes de gravar.</param>
    public AutoSaveScheduler(Func<Task> save, TimeSpan delay)
    {
        _save = save ?? throw new ArgumentNullException(nameof(save));
        _timer = new DispatcherTimer { Interval = delay };
        _timer.Tick += OnTick;
    }

    /// <summary><c>true</c> quando há alteração ainda não gravada.</summary>
    public bool IsDirty => _isDirty;

    /// <summary>Algo mudou: reinicia a contagem.</summary>
    public void MarkDirty()
    {
        if (_isDisposed) return;

        _isDirty = true;
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>
    /// Grava agora, se houver o que gravar. Se um salvamento estiver em curso,
    /// reagenda em vez de empilhar — dois escritores no mesmo arquivo é o caminho
    /// mais curto para um <c>.lflow</c> corrompido.
    /// </summary>
    public async Task FlushAsync()
    {
        if (_isDisposed || !_isDirty) return;

        if (_isSaving)
        {
            _timer.Stop();
            _timer.Start();
            return;
        }

        _isSaving = true;
        _isDirty = false;

        try
        {
            await _save().ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Quem sabe o que fazer com a falha é quem passou o delegate; aqui ela
            // só não pode derrubar o timer, ou o cenário deixaria de salvar
            // silenciosamente pelo resto da sessão.
            _isDirty = true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    /// <summary>Esquece a alteração pendente — usado ao trocar de cenário.</summary>
    public void Reset()
    {
        _timer.Stop();
        _isDirty = false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
    }

    private async void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        await FlushAsync();
    }
}