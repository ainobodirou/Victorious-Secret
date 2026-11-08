public interface ITokenStore
{
    void SaveToken(string token);
    string? GetToken();
    bool Remove();
}