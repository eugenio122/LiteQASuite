namespace LiteQASuite.Core.Events;

/// <summary>
/// O usuário acionou o atalho de captura. Publicado pelo LiteShot no instante do
/// disparo, <b>antes</b> de a lente escurecer a tela.
///
/// <b>É um sinal, não um payload.</b> Não carrega imagem de propósito: quem assina
/// este evento — hoje o LiteJson — precisa congelar o estado da tela (extrair a
/// árvore de UI, o DOM) no momento exato em que ela ainda está intacta, e para
/// isso não olha um único pixel.
///
/// Carregar a imagem aqui obrigaria o LiteShot a codificar um PNG da área de
/// trabalho inteira de forma síncrona, antes de mostrar o overlay — num setup com
/// dois monitores 4K são mais de trinta milhões de pixels, e a tela demoraria
/// visivelmente para congelar. Os pixels vêm depois, no
/// <see cref="CaptureCompletedEvent"/>.
///
/// <b>Publicação síncrona.</b> O <c>Publish</c> invoca os handlers na thread
/// chamadora e só retorna quando todos terminam — é isso que garante que o
/// assinante extraia o estado da tela antes de o overlay aparecer. Handlers deste
/// evento devem ser rápidos.
/// </summary>
/// <param name="StepId">
/// Identificador do passo, gerado pelo LiteShot. O mesmo id volta no evento de
/// conclusão ou de cancelamento — é ele que amarra "começou" a "terminou".
/// </param>
/// <param name="Timestamp">Momento do disparo.</param>
public sealed record CaptureStartedEvent(string StepId, DateTime Timestamp);