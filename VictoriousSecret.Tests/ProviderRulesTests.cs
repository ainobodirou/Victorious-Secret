public class ProviderRulesTests
{
    [Theory]
    [InlineData("GitLab", "Personal Access Token", "glpat-", "abcDE123_-abcDE123_-", 20, 20)]
    [InlineData("Linear", "API Key", "lin_api_", "aB01234567", 40, 40)]
    [InlineData("npm", "Access Token", "npm_", "aB0123456", 36, 36)]
    [InlineData("Postman", "API Key", "PMAK-", "aB0123456789deadBEEF0123-Af0123456789abcdef0123456789abcdef", 59, 59)]
    [InlineData("RubyGems", "API Token", "rubygems_", "abcdef012345", 48, 48)]
    [InlineData("Sentry", "User Auth Token", "sntryu_", "abcdef0123456789", 64, 64)]
    [InlineData("Stripe", "Test-Mode Secret Key", "sk_test_", "aB01234567", 10, 99)]
    [InlineData("Shippo", "Test-Mode API Token", "shippo_test_", "aBcDeF0123", 40, 40)]
    public void RecognizesDistinctTokenFormats(string provider, string type, string prefix,
        string sample, int minLength, int maxLength)
    {
        var rules = RuleConfig.GetRules().ToArray();
        var classifier = new ProviderClassifier();
        var detector = new SecretDetector(new SourcePreprocessor(), classifier);

        foreach (int length in new[] { minLength, maxLength }.Distinct())
        {
            string payload = string.Concat(Enumerable.Repeat(sample, (length + sample.Length - 1) / sample.Length))[..length];
            string token = prefix + payload;

            var match = Assert.Single(rules, r => r.Pattern.IsMatch(token));
            Assert.Equal(provider, match.Provider);
            Assert.Equal(type, match.CredentialType);
            Assert.Equal(prefix, match.Prefix);

            var file = new SourceFile("settings.py", $"value = \"{token}\"", "test-commit");
            var finding = Assert.Single(detector.Detect(file));
            Assert.Equal("ProviderPattern", finding.DetectionMethod);
            Assert.Equal(provider, finding.Provider);
            Assert.Equal(type, finding.CredentialType);

            Assert.Null(classifier.IdentifyProvider(token[..^1] + "!"));
            if (length == minLength)
                Assert.Null(classifier.IdentifyProvider(token[..^1]));
            if (length == maxLength)
                Assert.Null(classifier.IdentifyProvider(token + "a"));
        }

        if (provider == "Postman")
            Assert.Null(classifier.IdentifyProvider(prefix + new string('a', 59)));
    }

    [Fact]
    public void CatalogHasNoDuplicateRegistrationsOrCrossProviderCollisions()
    {
        var rules = RuleConfig.GetRules().ToArray();
        Assert.All(rules.GroupBy(r => (r.Provider, r.Prefix, r.CredentialType, Pattern: r.Pattern.ToString())),
            group => Assert.Single(group));
        Assert.All(rules.GroupBy(r => r.Pattern.ToString()),
            group => Assert.Single(group.Select(r => r.Provider).Distinct(StringComparer.OrdinalIgnoreCase)));

        foreach (var rule in rules)
        {
            Assert.DoesNotContain(rules, other =>
                !string.Equals(other.Provider, rule.Provider, StringComparison.OrdinalIgnoreCase) &&
                other.Prefix.StartsWith(rule.Prefix, StringComparison.Ordinal));
        }
    }
}
