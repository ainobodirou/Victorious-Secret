using System.Text.Json.Serialization;

public record ScanRequestResult(
    string Message,
    [property: JsonPropertyName("scanJobs")] List<Guid> jobIds
)
{
    public Dictionary<string, string> NotSubmitted { get; init; } = new();
    public string? SubmissionError { get; init; }
}
