using System;
using System.IO;
using System.Text.Json;
using LiteFlow.Models;
using LiteQASuite.Core.Storage;

namespace LiteFlow.Storage;

/// <summary>
/// Leitura e gravação do <c>liteflow_settings.json</c>. Mesmo contrato do
/// <c>SettingsStore</c> do LiteShot, e pela mesma razão: <b>nunca lança</b>.
/// Arquivo ausente, JSON malformado ou valores impossíveis devolvem o padrão —
/// preferência corrompida não é motivo para o módulo não abrir.
///
/// O <see cref="Save"/> devolve <c>bool</c> em vez de engolir a falha, para a tela
/// poder avisar em vez de fingir que salvou.
/// </summary>
public sealed class SettingsStore
{
    private const string FileName = "liteflow_settings.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    /// <summary>Caminho do arquivo, em <c>%AppData%\LiteQASuite\</c>.</summary>
    public static string FilePath => Path.Combine(AppPaths.UserData, FileName);

    /// <summary>Lê as preferências. Devolve o padrão normalizado se algo der errado.</summary>
    public LiteFlowSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var settings = JsonSerializer.Deserialize<LiteFlowSettings>(json);
                if (settings is not null)
                {
                    settings.Normalize();
                    return settings;
                }
            }
        }
        catch (Exception)
        {
            // Preferência ilegível é padrão, não erro fatal.
        }

        var fallback = new LiteFlowSettings();
        fallback.Normalize();
        return fallback;
    }

    /// <summary>Grava as preferências. <c>false</c> quando não foi possível.</summary>
    public bool Save(LiteFlowSettings settings)
    {
        try
        {
            settings.Normalize();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}