using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LiteFlow.Models;

namespace LiteFlow.Export;

/// <summary>
/// A pipeline de exportação: gera o <c>.docx</c>, e converte para PDF quando
/// pedido. Roda fora da thread de interface — um relatório com quarenta prints
/// leva segundos.
///
/// <b>O PDF depende de um programa instalado na máquina, e isso não tem como
/// mudar.</b> O OpenXML monta o <c>.docx</c>, mas não sabe paginar nem renderizar;
/// quem transforma isso num PDF com a formatação exata do template é o Word ou o
/// LibreOffice. Tentar gerar o PDF por conta própria significaria reimplementar o
/// motor de layout do Word — e o relatório sairia parecido, nunca igual.
/// </summary>
public static class ExportService
{
    /// <summary>
    /// Gera o <c>.docx</c> no caminho final. Sobrescreve se já existir — exportar
    /// de novo é o gesto normal depois de corrigir uma nota.
    /// </summary>
    public static void ExportToWord(
        string outputPath,
        string templatePath,
        IReadOnlyDictionary<string, string> tags,
        IReadOnlyList<ExportEvidence> items,
        ReportLayout layout,
        int mobileColumns)
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);

        WordDocumentEngine.PrepareDocument(templatePath, outputPath, tags);
        WordDocumentEngine.AppendAllEvidence(outputPath, items, layout, mobileColumns);
    }

    /// <summary>
    /// Gera só o PDF: monta um <c>.docx</c> num arquivo temporário de nome único,
    /// converte, e apaga. O nome único importa — o 1.x reaproveitava um nome fixo
    /// e ocasionalmente exportava o relatório da vez anterior.
    /// </summary>
    public static void ExportToPdf(
        string outputPath,
        string templatePath,
        IReadOnlyDictionary<string, string> tags,
        IReadOnlyList<ExportEvidence> items,
        ReportLayout layout,
        int mobileColumns)
    {
        var tempWord = Path.Combine(Path.GetTempPath(), $"LiteFlowExport_{Guid.NewGuid():N}.docx");

        try
        {
            WordDocumentEngine.PrepareDocument(templatePath, tempWord, tags);
            WordDocumentEngine.AppendAllEvidence(tempWord, items, layout, mobileColumns);
            ConvertDocxToPdf(tempWord, outputPath);
        }
        finally
        {
            if (File.Exists(tempWord))
            {
                try { File.Delete(tempWord); } catch (IOException) { /* o Windows limpa depois */ }
            }
        }
    }

    /// <summary>
    /// Converte um <c>.docx</c> existente em PDF. Público porque exportar os dois
    /// formatos de uma vez converte o <c>.docx</c> que acabou de sair, em vez de
    /// montar o documento duas vezes.
    ///
    /// Tenta o Word primeiro (é quem respeita o template ao pé da letra) e cai
    /// para o LibreOffice. Sem nenhum dos dois, lança — e a tela conta o porquê,
    /// em vez de deixar o usuário procurando um PDF que nunca foi criado.
    /// </summary>
    public static void ConvertDocxToPdf(string docxPath, string pdfPath)
    {
        if (TryConvertWithWord(docxPath, pdfPath)) return;
        if (TryConvertWithLibreOffice(docxPath, pdfPath)) return;

        throw new InvalidOperationException(
            "Para gerar o PDF é preciso ter o Microsoft Word ou o LibreOffice instalado — " +
            "é o que garante que o relatório saia com a formatação exata do template. " +
            "O .docx pode ser gerado normalmente sem eles.");
    }

    private static bool TryConvertWithWord(string docxPath, string pdfPath)
    {
        var wordType = Type.GetTypeFromProgID("Word.Application");
        if (wordType is null) return false;

        dynamic? word = null;

        try
        {
            word = Activator.CreateInstance(wordType);
            if (word is null) return false;

            word.Visible = false;

            var document = word.Documents.Open(docxPath);
            try
            {
                // 17 = wdExportFormatPDF.
                document.ExportAsFixedFormat(pdfPath, 17);
            }
            finally
            {
                document.Close(false);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            if (word is not null)
            {
                try { word.Quit(); } catch (Exception) { /* instância já morta */ }
            }
        }
    }

    private static bool TryConvertWithLibreOffice(string docxPath, string pdfPath)
    {
        string[] candidates =
        {
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        };

        string? soffice = null;
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate)) { soffice = candidate; break; }
        }

        if (soffice is null) return false;

        var outputFolder = Path.GetDirectoryName(pdfPath)!;

        using var process = new Process();
        process.StartInfo.FileName = soffice;
        process.StartInfo.Arguments =
            $"--headless --convert-to pdf \"{docxPath}\" --outdir \"{outputFolder}\"";
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        process.StartInfo.CreateNoWindow = true;

        process.Start();
        process.WaitForExit();

        // O LibreOffice nomeia a saída pelo arquivo de entrada; se o nome final for
        // outro, o arquivo é movido para o lugar certo.
        var produced = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(docxPath) + ".pdf");

        if (!string.Equals(produced, pdfPath, StringComparison.OrdinalIgnoreCase) && File.Exists(produced))
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
            File.Move(produced, pdfPath);
        }

        return File.Exists(pdfPath);
    }

    /// <summary>
    /// As tags que o template conhece. São as mesmas quatro do 1.x — mudar esse
    /// vocabulário quebraria todos os templates que já existem por aí.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildTags(ScenarioDocument document) =>
        new Dictionary<string, string>
        {
            { "{CASO}", document.TestCaseName ?? "" },
            { "{QA}", document.QAName ?? "" },
            { "{DATA}", string.IsNullOrWhiteSpace(document.TestDate) ? DateTime.Now.ToString("dd/MM/yyyy") : document.TestDate },
            { "{OBS}", document.Comments ?? "" }
        };
}