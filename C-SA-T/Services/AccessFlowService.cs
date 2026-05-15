using Microsoft.Maui.Storage;
using MauiApp1.Models;

namespace MauiApp1.Services;

public sealed class AccessFlowService
{
    private const string AccessTokenKey = "access_token";
    private const string AccessSourceKey = "access_source";
    private const string AccessExpiryKey = "access_expires_at";
    private const string AccessEmailKey = "access_email";
    private const string AccessPackageIdKey = "access_package_id";
    private const string AccessPackageNameKey = "access_package_name";
    private const string AccessDeviceCodeKey = "access_device_code";

    private readonly ApiService _apiService;
    private readonly SQLiteService _sqliteService;
    private readonly ClientDeviceIdentityService _clientDeviceIdentityService;

    public AccessFlowService(
        ApiService apiService,
        SQLiteService sqliteService,
        ClientDeviceIdentityService clientDeviceIdentityService)
    {
        _apiService = apiService;
        _sqliteService = sqliteService;
        _clientDeviceIdentityService = clientDeviceIdentityService;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        try
        {
            var secure = await SecureStorage.Default.GetAsync(AccessTokenKey);
            if (!string.IsNullOrWhiteSpace(secure))
                return secure;
        }
        catch
        {
        }

        var legacy = Preferences.Get(AccessTokenKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(legacy))
        {
            try
            {
                await SecureStorage.Default.SetAsync(AccessTokenKey, legacy);
                Preferences.Remove(AccessTokenKey);
            }
            catch
            {
            }
            return legacy;
        }

        var cached = await GetLatestUsableCachedAccessAsync();
        if (cached is not null)
        {
            await SetAccessTokenAsync(cached.AccessToken);
            RestorePreferencesFromCache(cached);
            return cached.AccessToken;
        }

        return string.Empty;
    }

    private static async Task SetAccessTokenAsync(string token)
    {
        try
        {
            await SecureStorage.Default.SetAsync(AccessTokenKey, token);
            Preferences.Remove(AccessTokenKey);
        }
        catch
        {
            Preferences.Set(AccessTokenKey, token);
        }
    }

    private static void RemoveAccessToken()
    {
        try
        {
            SecureStorage.Default.Remove(AccessTokenKey);
        }
        catch
        {
        }
        Preferences.Remove(AccessTokenKey);
    }

    public async Task<AccessValidationState> ValidateCurrentAccessAsync()
    {
        var token = await GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return new AccessValidationState
            {
                IsValid = false,
                Message = "Chua co access token trong local."
            };
        }

        var source = Preferences.Get(AccessSourceKey, "qr");
        if (string.Equals(source, "test", StringComparison.OrdinalIgnoreCase))
        {
            var expiresAt = ReadExpiryFromPreferences();
            if (expiresAt.HasValue && expiresAt.Value > DateTime.UtcNow)
            {
                return new AccessValidationState
                {
                    IsValid = true,
                    Message = "Goi demo con hieu luc.",
                    Source = source,
                    ExpiresAtUtc = expiresAt
                };
            }

            await ClearAccessAsync();
            return new AccessValidationState
            {
                IsValid = false,
                Message = "Goi demo da het han."
            };
        }

        var apiResult = await _apiService.ValidateAccessAsync(token);
        if (apiResult.IsValid)
        {
            var resolvedSource = string.Equals(source, "package", StringComparison.OrdinalIgnoreCase) ? "package" : "qr";
            await SaveAccessSnapshotAsync(
                token,
                resolvedSource,
                apiResult.BatDauLuc,
                apiResult.HetHanLuc,
                apiResult.MaThietBi,
                status: apiResult.TrangThai);

            return new AccessValidationState
            {
                IsValid = true,
                Message = apiResult.Message,
                Source = resolvedSource,
                DeviceCode = apiResult.MaThietBi,
                ExpiresAtUtc = apiResult.HetHanLuc?.ToUniversalTime(),
                LastValidatedAtUtc = DateTime.UtcNow
            };
        }

        if (apiResult.IsNetworkError)
        {
            var cachedState = await TryBuildCachedValidationStateAsync(token, apiResult.Message);
            if (cachedState is not null)
                return cachedState;

            return new AccessValidationState
            {
                IsValid = false,
                Message = apiResult.Message
            };
        }

