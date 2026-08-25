using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LiteFlow.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace LiteFlow.Export;

/// <summary>
/// Monta o <c>.docx</c>: copia o template, troca as tags e despeja as evidências.
/// Portado do <c>WordDocumentEngine</c> do LiteFlow 1.x — a heurística de tamanho
/// de imagem aqui dentro é conhecimento acumulado em uso real, não cálculo de
/// primeira, e por isso foi preservada quase intacta.
///
/// <b>Três mudanças em relação ao 1.x:</b>
/// <list type="number">
/// <item>Some o layout <c>Compacto</c>, que foi removido do produto.</item>
/// <item>O PNG vai <b>direto</b> para o documento. O 1.x decodificava para
/// <c>Bitmap</c> e re-codificava para PNG a cada evidência — trabalho puro, já que
/// o arquivo de origem já era um PNG. Agora só as dimensões são lidas do
/// cabeçalho.</item>
/// <item>Sem <c>System.Drawing</c>: a suíte é zero WinForms.</item>
/// </list>
/// </summary>
public static class WordDocumentEngine
{
    /// <summary>
    /// Prepara o arquivo de saída: cópia do template com as tags substituídas, ou
    /// um documento em branco quando não há template.
    /// </summary>
    public static void PrepareDocument(string templatePath, string outputPath, IReadOnlyDictionary<string, string> tags)
    {
        var folder = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

        if (!string.IsNullOrEmpty(templatePath) && File.Exists(templatePath))
        {
            File.Copy(templatePath, outputPath, overwrite: true);

            using var wordDocument = WordprocessingDocument.Open(outputPath, isEditable: true);
            var body = wordDocument.MainDocumentPart!.Document.Body;

            foreach (var tag in tags)
            {
                var safeValue = SanitizeForXml(tag.Value);

                // A troca é por nó de texto: uma tag partida em dois runs pelo Word
                // (acontece quando o autor do template digitou e voltou para editar)
                // não é encontrada. É o mesmo comportamento do 1.x — quem monta o
                // template já sabe digitar a tag de uma vez.
                foreach (var textNode in body!.Descendants<Text>().Where(t => t.Text.Contains(tag.Key)))
                    textNode.Text = textNode.Text.Replace(tag.Key, safeValue);
            }

            wordDocument.MainDocumentPart.Document.Save();
        }
        else
        {
            using var wordDocument = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            mainPart.Document.Save();
        }
    }

    /// <summary>
    /// Acrescenta as evidências ao fim do documento. No layout Padrão, uma por
    /// bloco; no Mobile, numa tabela sem bordas de N colunas — que é o que faz
    /// prints de celular, altos e estreitos, não desperdiçarem meia página cada.
    /// </summary>
    public static void AppendAllEvidence(
        string outputPath,
        IReadOnlyList<ExportEvidence> items,
        ReportLayout layout,
        int mobileColumns)
    {
        if (items.Count == 0) return;

        var columns = layout == ReportLayout.Mobile ? Math.Clamp(mobileColumns, 1, 3) : 1;

        using var wordDocument = WordprocessingDocument.Open(outputPath, isEditable: true);
        var mainPart = wordDocument.MainDocumentPart!;
        var body = mainPart.Document.Body!;

        if (columns <= 1)
        {
            foreach (var item in items)
                AppendSingleItem(mainPart, body, item, columns, layout);
        }
        else
        {
            body.AppendChild(BuildGrid(mainPart, items, columns, layout));
        }

        mainPart.Document.Save();
    }

    // ------------------------------------------------------------------ layout

