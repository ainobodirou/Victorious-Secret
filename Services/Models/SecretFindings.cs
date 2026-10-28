
public record SecretFinding(

    string FilePath,
    int LineNumber,
    string CommitSha,
    string Provider,
    string CredentialType,
    double Entropy,
    bool HasContext,
    string DetectionMethod

);