public class DeviceAuthResponse
{
    public string device_code { get; set; } = string.Empty;
    public string user_code { get; set; } = string.Empty;
    public string verification_uri { get; set; } = string.Empty;
    public int expires_in { get; set; } = 0;
    public int interval { get; set; } = 0;
}