using System;

namespace LiteQASuite.Core.Events;

/// <summary>
/// Um cenário foi iniciado no LiteFlow: a pasta do cenário já existe e o
/// <c>.lflow</c> foi criado. Publicado pelo LiteFlow para que outros módulos
/// preparem seus próprios artefatos na mesma pasta — o LiteJson, <b>se o motor
/// estiver ligado</b>, reage criando o <c>.json</c>. Se o motor estiver desligado,
/// o <c>.json</c> simplesmente não nasce (cada módulo é dono do próprio artefato).
///
/// Não carrega os metadados do relatório de propósito: eles vivem no <c>.lflow</c>
/// e são assunto do LiteFlow. Este evento diz só <i>onde</i> o cenário mora.
///
/// <b>O <see cref="ScenarioFolderPath"/> é o que basta para criar um arquivo
/// irmão</b> — o squad e o ciclo vêm junto para quem precisar rotular ou agrupar,
/// não para remontar caminho na mão.
/// </summary>
/// <param name="ScenarioId">O ID do cenário (antigo "Nome do Arquivo").</param>
/// <param name="Squad">O squad/projeto ao qual o ciclo pertence.</param>
/// <param name="Cycle">O ciclo/sprint ao qual o cenário pertence.</param>
/// <param name="ScenarioFolderPath">Caminho absoluto da pasta do cenário.</param>
/// <param name="Timestamp">Momento do início.</param>
public sealed record ScenarioStartedEvent(
    string ScenarioId,
    string Squad,
    string Cycle,
    string ScenarioFolderPath,
    DateTime Timestamp);