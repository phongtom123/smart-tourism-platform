using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using VinhKhanh.Services;

namespace VinhKhanh.Controllers
{
    [ApiController]
    // Use exact path to match legacy webhook URL /api/payment_webhook.php
    [Route("api/payment_webhook.php")]
    public class PayOsWebhookController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly VinhKhanh.Data.MySqlDbContext _db;
        private readonly AccessSessionService _accessSessionService;

        public PayOsWebhookController(
            IConfiguration config,
            VinhKhanh.Data.MySqlDbContext db,
            AccessSessionService accessSessionService)
        {
            _config = config;
            _db = db;
            _accessSessionService = accessSessionService;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            // read raw body
            string raw;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                raw = await reader.ReadToEndAsync();
            }

            // log request
            try
            {
                var logDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
                if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                await System.IO.File.AppendAllTextAsync(Path.Combine(logDir, "payos_webhook.log"), DateTime.UtcNow.ToString("o") + " " + Request.Method + " " + Request.Path + "\n" + raw + "\n---\n");
            }
            catch { }

            // parse JSON once, then detect whether this is a validation ping,
            // package payment callback, or store invoice callback.
            string? orderId = null;
            string? packageRef = null;
            string? transactionId = null;
            string? transactionDescription = null;
            DateTime? paidAt = null;
            decimal amount = 0;
            string? bodySignature = null;
            bool hasTransactionalFields = false;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (root.TryGetProperty("signature", out var rootSig) && rootSig.ValueKind == System.Text.Json.JsonValueKind.String)
                    bodySignature = rootSig.GetString();

                if (root.TryGetProperty("orderId", out var o)) orderId = o.GetString();
                if (root.TryGetProperty("invoiceId", out var inv)) orderId = orderId ?? inv.GetString();
                if (root.TryGetProperty("transactionId", out var t)) orderId = orderId ?? t.GetString();
                if (root.TryGetProperty("amount", out var a) && a.TryGetDecimal(out var aval)) amount = aval;
                if (root.TryGetProperty("total", out var t2) && amount == 0 && t2.TryGetDecimal(out var tval)) amount = tval;

                if (root.TryGetProperty("description", out var rootDesc) && rootDesc.ValueKind == System.Text.Json.JsonValueKind.String)
                    transactionDescription = rootDesc.GetString();

                if (root.TryGetProperty("reference", out var rootRef) && rootRef.ValueKind == System.Text.Json.JsonValueKind.String)
                    transactionId = rootRef.GetString();

                if (root.TryGetProperty("data", out var data) && data.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    hasTransactionalFields = true;

                    if (data.TryGetProperty("description", out var dataDesc) && dataDesc.ValueKind == System.Text.Json.JsonValueKind.String)
                        transactionDescription = dataDesc.GetString() ?? transactionDescription;

                    if (data.TryGetProperty("reference", out var dataRef) && dataRef.ValueKind == System.Text.Json.JsonValueKind.String)
                        transactionId = dataRef.GetString() ?? transactionId;

                    if (data.TryGetProperty("paymentLinkId", out var linkId) && linkId.ValueKind == System.Text.Json.JsonValueKind.String)
                        transactionId = transactionId ?? linkId.GetString();

                    if (data.TryGetProperty("orderCode", out var orderCode))
                        orderId = orderId ?? orderCode.GetRawText().Trim('"');

                    if (data.TryGetProperty("amount", out var dataAmount) && amount == 0 && dataAmount.TryGetDecimal(out var dataAmountValue))
                        amount = dataAmountValue;

                    if (data.TryGetProperty("transactionDateTime", out var txTime) && txTime.ValueKind == System.Text.Json.JsonValueKind.String && DateTime.TryParse(txTime.GetString(), out var parsedPaidAt))
                        paidAt = parsedPaidAt;
                }

                var packageCandidate = (transactionDescription ?? string.Empty) + " " + (orderId ?? string.Empty);
                var packageMatch = System.Text.RegularExpressions.Regex.Match(packageCandidate, @"CSAT\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (packageMatch.Success)
                    packageRef = packageMatch.Value.ToUpperInvariant();

                hasTransactionalFields = hasTransactionalFields ||
                    !string.IsNullOrWhiteSpace(orderId) ||
                    !string.IsNullOrWhiteSpace(transactionDescription) ||
                    amount > 0;
            }
            catch
            {
                return BadRequest(new { success = false, message = "Invalid JSON" });
            }

