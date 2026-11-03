using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Ecosys.Domain.Entities;
using UglyToad.PdfPig;

namespace Ecosys.Infrastructure.Services;

public interface IKnowledgeDocumentProcessingService
{
    Task<KnowledgeDocumentProcessingResult> ProcessAsync(Stream stream, string fileName, string fileType, CancellationToken cancellationToken);
}

public sealed record KnowledgeDocumentChunkDraft(int ChunkOrder, string ChunkText, int? PageNumber, string? SectionTitle);
public sealed record KnowledgeDocumentProcessingResult(string ExtractedText, IReadOnlyCollection<KnowledgeDocumentChunkDraft> Chunks);

public sealed class KnowledgeDocumentProcessingService : IKnowledgeDocumentProcessingService
{
    public async Task<KnowledgeDocumentProcessingResult> ProcessAsync(Stream stream, string fileName, string fileType, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".txt" or ".md" => await ProcessTextAsync(memory, cancellationToken),
            ".docx" => ProcessDocx(memory),
            ".pdf" => ProcessPdf(memory),
            _ => new KnowledgeDocumentProcessingResult(string.Empty, [])
        };
    }

    private static async Task<KnowledgeDocumentProcessingResult> ProcessTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);
        return new KnowledgeDocumentProcessingResult(text, Chunk(text, null));
    }

    private static KnowledgeDocumentProcessingResult ProcessDocx(Stream stream)
    {
        stream.Position = 0;
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var entry = archive.GetEntry("word/document.xml");
        if (entry is null)
        {
            return new KnowledgeDocumentProcessingResult(string.Empty, []);
        }

        using var entryStream = entry.Open();
        var document = XDocument.Load(entryStream);
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var paragraphs = document
            .Descendants(word + "p")
            .Select(p => string.Concat(p.Descendants(word + "t").Select(t => t.Value)).Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
        var text = string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        return new KnowledgeDocumentProcessingResult(text, Chunk(text, null));
    }

    private static KnowledgeDocumentProcessingResult ProcessPdf(Stream stream)
    {
        stream.Position = 0;
        using var document = PdfDocument.Open(stream);
        var pages = document.GetPages().ToList();
        var combined = new StringBuilder();
        var chunks = new List<KnowledgeDocumentChunkDraft>();
        var chunkOrder = 1;

        foreach (var page in pages)
        {
            var text = page.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (combined.Length > 0)
            {
                combined.AppendLine().AppendLine();
            }

            combined.Append(text);
            foreach (var chunk in Chunk(text, page.Number))
            {
                chunks.Add(chunk with { ChunkOrder = chunkOrder++ });
            }
        }

        return new KnowledgeDocumentProcessingResult(combined.ToString(), chunks);
    }

    private static IReadOnlyCollection<KnowledgeDocumentChunkDraft> Chunk(string text, int? pageNumber)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        const int chunkSize = 1200;
        const int overlap = 200;
        var normalized = Regex.Replace(text, "\\s+", " ").Trim();
        var results = new List<KnowledgeDocumentChunkDraft>();
        var index = 0;
        var chunkOrder = 1;

        while (index < normalized.Length)
        {
            var length = Math.Min(chunkSize, normalized.Length - index);
            var chunkText = normalized.Substring(index, length).Trim();
            if (!string.IsNullOrWhiteSpace(chunkText))
            {
                results.Add(new KnowledgeDocumentChunkDraft(chunkOrder++, chunkText, pageNumber, null));
            }

            if (index + length >= normalized.Length)
            {
                break;
            }

            index += Math.Max(1, chunkSize - overlap);
        }

        return results;
    }
}