    private static Table BuildGrid(
        MainDocumentPart mainPart,
        IReadOnlyList<ExportEvidence> items,
        int columns,
        ReportLayout layout)
    {
        const int totalWidthTwips = 8800;
        var cellWidthTwips = totalWidthTwips / columns;

        var table = new Table();

        table.AppendChild(new TableProperties(
            new TableWidth { Width = totalWidthTwips.ToString(), Type = TableWidthUnitValues.Dxa },
            new TableLayout { Type = TableLayoutValues.Fixed },
            new TableBorders(
                new TopBorder { Val = BorderValues.None },
                new BottomBorder { Val = BorderValues.None },
                new LeftBorder { Val = BorderValues.None },
                new RightBorder { Val = BorderValues.None },
                new InsideHorizontalBorder { Val = BorderValues.None },
                new InsideVerticalBorder { Val = BorderValues.None }),
            new TableCellMarginDefault(
                new TopMargin { Width = "115", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "115", Type = TableWidthUnitValues.Dxa },
                new LeftMargin { Width = "115", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "115", Type = TableWidthUnitValues.Dxa })));

        var grid = new TableGrid();
        for (var i = 0; i < columns; i++)
            grid.AppendChild(new GridColumn { Width = cellWidthTwips.ToString() });
        table.AppendChild(grid);

        for (var i = 0; i < items.Count; i += columns)
        {
            var row = new TableRow();

            // Sem isto, uma linha da tabela pode quebrar no meio da imagem quando
            // cai no fim da página.
            row.AppendChild(new TableRowProperties(new CantSplit { Val = OnOffOnlyValues.On }));

            for (var j = 0; j < columns; j++)
            {
                var cell = new TableCell();
                cell.AppendChild(new TableCellProperties(
                    new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = cellWidthTwips.ToString() },
                    new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Top }));

                if (i + j < items.Count) AppendItemToCell(mainPart, cell, items[i + j], columns, layout);
                else cell.AppendChild(new Paragraph(new Run(new Text(""))));

                row.AppendChild(cell);
            }

