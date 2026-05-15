using Google.Apis.Auth.OAuth2;
using Google.Cloud.TextToSpeech.V1;

namespace VinhKhanh.Services
{
    public class GoogleTtsService
    {
        private readonly IWebHostEnvironment _env;

        public GoogleTtsService(IWebHostEnvironment env)
        {
            _env = env;
        }

        private (string GoogleLanguageCode, string VoiceName) GetVoice(string languageCode)
        {
            return languageCode.ToLower() switch
            {
                "vi" => ("vi-VN", "vi-VN-Standard-A"),
                "en" => ("en-US", "en-US-Standard-C"),
                "ko" => ("ko-KR", "ko-KR-Standard-A"),
                "ja" => ("ja-JP", "ja-JP-Standard-A"),
                _ => ("vi-VN", "vi-VN-Standard-A")
            };
        }

        public bool AudioPathExists(string? audioPath)
        {
            var fullPath = ResolveLocalAudioPath(audioPath);
            return fullPath is null || File.Exists(fullPath);
        }

        public void DeleteAudioIfExists(string? audioPath)
        {
            var fullPath = ResolveLocalAudioPath(audioPath);
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
                return;

            try
            {
                File.Delete(fullPath);
            }
            catch (IOException)
            {
                // File đang bị giữ — bỏ qua, lần regen sau sẽ ghi đè bằng tên mới.
            }
            catch (UnauthorizedAccessException)
            {
                // Không có quyền — log nơi gọi xử lý nếu cần.
            }
        }

        public async Task<string> GenerateSpeechAsync(string text, string fileName, string languageCode = "vi")
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Text không được rỗng.");

            var jsonPath = Path.Combine(_env.ContentRootPath, "Credentials", "service-account.json");

            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("Không tìm thấy file service-account.json", jsonPath);

            var credential = GoogleCredential.FromFile(jsonPath);

            var client = new TextToSpeechClientBuilder
            {
                Credential = credential
            }.Build();

            var (googleLanguageCode, voiceName) = GetVoice(languageCode);

            var response = await client.SynthesizeSpeechAsync(
                new SynthesisInput
                {
                    Text = text
                },
                new VoiceSelectionParams
                {
                    LanguageCode = googleLanguageCode,
                    Name = voiceName
                },
                new AudioConfig
                {
                    AudioEncoding = AudioEncoding.Mp3
                });

            var webRoot = GetWebRoot();
            var folder = Path.Combine(webRoot, "audio");

            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fullPath = Path.Combine(folder, fileName);

            await File.WriteAllBytesAsync(fullPath, response.AudioContent.ToByteArray());

            return $"/audio/{fileName}";
        }

        private string GetWebRoot()
        {
            return _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        }

        private string? ResolveLocalAudioPath(string? audioPath)
        {
            if (string.IsNullOrWhiteSpace(audioPath))
                return null;

            if (audioPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                audioPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var normalizedPath = audioPath
                .Trim()
                .TrimStart('/')
                .Replace('/', Path.DirectorySeparatorChar);

            return Path.Combine(GetWebRoot(), normalizedPath);
        }
    }
}
