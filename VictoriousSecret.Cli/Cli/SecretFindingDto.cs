
public sealed record SecretFindingDTO
(
    string FilePath,
    int LineNumber,
    string Provider,
    string CredentialType,
    string DetectionMethod
);