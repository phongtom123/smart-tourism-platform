namespace VinhKhanh.Dtos
{
    public class ActivateAccessTokenRequestDto
    {
        public string AccessToken { get; set; } = string.Empty;
        public string ClientDeviceId { get; set; } = string.Empty;
        public string QrRaw { get; set; } = string.Empty;
    }
}
