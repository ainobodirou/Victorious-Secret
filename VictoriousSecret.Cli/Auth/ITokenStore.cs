using ktsu.CredentialCache;
using ktsu.CredentialCache.Storage;
using ktsu.Semantics.Strings;

public sealed class TokenStore : ITokenStore
{
    private readonly CredentialCache _cache;
    private static readonly PersonaGUID CredentialId =
    SemanticString<PersonaGUID>.Create("b7a34e62-9c81-4d26-8a50-719d8c2e4f63");

    public TokenStore()
    {
        ICredentialStore store = CredentialStoreFactory.CreateDefault("VS");
        _cache = new CredentialCache(store);
    }

    public void SaveToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _cache.AddOrReplace(CredentialId, new CredentialWithToken
        {
            Token = SemanticString<CredentialToken>.Create(token)
        });
    }
    public string? GetToken()
    {
        if (_cache.TryGet(CredentialId, out Credential? credential) && credential is CredentialWithToken token)
        {
            return token.Token.ToString();
        }
        return null;
    }

    public bool Remove()
    {
        return _cache.Remove(CredentialId);
    }

}

