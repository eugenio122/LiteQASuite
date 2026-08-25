using System;
using System.IO;

namespace LiteQASuite.Core.Storage;

/// <summary>
/// Caminhos padronizados de dados do LiteQASuite, para todos os módulos gravarem
/// no mesmo lugar.
///
/// <b>O aplicativo é portátil:</b> tudo mora numa pasta <c>Data</c> ao lado do
/// executável. Copiar a pasta do LiteQASuite para um pen drive leva junto as
/// preferências, os templates e a referência do Workspace — que é o ponto de
/// trabalhar em máquinas diferentes sem reconfigurar nada.
///
/// <b>Não há alternativa em caso de falha, e isso é deliberado.</b> Se a pasta do
/// executável não for gravável (instalado em Program Files, mídia protegida), as
/// configurações simplesmente não são salvas — o aplicativo continua funcionando
/// com os padrões. Cair para o <c>%AppData%</c> em silêncio criaria o pior dos
/// mundos: metade das configurações num lugar, metade no outro, e nenhuma pista
/// de qual está valendo.
/// </summary>
public static class AppPaths
{
    private const string PortableFolderName = "Data";
    private const string LegacyFolderName = "LiteQASuite";

    private static readonly string _userData = Initialize();

    /// <summary>
    /// Pasta de dados do LiteQASuite: <c>&lt;pasta do .exe&gt;\Data</c>. É onde
    /// ficam <c>workspace.json</c>, <c>liteshot_settings.json</c>,
    /// <c>liteflow_settings.json</c> e a pasta <c>Templates</c>.
    /// </summary>
    public static string UserData => _userData;

    private static string Initialize()
    {
        var path = Path.Combine(AppContext.BaseDirectory, PortableFolderName);

        try
        {
            var isFirstRun = !Directory.Exists(path);
            Directory.CreateDirectory(path);

            // Migração única: quem já usava a versão que gravava no %AppData%
            // encontra tudo no lugar novo, sem reconfigurar o Workspace nem
            // reimportar os templates. Acontece uma vez e nunca mais — depois disso
            // a pasta Data existe e este trecho não roda.
            if (isFirstRun) TryMigrateFromRoaming(path);
        }
        catch (Exception)
        {
            // Pasta não gravável. O caminho continua sendo este; quem escrever é
            // que vai descobrir que não dá, e cada store já trata isso.
        }

        return path;
    }

    private static void TryMigrateFromRoaming(string destination)
    {
        try
        {
            var legacy = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                LegacyFolderName);

            if (!Directory.Exists(legacy)) return;

            foreach (var file in Directory.EnumerateFiles(legacy))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);

            foreach (var folder in Directory.EnumerateDirectories(legacy))
            {
                var target = Path.Combine(destination, Path.GetFileName(folder));
                Directory.CreateDirectory(target);

                foreach (var file in Directory.EnumerateFiles(folder))
                    File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
            }
        }
        catch (Exception)
        {
            // Migração é cortesia, não requisito: falhar aqui só significa
            // reconfigurar uma vez.
        }
    }
}