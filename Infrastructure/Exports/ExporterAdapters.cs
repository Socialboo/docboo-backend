using Application.Exports;
using Domain.Assets;
using Domain.Documents;
using Exporter.Docx;
using Exporter.Pdf;

namespace Infrastructure.Exports;

/// <summary>Application port → Open XML generator.</summary>
public sealed class DocxGeneratorAdapter(DocxDocumentExporter exporter) : IDocumentGenerator
{
    public GeneratedDocument GenerateDocx(DocumentModel document, IReadOnlyDictionary<string, ImageData> images)
    {
        var result = exporter.Export(document, images);
        return new GeneratedDocument(result.Content, result.Warnings);
    }
}

/// <summary>Application port → LibreOffice headless.</summary>
public sealed class LibreOfficePdfConverterAdapter(LibreOfficeConverter converter) : IPdfConverter
{
    public bool IsAvailable => converter.IsAvailable;

    public async Task<byte[]> ConvertAsync(byte[] docx, CancellationToken ct)
    {
        try
        {
            return await converter.ConvertDocxToPdfAsync(docx, ct);
        }
        catch (PdfConversionException ex)
        {
            throw new ExportFailedException(ex.Message, ex);
        }
    }
}
