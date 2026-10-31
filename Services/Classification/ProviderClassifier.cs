using KTrie;

public class ProviderClassifier
{
    private readonly TrieDictionary<List<ProviderRule>> _rules = new();
    public ProviderClassifier()
    {
        foreach(ProviderRule rule in RuleConfig.GetRules())
        {
            RegisterRule(rule);
        }
    }

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
    public ProviderRule? IdentifyProvider(string value)
    {
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