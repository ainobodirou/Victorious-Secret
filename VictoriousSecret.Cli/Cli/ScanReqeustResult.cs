using System.Text.Json.Serialization;

public record ScanRequestResult(
    string Message,
    [property: JsonPropertyName("scanJobs")] List<Guid> jobIds
);