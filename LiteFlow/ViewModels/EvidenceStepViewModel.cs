using System.Windows.Media.Imaging;
using LiteFlow.Models;
using LiteQASuite.Core.Mvvm;

namespace LiteFlow.ViewModels;

/// <summary>
/// Um passo visto pela tela: a miniatura e o número que aparecem no histórico.
///
/// Existe para o modelo não voltar a carregar UI dentro dele — no 1.x o
/// <c>EvidenceItem</c> tinha um <c>PictureBox</c> como propriedade, e por isso
/// precisava ser um tipo diferente do que ia para o disco. Aqui o
/// <see cref="Step"/> é o dado e este objeto é a apresentação.
/// </summary>
public sealed class EvidenceStepViewModel : ViewModelBase
{
    private BitmapSource? _thumbnail;
    private string _displayIndex = "";

    public EvidenceStepViewModel(EvidenceStep step, BitmapSource? thumbnail)
    {
        Step = step;
        _thumbnail = thumbnail;
    }

    /// <summary>O passo propriamente dito.</summary>
    public EvidenceStep Step { get; }

    /// <summary>Miniatura já decodificada em tamanho pequeno e congelada.</summary>
    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    /// <summary>
    /// O número mostrado no canto da miniatura. É <c>string</c> porque uma
    /// evidência marcada como "só evidência" mostra <c>—</c> em vez de número: ela
    /// ilustra, não é um passo executado, e por isso não entra na contagem do
    /// relatório.
    /// </summary>
    public string DisplayIndex
    {
        get => _displayIndex;
        set => SetProperty(ref _displayIndex, value);
    }

    /// <summary>Atalho para o binding do selo laranja de "só evidência".</summary>
    public bool IsEvidenceOnly => Step.IsEvidenceOnly;

    /// <summary>Chamado quando o selo muda, para a lista repintar.</summary>
    public void RefreshEvidenceOnly() => OnPropertyChanged(nameof(IsEvidenceOnly));
}