using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;

namespace Kesinti.Ingest.Docx;

/// <summary>
/// .docx dosyalarindan duz metin cikarir: paragraflar ve tablo hucreleri, dokuman icindeki
/// sirayla birlestirilir. Bicimleme bilgisi (yazi tipi, renk vb.) atilir; sadece metin alinir.
/// </summary>
public static class DocxReader
{
    public static string ReadPlainText(string filePath)
    {
        using var document = WordprocessingDocument.Open(filePath, isEditable: false);
        var body = document.MainDocumentPart?.Document.Body
            ?? throw new InvalidOperationException($"'{filePath}' dosyasinda govde (body) bulunamadi.");

        var sb = new StringBuilder();
        AppendBlockText(body, sb);
        return sb.ToString().Trim();
    }

    private static void AppendBlockText(OpenXmlCompositeElement container, StringBuilder sb)
    {
        foreach (var element in container.Elements())
        {
            switch (element)
            {
                case Paragraph paragraph:
                    var text = paragraph.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                    break;

                case Table table:
                    AppendTableText(table, sb);
                    break;
            }
        }
    }

    private static void AppendTableText(Table table, StringBuilder sb)
    {
        foreach (var row in table.Elements<TableRow>())
        {
            var cellTexts = row.Elements<TableCell>()
                .Select(cell => cell.InnerText.Trim())
                .Where(t => t.Length > 0);
            var line = string.Join(" | ", cellTexts);
            if (line.Length > 0)
            {
                sb.AppendLine(line);
            }
        }
    }
}
