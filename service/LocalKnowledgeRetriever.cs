using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace HouseholdApi.Services;

public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken ct);
}

/// <summary>Indexes Markdown, text, and PDF files from KnowledgeBase and retrieves relevant passages.</summary>
public sealed class LocalKnowledgeRetriever : IKnowledgeRetriever
{
    private const int ChunkLength = 1200;
    private const int ChunkOverlap = 180;
    private const int MaxResults = 4;
    private readonly IReadOnlyList<Passage> _passages;
    private readonly ILogger<LocalKnowledgeRetriever> _logger;

    public LocalKnowledgeRetriever(IWebHostEnvironment environment, ILogger<LocalKnowledgeRetriever> logger)
    {
        _logger = logger;
        var root = Path.Combine(environment.ContentRootPath, "KnowledgeBase");
        var passages = new List<Passage>();

        if (Directory.Exists(root))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                         .Where(path => Path.GetExtension(path).Equals(".md", StringComparison.OrdinalIgnoreCase)
                             || Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)
                             || Path.GetExtension(path).Equals(".pdf", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var text = ReadDocumentText(file);
                    var relativePath = Path.GetRelativePath(root, file);
                    foreach (var chunk in Split(text))
                        passages.Add(new Passage(relativePath, chunk, Tokenize(chunk)));
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not index knowledge file {KnowledgeFile}.", file);
                }
            }
        }
        else
        {
            logger.LogInformation("Knowledge base folder {KnowledgeBasePath} does not exist; retrieval starts empty.", root);
        }

        _passages = passages;
        logger.LogInformation("Indexed {PassageCount} knowledge passages.", passages.Count);
    }

    private static string ReadDocumentText(string file)
    {
        if (!Path.GetExtension(file).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return File.ReadAllText(file);

        using var document = PdfDocument.Open(file);
        return string.Join("\n", document.GetPages().Select(page => page.Text));
    }

    public Task<IReadOnlyList<string>> SearchAsync(string query, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var terms = Tokenize(query).Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (terms.Count == 0 || _passages.Count == 0)
            return Task.FromResult<IReadOnlyList<string>>([]);

        var matches = _passages
            .Select(passage => new
            {
                Passage = passage,
                Score = terms.Sum(term => passage.Terms.TryGetValue(term, out var count) ? Math.Min(count, 3) : 0)
            })
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Passage.Source, StringComparer.OrdinalIgnoreCase)
            .Take(MaxResults)
            .ToArray();

        if (matches.Length > 0)
        {
            var matchedSources = matches
                .Select(result => result.Passage.Source)
                .Distinct(StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation("RAG retrieved {PassageCount} passages from: {Sources}", matches.Length, string.Join(", ", matchedSources));
        }
        else
        {
            _logger.LogInformation("RAG found no matching passages for the latest question.");
        }

        var results = matches
            .Select(result => $"Source: {result.Passage.Source}\n{result.Passage.Text}")
            .ToArray();
        return Task.FromResult<IReadOnlyList<string>>(results);
    }

    private static IEnumerable<string> Split(string text)
    {
        var normalized = Regex.Replace(text, @"\s+", " ").Trim();
        for (var start = 0; start < normalized.Length; start += ChunkLength - ChunkOverlap)
        {
            var length = Math.Min(ChunkLength, normalized.Length - start);
            var chunk = normalized.Substring(start, length).Trim();
            if (chunk.Length > 0)
                yield return chunk;
            if (start + length >= normalized.Length)
                yield break;
        }
    }

    private static Dictionary<string, int> Tokenize(string text)
    {
        return Regex.Matches(text.ToLowerInvariant(), @"[\p{L}\p{N}]{2,}")
            .Select(match => match.Value)
            .GroupBy(token => token, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
    }

    private sealed record Passage(string Source, string Text, Dictionary<string, int> Terms);
}
