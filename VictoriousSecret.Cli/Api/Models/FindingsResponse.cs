

public sealed record FindingsResponse
(
    List<SecretFindingDTO> Findings,
    Dictionary<Guid, string> Failed
);