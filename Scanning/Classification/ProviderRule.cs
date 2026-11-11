using System.Text.RegularExpressions;

public record ProviderRule
(
    string Provider,
    string Prefix,
    string CredentialType,
    Regex Pattern

);