            // signature verification
            var secret = _config["Payment:WebhookSecurityKey"] ?? _config["Payment:PayOS:WebhookSecurityKey"] ?? string.Empty;
            var headerSig = Request.Headers.ContainsKey("X-PAYOS-SIGNATURE")
                ? Request.Headers["X-PAYOS-SIGNATURE"].ToString()
                : (Request.Headers.ContainsKey("X-Signature") ? Request.Headers["X-Signature"].ToString() : string.Empty);

            // Accept validation probes that don't carry transactional fields.
            if (string.IsNullOrWhiteSpace(headerSig) && string.IsNullOrWhiteSpace(bodySignature) && !hasTransactionalFields)
            {
                return Ok(new { success = true, message = "validation_ping_ack" });
            }

            if (!string.IsNullOrWhiteSpace(secret) && !string.IsNullOrWhiteSpace(headerSig))
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
                var expected = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-", "").ToLowerInvariant();
                if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(headerSig.Trim().ToLowerInvariant())))
                {
                    return StatusCode(403, new { success = false, message = "Invalid signature" });
                }
            }

            // Process package-payment webhook first (CSAT...).
            if (!string.IsNullOrWhiteSpace(packageRef) && amount > 0)
            {
                var activation = await _accessSessionService.ActivatePackagePaymentFromWebhookAsync(
                    packageRef,
                    amount,
                    transactionId,
                    paidAt,
                    transactionDescription ?? "PayOS webhook");

                return Ok(new
                {
                    success = activation.Success,
                    message = activation.Message,
                    paymentReference = packageRef,
                    activated = activation.Success ? 1 : 0
                });
            }

            if (string.IsNullOrEmpty(orderId) && string.IsNullOrWhiteSpace(transactionDescription))
                return BadRequest(new { success = false, message = "Missing orderId and description" });

            // extract invoice id like HDGH123
            // PayOS puts HDGH... in data.description, not in orderId (which is the numeric orderCode)
            int? invoiceId = null;
            var candidateText = (transactionDescription ?? "") + " " + (orderId ?? "");
            var m = System.Text.RegularExpressions.Regex.Match(candidateText, @"HDGH0*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) invoiceId = int.Parse(m.Groups[1].Value);
            else if (long.TryParse(orderId, out var numericLong) && numericLong <= int.MaxValue) invoiceId = (int)numericLong;

            if (invoiceId == null) return Ok(new { success = true, message = "No invoice id found", processed = 0 });

            // update DB similar to PHP handler
            int processed = 0;
            try
            {
                await using var conn = _db.GetConnection();
                await conn.OpenAsync();

                // update invoice
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "UPDATE hoadongianhang SET trangThai = 'da_thanh_toan' WHERE idHoaDonGianHang = @id AND tongTien <= @amount AND trangThai = 'chua_thanh_toan'";
                    cmd.Parameters.Add(new MySqlParameter("@id", MySqlDbType.Int32) { Value = invoiceId.Value });
                    cmd.Parameters.Add(new MySqlParameter("@amount", MySqlDbType.Decimal) { Value = amount });
                    var affected = await cmd.ExecuteNonQueryAsync();
                    if (affected > 0) processed += (int)affected;
                }

                if (processed > 0)
                {
                    await using (var cmd2 = conn.CreateCommand())
                    {
                        cmd2.CommandText = "UPDATE yeucaugianhang SET trangThai = 'da_duyet' WHERE idGianHang = (SELECT idGianHang FROM hoadongianhang WHERE idHoaDonGianHang = @id) AND trangThai = 'cho_thanh_toan'";
                        cmd2.Parameters.Add(new MySqlParameter("@id", MySqlDbType.Int32) { Value = invoiceId.Value });
                        await cmd2.ExecuteNonQueryAsync();
                    }

                    await using (var cmd3 = conn.CreateCommand())
                    {
                        cmd3.CommandText = "UPDATE gianhang SET tinhTrang = 'dang_hoat_dong' WHERE idGianHang = (SELECT idGianHang FROM hoadongianhang WHERE idHoaDonGianHang = @id)";
                        cmd3.Parameters.Add(new MySqlParameter("@id", MySqlDbType.Int32) { Value = invoiceId.Value });
                        await cmd3.ExecuteNonQueryAsync();
                    }
                }

                await conn.CloseAsync();
            }
            catch (Exception ex)
            {
                // log error
                try { await System.IO.File.AppendAllTextAsync(Path.Combine(Directory.GetCurrentDirectory(), "Logs", "payos_webhook_errors.log"), DateTime.UtcNow.ToString("o") + " " + ex.ToString() + "\n"); } catch { }
                return StatusCode(500, new { success = false, message = "DB error" });
            }

            return Ok(new { success = true, message = $"Successfully processed {processed} transaction(s)", processed = processed });
        }
    }
}