        await ClearAccessAsync();
        return new AccessValidationState
        {
            IsValid = false,
            Message = apiResult.Message
        };
    }

    public async Task<PackageAccessActivationState> RegisterPackageAccessBypassAsync(string email, int packageId, bool sendEmail = true)
    {
        var result = await _apiService.RegisterPackageAccessAsync(email, packageId, bypassPayment: true, sendEmail: sendEmail);
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            return new PackageAccessActivationState
            {
                Success = false,
                Message = result.Message,
                EmailSent = result.EmailSent,
                EmailStatusMessage = result.EmailStatusMessage
            };
        }

        await SaveAccessSnapshotAsync(
            result.AccessToken,
            "package",
            result.BatDauLuc,
            result.HetHanLuc,
            result.MaThietBi,
            result.Email,
            result.IdGoi?.ToString(),
            result.TenGoi,
            result.TrangThai);

        var validation = await _apiService.ValidateAccessAsync(result.AccessToken);
        if (!validation.IsValid)
        {
            if (validation.IsNetworkError && IsFuture(result.HetHanLuc))
            {
                return new PackageAccessActivationState
                {
                    Success = true,
                    Message = result.Message,
                    AccessToken = result.AccessToken,
                    QrTokenPayload = result.QrTokenPayload,
                    Email = result.Email ?? email.Trim(),
                    PackageId = result.IdGoi?.ToString() ?? packageId.ToString(),
                    PackageName = result.TenGoi ?? string.Empty,
                    ExpiresAtUtc = result.HetHanLuc?.ToUniversalTime(),
                    EmailSent = result.EmailSent,
                    EmailStatusMessage = result.EmailStatusMessage
                };
            }

            await ClearAccessAsync();
            return new PackageAccessActivationState
            {
                Success = false,
                Message = validation.Message,
                EmailSent = result.EmailSent,
                EmailStatusMessage = result.EmailStatusMessage
            };
        }

        await SaveAccessSnapshotAsync(
            result.AccessToken,
            "package",
            validation.BatDauLuc ?? result.BatDauLuc,
            validation.HetHanLuc ?? result.HetHanLuc,
            validation.MaThietBi ?? result.MaThietBi,
            result.Email,
            result.IdGoi?.ToString(),
            result.TenGoi,
            validation.TrangThai ?? result.TrangThai);

        return new PackageAccessActivationState
        {
            Success = true,
            Message = result.Message,
            AccessToken = result.AccessToken,
            QrTokenPayload = result.QrTokenPayload,
            Email = result.Email ?? email.Trim(),
            PackageId = result.IdGoi?.ToString() ?? packageId.ToString(),
            PackageName = result.TenGoi ?? string.Empty,
            ExpiresAtUtc = validation.HetHanLuc?.ToUniversalTime() ?? result.HetHanLuc?.ToUniversalTime(),
            EmailSent = result.EmailSent,
            EmailStatusMessage = result.EmailStatusMessage
        };
    }

    public Task<PackagePaymentResult> CreatePackagePaymentAsync(string email, int packageId, bool sendEmail = false)
    {
        return _apiService.CreatePackagePaymentAsync(email, packageId, sendEmail);
    }

    public async Task<PackageAccessActivationState> ConfirmPackagePaymentAsync(string paymentReference, string email, int packageId)
    {
        var result = await _apiService.GetPackagePaymentStatusAsync(paymentReference);
        if (!result.Success)
        {
            return new PackageAccessActivationState
            {
                Success = false,
                Message = result.Message
            };
        }

        if (string.IsNullOrWhiteSpace(result.AccessToken))
        {
            return new PackageAccessActivationState
            {
                Success = false,
                Message = result.Message,
                Email = result.Email ?? email.Trim(),
                PackageId = result.IdGoi?.ToString() ?? packageId.ToString(),
                PackageName = result.TenGoi ?? string.Empty
            };
        }

        await SaveAccessSnapshotAsync(
            result.AccessToken,
            "package",
            result.BatDauLuc,
            result.HetHanLuc,
            result.MaThietBi,
            result.Email,
            result.IdGoi?.ToString(),
            result.TenGoi,
            result.TrangThai);

        var validation = await _apiService.ValidateAccessAsync(result.AccessToken);
        if (!validation.IsValid && !validation.IsNetworkError)
        {
            await ClearAccessAsync();
            return new PackageAccessActivationState
            {
                Success = false,
                Message = validation.Message
            };
        }

        return new PackageAccessActivationState
        {
            Success = true,
            Message = result.Message,
            AccessToken = result.AccessToken,
            QrTokenPayload = result.QrTokenPayload,
            Email = result.Email ?? email.Trim(),
            PackageId = result.IdGoi?.ToString() ?? packageId.ToString(),
            PackageName = result.TenGoi ?? string.Empty,
            ExpiresAtUtc = validation.HetHanLuc?.ToUniversalTime() ?? result.HetHanLuc?.ToUniversalTime(),
            EmailSent = result.EmailSent,
            EmailStatusMessage = result.EmailStatusMessage
        };
    }

    public async Task SaveQrAccessAsync(QrScanResult result)
    {
        if (!result.Success || string.IsNullOrWhiteSpace(result.AccessToken))
            return;

        await SaveAccessSnapshotAsync(
            result.AccessToken,
            result.IdGoi.HasValue ? "package" : "qr",
            result.BatDauLuc,
            result.HetHanLuc,
            result.MaThietBi,
            result.Email,
            result.IdGoi?.ToString(),
            result.TenGoi,
            result.TrangThai);
    }

    public async Task<TestPackageActivationResult> ActivateTestPackageAsync(string email, string packageId, string packageName, int durationDays)
    {
        var accessToken = $"TEST-{Guid.NewGuid():N}".ToUpperInvariant();
        var expiresAtUtc = DateTime.UtcNow.AddDays(durationDays);

        await SetAccessTokenAsync(accessToken);
        Preferences.Set(AccessSourceKey, "test");
        Preferences.Set(AccessExpiryKey, expiresAtUtc.ToString("O"));
        Preferences.Set(AccessEmailKey, email.Trim());
        Preferences.Set(AccessPackageIdKey, packageId);
        Preferences.Set(AccessPackageNameKey, packageName);

        await SaveAccessSnapshotAsync(
            accessToken,
            "test",
            DateTime.UtcNow,
            expiresAtUtc,
            _clientDeviceIdentityService.GetOrCreateClientDeviceId(),
            email.Trim(),
            packageId,
            packageName,
            "hieu_luc");

        return new TestPackageActivationResult
        {
            AccessToken = accessToken,
            Email = email.Trim(),
            PackageId = packageId,
            PackageName = packageName,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    public async Task ClearAccessAsync()
    {
        RemoveAccessToken();
        Preferences.Remove(AccessSourceKey);
        Preferences.Remove(AccessExpiryKey);
        Preferences.Remove(AccessEmailKey);
        Preferences.Remove(AccessPackageIdKey);
        Preferences.Remove(AccessPackageNameKey);
        Preferences.Remove(AccessDeviceCodeKey);
        await _sqliteService.ClearAccessTokenCachesAsync();
    }

    public async Task<AccessSummary> GetCurrentSummaryAsync()
    {
        return new AccessSummary
        {
            AccessToken = await GetAccessTokenAsync(),
            Source = Preferences.Get(AccessSourceKey, string.Empty),
            Email = Preferences.Get(AccessEmailKey, string.Empty),
            PackageId = Preferences.Get(AccessPackageIdKey, string.Empty),
            PackageName = Preferences.Get(AccessPackageNameKey, string.Empty),
            DeviceCode = Preferences.Get(AccessDeviceCodeKey, string.Empty),
            ExpiresAtUtc = ReadExpiryFromPreferences()
        };
    }

    private static DateTime? ReadExpiryFromPreferences()
    {
        var raw = Preferences.Get(AccessExpiryKey, string.Empty);
        if (DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
            return parsed.ToUniversalTime();

        return null;
    }

    private async Task SaveAccessSnapshotAsync(
        string accessToken,
        string source,
        DateTime? startedAt,
        DateTime? expiresAt,
        string? deviceCode,
        string? email = null,
        string? packageId = null,
        string? packageName = null,
        string? status = null)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return;

        var resolvedSource = string.IsNullOrWhiteSpace(source) ? "qr" : source;
        var resolvedEmail = FirstNonEmpty(email, Preferences.Get(AccessEmailKey, string.Empty));
        var resolvedPackageId = FirstNonEmpty(packageId, Preferences.Get(AccessPackageIdKey, string.Empty));
        var resolvedPackageName = FirstNonEmpty(packageName, Preferences.Get(AccessPackageNameKey, string.Empty));
        var resolvedDeviceCode = FirstNonEmpty(deviceCode, Preferences.Get(AccessDeviceCodeKey, string.Empty));
        var resolvedStatus = string.IsNullOrWhiteSpace(status) ? "hieu_luc" : status.Trim();
        var now = DateTime.UtcNow;

        await SetAccessTokenAsync(accessToken);
        Preferences.Set(AccessSourceKey, resolvedSource);

        if (expiresAt.HasValue)
            Preferences.Set(AccessExpiryKey, expiresAt.Value.ToUniversalTime().ToString("O"));
        if (!string.IsNullOrWhiteSpace(resolvedEmail))
            Preferences.Set(AccessEmailKey, resolvedEmail);
        if (!string.IsNullOrWhiteSpace(resolvedPackageId))
            Preferences.Set(AccessPackageIdKey, resolvedPackageId);
        if (!string.IsNullOrWhiteSpace(resolvedPackageName))
            Preferences.Set(AccessPackageNameKey, resolvedPackageName);
        if (!string.IsNullOrWhiteSpace(resolvedDeviceCode))
            Preferences.Set(AccessDeviceCodeKey, resolvedDeviceCode);

        await _sqliteService.UpsertAccessTokenCacheAsync(new AccessTokenCacheEntry
        {
            AccessToken = accessToken,
            Source = resolvedSource,
            Email = resolvedEmail,
            PackageId = resolvedPackageId,
            PackageName = resolvedPackageName,
            DeviceCode = resolvedDeviceCode,
            ClientDeviceId = _clientDeviceIdentityService.GetOrCreateClientDeviceId(),
            StartedAtUtc = startedAt?.ToUniversalTime(),
            ExpiresAtUtc = expiresAt?.ToUniversalTime(),
            LastStatus = resolvedStatus,
            LastValidatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }

    private async Task<AccessValidationState?> TryBuildCachedValidationStateAsync(string accessToken, string reason)
    {
        var cached = await GetUsableCachedAccessAsync(accessToken);
        if (cached is null)
            return null;

        RestorePreferencesFromCache(cached);
        return new AccessValidationState
        {
            IsValid = true,
            Message = $"{reason} Token cache con han den {cached.ExpiresAtUtc!.Value.ToLocalTime():dd/MM/yyyy HH:mm}.",
            Source = cached.Source,
            DeviceCode = cached.DeviceCode,
            ExpiresAtUtc = cached.ExpiresAtUtc,
            LastValidatedAtUtc = cached.LastValidatedAtUtc,
            UsedOfflineCache = true
        };
    }

    private async Task<AccessTokenCacheEntry?> GetUsableCachedAccessAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        try
        {
            var cached = await _sqliteService.GetAccessTokenCacheAsync(accessToken);
            return IsUsableCachedAccess(cached) ? cached : null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccessFlow] SQLite token cache read error: {ex.Message}");
            return null;
        }
    }

    private async Task<AccessTokenCacheEntry?> GetLatestUsableCachedAccessAsync()
    {
        try
        {
            return await _sqliteService.GetLatestValidAccessTokenCacheAsync(
                _clientDeviceIdentityService.GetOrCreateClientDeviceId());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccessFlow] SQLite latest token cache read error: {ex.Message}");
            return null;
        }
    }

    private bool IsUsableCachedAccess(AccessTokenCacheEntry? cached)
    {
        if (cached is null)
            return false;

        if (!cached.ExpiresAtUtc.HasValue || cached.ExpiresAtUtc.Value <= DateTime.UtcNow)
            return false;

        if (!string.Equals(cached.LastStatus, "hieu_luc", StringComparison.OrdinalIgnoreCase))
            return false;

        var clientDeviceId = _clientDeviceIdentityService.GetOrCreateClientDeviceId();
        return string.Equals(cached.ClientDeviceId, clientDeviceId, StringComparison.OrdinalIgnoreCase);
    }

    private static void RestorePreferencesFromCache(AccessTokenCacheEntry cached)
    {
        if (!string.IsNullOrWhiteSpace(cached.Source))
            Preferences.Set(AccessSourceKey, cached.Source);
        if (cached.ExpiresAtUtc.HasValue)
            Preferences.Set(AccessExpiryKey, cached.ExpiresAtUtc.Value.ToUniversalTime().ToString("O"));
        if (!string.IsNullOrWhiteSpace(cached.Email))
            Preferences.Set(AccessEmailKey, cached.Email);
        if (!string.IsNullOrWhiteSpace(cached.PackageId))
            Preferences.Set(AccessPackageIdKey, cached.PackageId);
        if (!string.IsNullOrWhiteSpace(cached.PackageName))
            Preferences.Set(AccessPackageNameKey, cached.PackageName);
        if (!string.IsNullOrWhiteSpace(cached.DeviceCode))
            Preferences.Set(AccessDeviceCodeKey, cached.DeviceCode);
    }

    private static bool IsFuture(DateTime? value)
    {
        return value.HasValue && value.Value.ToUniversalTime() > DateTime.UtcNow;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }
}

public sealed class AccessValidationState
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? DeviceCode { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? LastValidatedAtUtc { get; set; }
    public bool UsedOfflineCache { get; set; }
}

public sealed class TestPackageActivationResult
{
    public string AccessToken { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}

public sealed class PackageAccessActivationState
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public string? QrTokenPayload { get; set; }
    public bool EmailSent { get; set; }
    public string? EmailStatusMessage { get; set; }
}

public sealed class AccessSummary
{
    public string AccessToken { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string DeviceCode { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
}