            table.AppendChild(row);
        }

        return table;
    }

    private static void AppendItemToCell(
        MainDocumentPart mainPart,
        OpenXmlElement parent,
        ExportEvidence item,
        int columns,
        ReportLayout layout)
    {
        var note = CreateNoteParagraph(item.Note, keepNext: false);
        var image = CreateImageParagraph(mainPart, item, columns, layout, keepNext: false);

        AppendInOrder(parent, note, image, item.TextBelowImage);
        parent.AppendChild(new Paragraph(new Run(new Text(""))));
    }

    private static void AppendSingleItem(
        MainDocumentPart mainPart,
        Body body,
        ExportEvidence item,
        int columns,
        ReportLayout layout)
    {
        // O KeepNext vai em quem vem primeiro: é o que impede o Word de deixar o
        // texto no fim de uma página e a imagem no começo da seguinte.
        var note = CreateNoteParagraph(item.Note, keepNext: !item.TextBelowImage);
        var image = CreateImageParagraph(mainPart, item, columns, layout, keepNext: item.TextBelowImage);

        AppendInOrder(body, note, image, item.TextBelowImage);
        body.AppendChild(new Paragraph(new Run(new Text(""))));
    }

    private static void AppendInOrder(OpenXmlElement parent, Paragraph? note, Paragraph? image, bool textBelowImage)
    {
        if (textBelowImage)
        {
            if (image is not null) parent.AppendChild(image);
            if (note is not null) parent.AppendChild(note);
        }
        else
        {
            if (note is not null) parent.AppendChild(note);
            if (image is not null) parent.AppendChild(image);
        }
    }

    // ---------------------------------------------------------------- conteúdo

    private static Paragraph? CreateNoteParagraph(string note, bool keepNext)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;

        var safeNote = SanitizeForXml(note);
        var run = new Run();
        run.AppendChild(new RunProperties(
            new Bold(),
            new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "404040" },
            new FontSize { Val = "20" }));

        var lines = safeNote.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        for (var i = 0; i < lines.Length; i++)
        {
            run.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            if (i < lines.Length - 1) run.AppendChild(new Break());
        }

        var properties = new ParagraphProperties();
        if (keepNext) properties.AppendChild(new KeepNext { Val = true });
        properties.AppendChild(new SpacingBetweenLines { Before = "100", After = "100" });
        properties.AppendChild(new Justification { Val = JustificationValues.Left });

        return new Paragraph(properties, run);
    }

    private static Paragraph? CreateImageParagraph(
        MainDocumentPart mainPart,
        ExportEvidence item,
        int columns,
        ReportLayout layout,
        bool keepNext)
    {
        if (string.IsNullOrEmpty(item.ImagePath) || !File.Exists(item.ImagePath)) return null;

        byte[] png;
        try
        {
            png = File.ReadAllBytes(item.ImagePath);
        }
        catch (IOException)
        {
            return null;
        }

        if (!TryReadPixelSize(png, out var width, out var height)) return null;

        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        using (var stream = new MemoryStream(png, writable: false))
        {
            imagePart.FeedData(stream);
        }

        var drawing = CreateImageElement(
            mainPart.GetIdOfPart(imagePart), width, height, item.Note, columns, layout);

        var properties = new ParagraphProperties();
        if (keepNext) properties.AppendChild(new KeepNext { Val = true });
        properties.AppendChild(new KeepLines { Val = true });
        properties.AppendChild(new Justification { Val = JustificationValues.Center });

        return new Paragraph(properties, new Run(drawing));
    }

    /// <summary>
    /// Lê largura e altura sem decodificar a imagem inteira — o decodificador para
    /// no cabeçalho porque nada além das dimensões é pedido.
    /// </summary>
    private static bool TryReadPixelSize(byte[] png, out int width, out int height)
    {
        width = 0;
        height = 0;

        try
        {
            using var stream = new MemoryStream(png, writable: false);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.DelayCreation | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.None);

            if (decoder.Frames.Count == 0) return false;

            width = decoder.Frames[0].PixelWidth;
            height = decoder.Frames[0].PixelHeight;
            return width > 0 && height > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ------------------------------------------------------------- geometria

    /// <summary>
    /// O cálculo de tamanho da imagem no documento, em EMU (914400 por polegada).
    ///
    /// O caso interessante é o print <b>em retrato</b> no layout Padrão: uma
    /// imagem alta com um texto comprido em cima empurra a imagem para a página
    /// seguinte e deixa meia folha em branco. A regra desce a altura da imagem até
    /// 80% do padrão para tentar caber junto do texto; se nem assim couber, deixa
    /// a imagem no tamanho cheio e aceita a quebra — encolher demais só produziria
    /// um print ilegível numa página vazia.
    /// </summary>
    private static Drawing CreateImageElement(
        string relationshipId,
        int width,
        int height,
        string note,
        int columns,
        ReportLayout layout)
    {
        const long baseWidthEmu = 5400000L;
        var maxWidthEmu = (baseWidthEmu / columns) - 150000L;

        long maxHeightEmu;

        if (layout == ReportLayout.Padrao)
        {
            var isLandscape = width > height;

            if (isLandscape)
            {
                maxHeightEmu = 5400000L;
            }
            else
            {
                const long usablePageHeight = 8200000L;
                const long standardImageHeight = 7200000L;
                const long reducedImageHeight = 5760000L;   // 80% de standardImageHeight

                var textLines = 0;
                if (!string.IsNullOrWhiteSpace(note))
                {
                    foreach (var line in note.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                        textLines += 1 + (line.Length / 85);
                }

                var textHeight = (textLines * 250000L) + 300000L;
                var availableHeight = usablePageHeight - textHeight;

                maxHeightEmu = availableHeight >= reducedImageHeight
                    ? Math.Min(standardImageHeight, availableHeight)
                    : standardImageHeight;
            }
        }
        else
        {
            maxHeightEmu = 6500000L;
        }

        // 9525 EMU por pixel a 96 DPI.
        var originalWidthEmu = width * 9525L;
        var originalHeightEmu = height * 9525L;

        var ratioX = originalWidthEmu > maxWidthEmu ? (double)maxWidthEmu / originalWidthEmu : 1.0;
        var ratioY = originalHeightEmu > maxHeightEmu ? (double)maxHeightEmu / originalHeightEmu : 1.0;

        var ratio = Math.Min(ratioX, ratioY);
        var finalWidthEmu = (long)(originalWidthEmu * ratio);
        var finalHeightEmu = (long)(originalHeightEmu * ratio);

        var imageName = "Evidencia_" + Guid.NewGuid().ToString("N")[..8];

        return new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = finalWidthEmu, Cy = finalHeightEmu },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = (UInt32Value)1U, Name = imageName },
                new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = (UInt32Value)0U, Name = imageName + ".png" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip(new A.BlipExtensionList(
                                    new A.BlipExtension { Uri = "{28A0092B-C50C-407E-A947-70E740481C1C}" }))
                                {
                                    Embed = relationshipId,
                                    CompressionState = A.BlipCompressionValues.Print
                                },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = finalWidthEmu, Cy = finalHeightEmu }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
                    }))
            {
                DistanceFromTop = (UInt32Value)0U,
                DistanceFromBottom = (UInt32Value)0U,
                DistanceFromLeft = (UInt32Value)0U,
                DistanceFromRight = (UInt32Value)0U,
                EditId = "50D07946"
            });
    }

    /// <summary>
    /// Tira do texto os caracteres de controle que o XML do Word não aceita. O
    /// <c>\v</c> vira quebra de linha porque é o que o Windows insere quando se
    /// cola texto de certas origens — e ele sozinho corrompia o documento inteiro.
    /// </summary>
    private static string SanitizeForXml(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        text = text.Replace("\v", "\n");
        return Regex.Replace(text, @"[\x00-\x08\x0C\x0E-\x1F]", "");
    }
}