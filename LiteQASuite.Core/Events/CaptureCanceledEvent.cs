namespace LiteQASuite.Core.Events;

/// <summary>
/// A captura terminou sem virar um passo do cenário. Publicado pelo LiteShot tanto
/// quando o usuário desiste quanto quando ele grava um arquivo local — o
/// <see cref="Reason"/> separa os dois.
///
/// <b>Este evento fecha um furo real do fluxo antigo.</b> O
/// <see cref="CaptureStartedEvent"/> faz o assinante criar um passo pendente. Se
/// nada mais chegasse, esse passo ficaria pendurado para sempre — era exatamente o
/// que acontecia no LiteTools quando o usuário clicava em "salvar" em vez de
/// "copiar": o arquivo ia para o disco e o passo pendente nunca era confirmado nem
/// descartado.
///
/// Todo <see cref="CaptureStartedEvent"/> tem, obrigatoriamente, um
/// <see cref="CaptureCompletedEvent"/> ou um <see cref="CaptureCanceledEvent"/> com
/// o mesmo <see cref="StepId"/>.
///
/// <b>Publicação assíncrona</b>, na thread que estiver disponível.
/// </summary>
/// <param name="StepId">O mesmo id que veio no <see cref="CaptureStartedEvent"/>.</param>
/// <param name="Reason">Desistência do usuário ou gravação local.</param>
/// <param name="Timestamp">Momento do encerramento.</param>
public sealed record CaptureCanceledEvent(
    string StepId,
    CaptureCancelReason Reason,
    DateTime Timestamp);