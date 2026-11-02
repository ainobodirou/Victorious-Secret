
public class UserAuthResponse
{
    public string? access_token {get; set;} = string.Empty;
    public int? expires_in {get; set;} = 0;
    public string? refresh_token {get; set;} = string.Empty;
    public int? refresh_token_expires_in {get; set;} = 0;

    public string? scope {get; set;} = string.Empty;
    public string? token_type {get; set;} = "bearer";
    public string? error { get; set; }
    public int? interval { get; set; }

}