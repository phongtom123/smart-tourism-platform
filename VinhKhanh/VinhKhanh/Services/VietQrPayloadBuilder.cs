using System.Globalization;
using System.Text;

namespace VinhKhanh.Services
{
    public sealed class VietQrPayloadBuilder
    {
        public string BuildPayload(
            string bankBin,
            string accountNo,
            string accountName,
            decimal amount,
            string addInfo)
        {
            var normalizedBankBin = KeepDigits(bankBin);
            var normalizedAccountNo = KeepDigits(accountNo);
            var normalizedAccountName = NormalizeText(accountName, maxLength: 25);
            var normalizedAddInfo = NormalizeText(addInfo, maxLength: 25);

            if (normalizedBankBin.Length != 6)
                throw new InvalidOperationException("Payment:VietQr:BankBin phai la ma BIN ngan hang 6 chu so.");

            if (normalizedAccountNo.Length < 6 || normalizedAccountNo.Length > 19)
                throw new InvalidOperationException("Payment:VietQr:BankAccountNo phai tu 6 den 19 chu so.");

            if (amount <= 0)
                throw new InvalidOperationException("So tien thanh toan phai lon hon 0.");

            var roundedAmount = decimal.Truncate(amount).ToString("0", CultureInfo.InvariantCulture);
            var beneficiaryAccount = Tlv("00", normalizedBankBin) + Tlv("01", normalizedAccountNo);
            var merchantAccountInfo =
                Tlv("00", "A000000727") +
                Tlv("01", beneficiaryAccount) +
                Tlv("02", "QRIBFTTA");

            var payloadWithoutCrc =
                Tlv("00", "01") +
                Tlv("01", "12") +
                Tlv("38", merchantAccountInfo) +
                Tlv("53", "704") +
                Tlv("54", roundedAmount) +
                Tlv("58", "VN") +
                (string.IsNullOrWhiteSpace(normalizedAccountName) ? string.Empty : Tlv("59", normalizedAccountName)) +
                Tlv("62", Tlv("08", normalizedAddInfo)) +
                "6304";

            return payloadWithoutCrc + ComputeCrc16(payloadWithoutCrc);
        }

        public static string NormalizePaymentContent(int invoiceId, int packageId, string clientDeviceId)
        {
            var suffix = KeepAlphaNumeric(clientDeviceId);
            if (suffix.Length > 6)
                suffix = suffix[^6..];

            var content = $"CSAT{invoiceId} G{packageId} D{suffix}";
            return NormalizeText(content, maxLength: 25);
        }

        private static string Tlv(string id, string value)
        {
            var length = Encoding.ASCII.GetByteCount(value);
            if (length > 99)
                throw new InvalidOperationException($"Gia tri VietQR tag {id} vuot qua 99 byte.");

            return $"{id}{length:00}{value}";
        }

        private static string KeepDigits(string value)
        {
            return new string((value ?? string.Empty).Where(char.IsDigit).ToArray());
        }

        private static string KeepAlphaNumeric(string value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsAsciiLetterOrDigit)
                .Select(char.ToUpperInvariant)
                .ToArray());
        }

        private static string NormalizeText(string value, int maxLength)
        {
            var normalized = RemoveDiacritics(value ?? string.Empty).ToUpperInvariant();
            var builder = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                if (char.IsAsciiLetterOrDigit(ch) || ch == ' ')
                    builder.Append(ch);
            }

            var compact = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
            return compact.Length <= maxLength ? compact : compact[..maxLength].TrimEnd();
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var ch in normalized)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category != UnicodeCategory.NonSpacingMark)
                    builder.Append(ch);
            }

            return builder.ToString().Normalize(NormalizationForm.FormC)
                .Replace('Đ', 'D')
                .Replace('đ', 'd');
        }

        private static string ComputeCrc16(string input)
        {
            const ushort polynomial = 0x1021;
            ushort crc = 0xFFFF;

            foreach (var b in Encoding.ASCII.GetBytes(input))
            {
                crc ^= (ushort)(b << 8);
                for (var i = 0; i < 8; i++)
                {
                    crc = (crc & 0x8000) != 0
                        ? (ushort)((crc << 1) ^ polynomial)
                        : (ushort)(crc << 1);
                }
            }

            return crc.ToString("X4", CultureInfo.InvariantCulture);
        }
    }
}
