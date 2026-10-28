
public record SecretCandidate(
    ExtractedLiteral Literal,
    bool SecretContext,
    double Entropy,
    int Length

);