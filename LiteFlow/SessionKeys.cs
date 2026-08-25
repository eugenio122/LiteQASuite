namespace LiteFlow;

/// <summary>
/// As chaves que o LiteFlow escreve no <c>ISessionContext</c> — o quadro de avisos
/// compartilhado do Core.
///
/// <b>É assim que o LiteShot descobre o que dizer no toast de confirmação</b>
/// ("Copiado" quando não há cenário, "Copiado — adicionado a PED-1042" quando há)
/// sem nunca conhecer o LiteFlow. Estado compartilhado é exatamente para isto: o
/// barramento carrega eventos pontuais, o contexto de sessão carrega o que
/// persiste enquanto o app roda.
///
/// A leitura é por consulta, não por notificação: o LiteShot lê no instante em que
/// vai montar a mensagem. Por isso não precisou de evento novo nem de assinatura.
///
/// As chaves ficam nesta classe pública porque o outro lado precisa escrever a
/// mesma string. Enquanto o ajuste no LiteShot não acontece, ninguém lê — escrever
/// não custa nada e o dia em que ele ler, já está lá.
/// </summary>
public static class SessionKeys
{
    /// <summary><c>bool</c> — há cenário aberto e a gravação não está pausada.</summary>
    public const string Recording = "LiteFlow.Recording";

    /// <summary><c>string</c> — o ID do cenário aberto, ou vazio se não há nenhum.</summary>
    public const string ScenarioId = "LiteFlow.ScenarioId";
}