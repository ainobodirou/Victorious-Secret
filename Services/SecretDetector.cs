using System.Text.RegularExpressions;


public class SecretDetector
{
    private readonly SourcePreprocessor _preprocessor;
    private readonly ProviderClassifier _classifier;
    public SecretDetector(SourcePreprocessor preprocessor, ProviderClassifier classifier)
    {
        _preprocessor = preprocessor;
        _classifier = classifier;
    }
    private static readonly HashSet<string> SecretKeywords = new (StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwd",
        "pwd",
        "secret",
        "token",
        "apikey",
        "api_key",
        "pk",
        "private_key",
        "privatekey",
        "credential",
        "creds",
        "cred",
        "auth"
    };

    private static bool VariableContext(string variableName)
    {
        string separated = Regex.Replace(variableName, @"([a-z0-9])([A-Z])", "$1 $2");
        string[] words = Regex.Split(separated, @"[^A-Za-z0-9]+");

        foreach(string word in words)
        {
            if (SecretKeywords.Contains(word))
            {
                return true;
            }
        }
        return false;
    }

    public IEnumerable<SecretFinding> Detect(SourceFile file)
    {
        foreach(var literal in _preprocessor.Extract(file))
        {
            int length = literal.Value.Length;
            double entropy = GetEntropy(literal.VariableName);
            bool hasContext = VariableContext(literal.VariableName);
            ProviderRule? matchedRule = _classifier.IdentifyProvider(literal.Value);

            bool isFlag = hasContext || (length >= 10 && entropy >= 3.5);
            if(!isFlag && matchedRule is null)
            {
                continue;
            }

            yield return new SecretFinding(
                FilePath: literal.FilePath,
                LineNumber: literal.LineNumber,
                CommitSha: file.CommitSha,
                Provider: matchedRule?.Provider ?? "undefined",
                CredentialType: matchedRule?.CredentialType ?? "undefined",
                Entropy: entropy,
                HasContext: hasContext,
                DetectionMethod: matchedRule is not null
                                ? "ProviderPattern" 
                                : "Generic"
            );  
        }
    }

    private static double GetEntropy(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0;
        double length = value.Length;
        return -value
            .GroupBy(c => c)
            .Sum(group =>
            {
                double prob = group.Count() / length;
                return prob * Math.Log2(prob);
            });
    }
}