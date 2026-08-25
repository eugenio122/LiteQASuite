using System;
using System.IO;
using LiteQASuite.Core.Storage;

namespace LiteFlow.Storage;

/// <summary>
/// Guarda os templates <c>.docx</c> num lugar estável:
/// <c>%AppData%\LiteQASuite\Templates\</c>.
///
/// <b>Por que copiar em vez de só apontar.</b> O <c>.lflow</c> grava o caminho do
/// template, e esse caminho tem que continuar valendo daqui a três semanas. Um
/// template escolhido em <c>Downloads</c>, ou numa pasta de rede que só existe
/// conectado à VPN, some sem avisar — e a falha aparece justamente na hora de
/// exportar o relatório. Copiando, o cenário reaberto exporta igual ao que
/// exportou no dia.
///
/// O 1.x só copiava quando o usuário marcava "usar como padrão"; um template
/// escolhido só para aquele relatório ficava com a referência frágil.
/// </summary>
public sealed class TemplateStore
{
    /// <summary>Pasta onde os templates ficam.</summary>
    public static string Folder => Path.Combine(AppPaths.UserData, "Templates");

    /// <summary>
    /// Copia o arquivo para a pasta de templates e devolve o novo caminho. Se ele
    /// já estiver lá, devolve o próprio caminho sem copiar sobre si mesmo.
    /// Um arquivo de mesmo nome é substituído — reimportar um template corrigido é
    /// o gesto normal.
    /// </summary>
    public string Import(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Template não encontrado.", sourcePath);

        Directory.CreateDirectory(Folder);

        var destination = Path.Combine(Folder, Path.GetFileName(sourcePath));

        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
            return destination;

        File.Copy(sourcePath, destination, overwrite: true);
        return destination;
    }

    /// <summary>
    /// O nome que aparece na tela: só o arquivo, sem o caminho. Vazio devolve
    /// <c>null</c>, e quem chama decide o texto de "nenhum template".
    /// </summary>
    public static string? DisplayName(string? templatePath) =>
        string.IsNullOrWhiteSpace(templatePath) ? null : Path.GetFileName(templatePath);
}