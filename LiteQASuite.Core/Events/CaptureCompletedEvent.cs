namespace LiteQASuite.Core.Events;

/// <summary>
/// A captura foi confirmada pelo usuário — o commit do passo no cenário. Publicado
/// pelo LiteShot depois de a lente sair da frente.
///
/// Só o "copiar" chega aqui. Gravar um arquivo local é exportação, não participação
/// no cenário, e publica <see cref="CaptureCanceledEvent"/> com motivo
/// <see cref="CaptureCancelReason.SavedLocally"/>.
///
/// <b>Publicação assíncrona.</b> Ao contrário do <see cref="CaptureStartedEvent"/>,
/// aqui não há corrida contra o tempo: o overlay já fechou e o usuário já voltou ao
/// que estava fazendo. A codificação das duas imagens acontece em segundo plano, e
/// o evento sai de uma thread que não é a de interface — quem assina e toca a tela
/// é responsável pelo próprio <c>Dispatcher</c>.
/// </summary>
/// <param name="StepId">
/// O mesmo id que veio no <see cref="CaptureStartedEvent"/>. É o que permite ao
/// assinante confirmar o passo pendente que ele criou lá atrás.
/// </param>
/// <param name="Image">
/// A imagem final em PNG: recortada na área escolhida, com as anotações desenhadas
/// e o limitador de resolução já aplicado. É o que o usuário viu e o que foi para
/// a área de transferência.
/// </param>
/// <param name="CleanScreenshot">
/// A área de trabalho inteira em PNG, sem recorte e sem anotação, como estava no
/// instante do disparo. É a "foto limpa" que consumidores de análise usam — o
/// LiteShot entrega em tamanho real, e quem precisar reduzir reduz.
/// </param>
/// <param name="Timestamp">Momento da confirmação.</param>
public sealed record CaptureCompletedEvent(
    string StepId,
    byte[] Image,
    byte[] CleanScreenshot,
    DateTime Timestamp);