using LiteQASuite.Core.Storage;
using LiteShot.Models;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace LiteShot.Settings;

/// <summary>
/// Lê e grava o <c>liteshot_settings.json</c>. Sucessor do <c>SettingsManager</c>
/// estático do código antigo — agora é instância, para que o caminho do arquivo
/// possa ser trocado (útil em teste) e para que o módulo tenha um dono claro da
/// persistência.
///
/// O local mudou: o antigo gravava ao lado do executável, o que quebra quando o
/// aplicativo é instalado em Program Files. Agora usa
/// <see cref="AppPaths.UserData"/> (<c>%AppData%\LiteQASuite</c>), padronizado
/// para todos os módulos da suíte.
///
/// Nem <see cref="Load"/> nem <see cref="Save"/> lançam: configuração corrompida
/// não pode derrubar o módulo.
/// </summary>
public sealed class SettingsStore
{
    private const string FileName = "liteshot_settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>Caminho completo do arquivo de configuração.</summary>
    public string FilePath { get; }

    /// <summary>Usa o caminho padrão: <c>%AppData%\LiteQASuite\liteshot_settings.json</c>.</summary>
    public SettingsStore()
        : this(Path.Combine(AppPaths.UserData, FileName))
    {
    }

    /// <param name="filePath">Caminho completo do arquivo a usar.</param>
    public SettingsStore(string filePath)
    {
        FilePath = filePath;
    }

    /// <summary>
    /// Lê as configurações do disco. Arquivo ausente, JSON malformado ou conteúdo
    /// inconsistente devolvem o padrão — nunca uma exceção. O resultado sempre
    /// passa por <see cref="LiteShotSettings.Normalize"/>.
    /// </summary>
    public LiteShotSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<LiteShotSettings>(json);

                if (loaded is not null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            // Um arquivo quebrado não pode impedir o módulo de subir. Quando o
            // logger do Core existir, isto vira um warning registrado.
            Debug.WriteLine($"[LiteShot] Falha ao ler {FilePath}: {ex.Message}");
        }

        var fresh = new LiteShotSettings();
        fresh.Normalize();
        return fresh;
    }

    /// <summary>
    /// Grava as configurações no disco.
    /// </summary>
    /// <returns>
    /// <c>true</c> se gravou. <c>false</c> em caso de falha (disco cheio, permissão
    /// negada, arquivo em uso) — o ViewModel usa isso para avisar o usuário em vez
    /// de fingir que salvou.
    /// </returns>
    public bool Save(LiteShotSettings settings)
    {
        try
        {
            settings.Normalize();

            var folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, SerializerOptions));
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LiteShot] Falha ao gravar {FilePath}: {ex.Message}");
            return false;
        }
    }
}