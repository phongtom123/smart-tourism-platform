namespace MauiApp1.Services;

public class GoogleServiceAccountJsonProvider
{
    private const string AssetName = "service-account.json";

    public async Task<string> GetJsonAsync()
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync(AssetName);
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}
