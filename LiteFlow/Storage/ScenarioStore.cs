using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LiteFlow.Models;

namespace LiteFlow.Storage;

/// <summary>
/// Leitura e gravação do <c>.lflow</c>. É o único lugar do módulo que conhece o
/// formato do arquivo.
///
/// <b>Por que é escrito à mão, campo a campo, em vez de um
/// <c>JsonSerializer.Serialize(documento)</c>.</b> Porque as imagens estão dentro
/// do arquivo. Serializar o objeto exigiria que cada PNG já estivesse em memória
/// como <c>byte[]</c> e depois como string base64 — num cenário de quarenta prints
/// em 4K isso é meio giga alocado a cada salvamento, e o autosave salva a cada
/// poucos segundos. Com o <see cref="Utf8JsonWriter"/>, cada PNG é lido do cache e
/// escrito direto no arquivo: uma imagem por vez atravessa a memória.
///
/// <b>Gravação atômica.</b> Escreve num <c>.tmp</c> irmão e só então substitui o
/// original. Uma queda no meio da escrita deixa o cenário anterior intacto em vez
/// de um arquivo pela metade — que, num formato com as imagens embutidas, seria a
/// perda do cenário inteiro.
/// </summary>
public static class ScenarioStore
{
    private static readonly JsonSerializerOptions AnnotationOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Grava o documento em <paramref name="filePath"/>. Os PNGs vêm do
    /// <c>CachePath</c> de cada passo; um passo cujo arquivo de cache sumiu é
    /// gravado com imagem vazia em vez de derrubar o salvamento inteiro — perder
    /// um print é ruim, perder o cenário é pior.
    ///
    /// Roda em segundo plano: receba sempre um <c>Clone()</c>, nunca o documento
    /// que a interface está editando.
    /// </summary>
    public static void Save(string filePath, ScenarioDocument document)
    {
        var folder = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        var tempPath = filePath + ".tmp";

        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();

                writer.WriteNumber(ScenarioSchema.SchemaVersion, ScenarioSchema.CurrentVersion);
                writer.WriteString(ScenarioSchema.ScenarioId, document.ScenarioId);

                // O mesmo valor sob o nome antigo: um .lflow gravado aqui continua
                // abrindo no LiteFlow 1.x, e não só o contrário.
                writer.WriteString(ScenarioSchema.FileName, document.ScenarioId);

                writer.WriteString(ScenarioSchema.TemplatePath, document.TemplatePath);
                writer.WriteString(ScenarioSchema.FilePrefix, document.FilePrefix);
                writer.WriteString(ScenarioSchema.TestCaseName, document.TestCaseName);
                writer.WriteString(ScenarioSchema.QAName, document.QAName);
                writer.WriteString(ScenarioSchema.TestDate, document.TestDate);
                writer.WriteString(ScenarioSchema.Comments, document.Comments);
                writer.WriteNumber(ScenarioSchema.ReportLayout, (int)document.ReportLayout);
                writer.WriteNumber(ScenarioSchema.MobileColumns, document.MobileColumns);

                writer.WritePropertyName(ScenarioSchema.Steps);
                writer.WriteStartArray();

                foreach (var step in document.Steps)
                {
                    writer.WriteStartObject();
                    writer.WriteString(ScenarioSchema.StepId, step.StepId);

                    var image = ReadCachedImage(step.CachePath);
                    if (image.Length > 0) writer.WriteBase64String(ScenarioSchema.ImageData, image);
                    else writer.WriteString(ScenarioSchema.ImageData, string.Empty);

                    writer.WriteString(ScenarioSchema.Note, step.Note);
                    writer.WriteBoolean(ScenarioSchema.TextBelowImage, step.TextBelowImage);
                    writer.WriteBoolean(ScenarioSchema.IsEvidenceOnly, step.IsEvidenceOnly);

                    writer.WritePropertyName(ScenarioSchema.Annotations);
                    JsonSerializer.Serialize(writer, step.Annotations, AnnotationOptions);

                    writer.WriteEndObject();

                    // O buffer do escritor cresce com o base64 do print; devolvê-lo
                    // ao arquivo aqui mantém o pico em uma imagem, não em todas.
                    writer.Flush();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            if (File.Exists(filePath)) File.Replace(tempPath, filePath, null, ignoreMetadataErrors: true);
            else File.Move(tempPath, filePath);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch (IOException) { /* o .tmp sobra; o cenário está salvo */ }
            }
        }
    }

    /// <summary>
    /// Lê o <c>.lflow</c>. Cada PNG é entregue a <paramref name="storeImage"/>
    /// (que devolve o caminho no cache) e sai da memória em seguida — a imagem
    /// nunca fica no documento.
    ///
    /// Aceita tanto o formato 2 quanto o do LiteFlow 1.x: campos ausentes viram
    /// padrão, <c>FileName</c> serve de <c>ScenarioId</c>, e o layout
    /// <c>2</c> (Compacto, extinto) lê como Padrão.
    /// </summary>
    /// <param name="storeImage">Recebe (stepId, PNG) e devolve o caminho no cache.</param>
    public static ScenarioDocument Load(string filePath, Func<string, byte[], string> storeImage)
    {
        using var stream = File.OpenRead(filePath);
        using var json = JsonDocument.Parse(stream);

        var root = json.RootElement;

        var document = new ScenarioDocument
        {
            SchemaVersion = ReadInt(root, ScenarioSchema.SchemaVersion, 1),
            ScenarioId = ReadString(root, ScenarioSchema.ScenarioId, ReadString(root, ScenarioSchema.FileName, "")),
            TemplatePath = ReadString(root, ScenarioSchema.TemplatePath, ""),
            FilePrefix = ReadString(root, ScenarioSchema.FilePrefix, ""),
            TestCaseName = ReadString(root, ScenarioSchema.TestCaseName, ""),
            QAName = ReadString(root, ScenarioSchema.QAName, ""),
            TestDate = ReadString(root, ScenarioSchema.TestDate, ""),
            Comments = ReadString(root, ScenarioSchema.Comments, ""),
            ReportLayout = ReadLayout(root),
            MobileColumns = ReadInt(root, ScenarioSchema.MobileColumns, 2)
        };

        if (document.MobileColumns is < 1 or > 3) document.MobileColumns = 2;

        if (root.TryGetProperty(ScenarioSchema.Steps, out var steps) && steps.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in steps.EnumerateArray())
            {
                var step = new EvidenceStep
                {
                    StepId = ReadString(element, ScenarioSchema.StepId, Guid.NewGuid().ToString("N")),
                    Note = ReadString(element, ScenarioSchema.Note, ""),
                    TextBelowImage = ReadBool(element, ScenarioSchema.TextBelowImage),
                    IsEvidenceOnly = ReadBool(element, ScenarioSchema.IsEvidenceOnly),
                    Annotations = ReadAnnotations(element)
                };

                var image = ReadImageBytes(element);
                if (image.Length > 0) step.CachePath = storeImage(step.StepId, image);

                document.Steps.Add(step);
            }
        }

        // O arquivo pode ter vindo do 1.x; a partir de agora ele é gravado na
        // versão corrente.
        document.SchemaVersion = ScenarioSchema.CurrentVersion;

        return document;
    }

    /// <summary>
    /// Lê só o cabeçalho do <c>.lflow</c>: o ID e o caso de teste, para rotular o
    /// cenário na árvore do Workspace.
    ///
    /// <b>Para de ler ao chegar no array de passos.</b> É o que torna isto viável:
    /// os metadados são gravados antes dos passos, então o leitor consome algumas
    /// centenas de bytes e desiste — em vez dos duzentos megabytes de base64 que
    /// vêm depois. Sem esse corte, montar a árvore de trinta cenários leria o
    /// Workspace inteiro para mostrar trinta linhas de texto.
    ///
    /// Nunca lança: um arquivo corrompido devolve o que deu para ler, e a árvore
    /// mostra o cenário pelo ID da pasta.
    /// </summary>
    public static ScenarioSummary ReadSummary(string filePath)
    {
        var scenarioId = "";
        var testCaseName = "";

        try
        {
            const int headerBudget = 128 * 1024;

            byte[] buffer;
            int read;

            using (var stream = File.OpenRead(filePath))
            {
                var size = (int)Math.Min(headerBudget, stream.Length);
                buffer = new byte[size];
                read = stream.Read(buffer, 0, size);
            }

            // isFinalBlock: false — o buffer é um pedaço do arquivo, não o arquivo.
            // O leitor devolve false quando acaba o que dá para ler inteiro, em vez
            // de reclamar de JSON truncado.
            var reader = new Utf8JsonReader(buffer.AsSpan(0, read), isFinalBlock: false, state: default);

            while (reader.Read())
            {
                if (reader.TokenType != JsonTokenType.PropertyName) continue;

                var name = reader.GetString();

                // Daqui para a frente são as imagens. É o ponto de parada.
                if (name == ScenarioSchema.Steps) break;

                if (!reader.Read()) break;
                if (reader.TokenType != JsonTokenType.String) continue;

                if (name == ScenarioSchema.ScenarioId)
                {
                    scenarioId = reader.GetString() ?? "";
                }
                else if (name == ScenarioSchema.FileName && string.IsNullOrEmpty(scenarioId))
                {
                    // Formato 1.x: o ID vinha sob o nome antigo.
                    scenarioId = reader.GetString() ?? "";
                }
                else if (name == ScenarioSchema.TestCaseName)
                {
                    testCaseName = reader.GetString() ?? "";
                }
            }
        }
        catch (Exception)
        {
            // Arquivo ilegível: devolve o que houver. Rotular mal um cenário é
            // muito melhor do que a árvore inteira não abrir por causa de um.
        }

        return new ScenarioSummary(scenarioId, testCaseName);
    }

    private static byte[] ReadCachedImage(string cachePath)
    {
        try
        {
            return string.IsNullOrEmpty(cachePath) || !File.Exists(cachePath)
                ? Array.Empty<byte>()
                : File.ReadAllBytes(cachePath);
        }
        catch (IOException)
        {
            return Array.Empty<byte>();
        }
    }

    private static byte[] ReadImageBytes(JsonElement step)
    {
        if (!step.TryGetProperty(ScenarioSchema.ImageData, out var value) || value.ValueKind != JsonValueKind.String)
            return Array.Empty<byte>();

        try
        {
            return value.GetBytesFromBase64();
        }
        catch (FormatException)
        {
            return Array.Empty<byte>();
        }
    }

    private static List<Annotation> ReadAnnotations(JsonElement step)
    {
        if (!step.TryGetProperty(ScenarioSchema.Annotations, out var value) || value.ValueKind != JsonValueKind.Array)
            return new List<Annotation>();

        try
        {
            return JsonSerializer.Deserialize<List<Annotation>>(value.GetRawText(), AnnotationOptions)
                   ?? new List<Annotation>();
        }
        catch (JsonException)
        {
            return new List<Annotation>();
        }
    }

    private static ReportLayout ReadLayout(JsonElement root)
    {
        var raw = ReadInt(root, ScenarioSchema.ReportLayout, 0);
        return raw == (int)ReportLayout.Mobile ? ReportLayout.Mobile : ReportLayout.Padrao;
    }

    private static string ReadString(JsonElement parent, string name, string fallback) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static int ReadInt(JsonElement parent, string name, int fallback) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : fallback;

    private static bool ReadBool(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}