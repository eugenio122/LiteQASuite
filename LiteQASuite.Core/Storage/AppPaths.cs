using System;
using System.IO;

namespace LiteQASuite.Core.Storage;

/// <summary>
/// Caminhos padronizados de dados do usuário, para todos os módulos gravarem no
/// mesmo lugar. Substitui o velho hábito de escrever ao lado do .exe — que quebra
/// quando o app é instalado em Program Files (onde escrever exige elevação).
/// </summary>
public static class AppPaths
{
    /// <summary>
    /// Pasta de dados do usuário do LiteQASuite (<c>%AppData%\LiteQASuite</c>).
    /// Criada na primeira chamada se ainda não existir. É onde ficam as
    /// configurações dos módulos, como o <c>liteshot_settings.json</c>.
    /// </summary>
    public static string UserData
    {
        get
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "LiteQASuite");

            Directory.CreateDirectory(path);
            return path;
        }
    }
}