using LiteFlow.Models;

namespace LiteFlow.Scenario;

/// <summary>
/// O cenário que está aberto agora: onde ele mora e o que ele contém. Existir
/// significa "há um cenário iniciado" — e é essa distinção que a regra do produto
/// usa: sem sessão, uma captura não vira evidência.
///
/// Os caminhos são resolvidos <b>uma vez</b>, pelo <c>IWorkspaceService</c>, no
/// momento em que o cenário nasce ou é aberto. O módulo nunca monta caminho na mão
/// nem guarda a raiz do Workspace: quem é dono da estrutura de pastas é o Core.
/// </summary>
public sealed class ScenarioSession
{
    public ScenarioSession(
        string squad,
        string cycle,
        string scenarioId,
        string folderPath,
        string filePath,
        ScenarioDocument document)
    {
        Squad = squad;
        Cycle = cycle;
        ScenarioId = scenarioId;
        FolderPath = folderPath;
        FilePath = filePath;
        Document = document;
    }

    /// <summary>O squad/projeto ao qual o ciclo pertence.</summary>
    public string Squad { get; }

    /// <summary>O ciclo/sprint ao qual o cenário pertence.</summary>
    public string Cycle { get; }

    /// <summary>O ID do cenário — nomeia a pasta e o arquivo.</summary>
    public string ScenarioId { get; }

    /// <summary>
    /// Pasta do cenário. O <c>.json</c> do LiteJson nasce aqui, e é para cá que o
    /// relatório exportado vai — cenário e entregável no mesmo lugar.
    /// </summary>
    public string FolderPath { get; }

    /// <summary>Caminho completo do <c>.lflow</c>.</summary>
    public string FilePath { get; }

    /// <summary>O conteúdo do cenário — metadados e passos.</summary>
    public ScenarioDocument Document { get; }
}