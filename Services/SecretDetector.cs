using Octokit;
using Octokit.Caching;
using System.Collections.Generic;
using System.Text.RegularExpressions;


public class SecretDetector
{
    private readonly SourcePreprocessor _preprocessor;
    public SecretDetector(SourcePreprocessor preprocessor)
    {
        _preprocessor = preprocessor;
    }

    private static readonly string[] SecretKeywords =
    {
        "passwords",
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

    public IEnumerable<SecretCandidate> Detect(SourceFile file)
    {
        foreach(var literal in _preprocessor.Extract(file))
        {
            SecretCandidate candidate = new SecretCandidate(
                Literal: literal,
                SecretContext: VariableContext(literal.VariableName),
                Entropy: GetEntropy(literal.Value),
                Length: literal.Value.Length

            );

            if (!(candidate.Length >= 10 && candidate.Entropy >= 3.5))
            {
                continue;
            }
            yield return candidate;
        }


    }

    private static double GetEntropy(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

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