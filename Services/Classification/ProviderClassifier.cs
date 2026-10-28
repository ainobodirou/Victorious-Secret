using KTrie;

public class ProviderClassifier
{
    private readonly TrieDictionary<List<ProviderRule>> _rules = new();

    public void RegisterRule(ProviderRule rule)
    {
        if (_rules.TryGetValue(rule.Prefix, out var existingRules))
        {
            existingRules.Add(rule);
        }
        else
        {
            _rules.Add(rule.Prefix, new List<ProviderRule> {rule});
        }
    }
    public ProviderRule? IdentifyProvider(SecretCandidate candidate)
    {
        string value = candidate.Literal.Value;
        var match = _rules.LongestPrefixMatch(value);

        if (match is null)
            return null;
        
        List<ProviderRule> possibleRules = match.Value.Value;

        foreach (var rule in possibleRules)
        {
            if(rule.Pattern.IsMatch(value))
            return rule;
        }
        return null;
    }
}