using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using QRCoder;

namespace VinhKhanh.Services
{
    public class PackageAccessEmailService
    {
        private readonly IConfiguration _config;

        public PackageAccessEmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<(bool Sent, string Message)> TrySendQrTokenEmailAsync(
            string email,
            string packageName,
            string accessToken,
            string qrTokenPayload,
            DateTime expiresAtUtc)
        {
            var host = _config["Email:SmtpHost"];
            var username = _config["Email:Username"];
            var password = _config["Email:Password"];
            var fromEmail = _config["Email:FromEmail"];
            var fromName = _config["Email:FromName"] ?? "Vinh Khanh Smart Tourism";

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                return (false, "Chua cau hinh SMTP nen chua gui email tu dong.");
            }

            try
            {
                var port = _config.GetValue<int?>("Email:SmtpPort") ?? 587;
                var enableSsl = _config.GetValue<bool?>("Email:EnableSsl") ?? true;

                using var qrGenerator = new QRCodeGenerator();
                using var qrData = qrGenerator.CreateQrCode(qrTokenPayload, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new PngByteQRCode(qrData);
                var pngBytes = qrCode.GetGraphic(20);
                var expiresAtLocal = expiresAtUtc.ToLocalTime();
                var qrContentId = Guid.NewGuid().ToString("N");
                var htmlBody = $@"
<div style='font-family:Arial,sans-serif;line-height:1.5'>
  <h2>QR token dang nhap</h2>
  <p>Goi dich vu: <strong>{WebUtility.HtmlEncode(packageName)}</strong></p>
  <p>Token co hieu luc den: <strong>{expiresAtLocal:dd/MM/yyyy HH:mm}</strong></p>
  <p>Hay dung QR nay de dang nhap vao thiet bi. Token chi duoc kich hoat tren mot may dau tien quet thanh cong.</p>
  <div style='margin:16px 0;padding:14px;border:1px solid #e5e7eb;border-radius:12px;background:#f8fafc'>
    <p style='margin:0 0 8px 0'><strong>QR token dang nhap:</strong></p>
    <img alt='QR token dang nhap' style='display:block;width:240px;height:240px;border:1px solid #e5e7eb;border-radius:12px;background:#ffffff' src='cid:{qrContentId}' />
  </div>
  <p><strong>Access token:</strong> {WebUtility.HtmlEncode(accessToken)}</p>
  <p><strong>QR payload:</strong> {WebUtility.HtmlEncode(qrTokenPayload)}</p>
</div>";

                var plainTextBody = $@"
QR token dang nhap
Goi dich vu: {packageName}
Token co hieu luc den: {expiresAtLocal:dd/MM/yyyy HH:mm}
Token chi duoc kich hoat tren mot may dau tien quet thanh cong.
Access token: {accessToken}
QR payload: {qrTokenPayload}";

                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "QR token truy cap Vinh Khanh Smart Tourism"
                };
                message.SubjectEncoding = Encoding.UTF8;
                message.BodyEncoding = Encoding.UTF8;
                message.HeadersEncoding = Encoding.UTF8;

                message.To.Add(email);
                var plainView = AlternateView.CreateAlternateViewFromString(plainTextBody, Encoding.UTF8, MediaTypeNames.Text.Plain);
                plainView.TransferEncoding = TransferEncoding.Base64;
                message.AlternateViews.Add(plainView);

                var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html);
                htmlView.TransferEncoding = TransferEncoding.Base64;

                const string pngMimeType = "image/png";
                var inlineQr = new LinkedResource(new MemoryStream(pngBytes, writable: false), pngMimeType)
                {
                    ContentId = qrContentId,
                    TransferEncoding = TransferEncoding.Base64,
                    ContentType = new ContentType(pngMimeType)
                };
                inlineQr.ContentType.Name = "qr-token-dang-nhap.png";
                inlineQr.ContentLink = new Uri($"cid:{qrContentId}");
                htmlView.LinkedResources.Add(inlineQr);
                message.AlternateViews.Add(htmlView);

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(username, password)
                };

                await client.SendMailAsync(message);
                return (true, "Da gui email QR token dang nhap thanh cong.");
            }
            catch (Exception ex)
            {
                return (false, $"Gui email that bai: {ex.Message}");
            }
        }
    }
}
