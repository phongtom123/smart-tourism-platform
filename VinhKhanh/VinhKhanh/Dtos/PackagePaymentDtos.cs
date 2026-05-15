namespace VinhKhanh.Dtos
{
    public class CreatePackagePaymentRequestDto
    {
        public string Email { get; set; } = string.Empty;
        public int IdGoi { get; set; }
        public bool SendEmail { get; set; }
        public string ClientDeviceId { get; set; } = string.Empty;
    }

    public class PackagePaymentResponseDto : RegisterPackageAccessResponseDto
    {
        public string? PaymentReference { get; set; }
        public string? PaymentQrPayload { get; set; }
        public string? PaymentContent { get; set; }
        public string? CheckoutUrl { get; set; }
        public string? PaymentLinkId { get; set; }
        public decimal Amount { get; set; }
        public string? BankBin { get; set; }
        public string? BankAccountNo { get; set; }
        public string? BankAccountName { get; set; }
        public DateTime? PaymentCreatedAt { get; set; }
        public DateTime? PaymentPaidAt { get; set; }
        public string? PaymentStatus { get; set; }
    }

    public class CassoWebhookProcessResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Processed { get; set; }
        public int Activated { get; set; }
        public int Ignored { get; set; }
    }
}
