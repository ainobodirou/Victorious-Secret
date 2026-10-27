namespace VictoriousSecret.Test;

    public class MockSecrets
{
    // Synthetic values — not real credentials!
    private string githubToken = "ghp_FAKE_TEST_TOKEN_123456789";
    private string apiKey = "K8mP2xQ9vR4tN7bY3zL6";
    private string databasePassword = "TestPassword123!";

    // Ordinary values that should also be extracted
    private string environment = "development";
    private string applicationName = "VictoriousSecret";

    // Should NOT be extracted by our current regex
    private int timeout = 30;
    private bool enabled = true;
}
