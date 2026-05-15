using Microsoft.AspNetCore.HttpOverrides;
using VinhKhanh.Data;
using VinhKhanh.Middleware;
using VinhKhanh.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<MySqlDbContext>();
builder.Services.AddScoped<GianHangService>();
builder.Services.AddScoped<MonAnService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AccountAccessService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<OwnerService>();
builder.Services.AddScoped<StoreManagementService>();
builder.Services.AddScoped<StoreRequestService>();
builder.Services.AddScoped<DeviceService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<AccessSessionService>();
builder.Services.AddScoped<PackageAccessEmailService>();
builder.Services.AddSingleton<VietQrPayloadBuilder>();
builder.Services.AddSingleton<GoogleTtsService>();
builder.Services.AddHttpClient<GoogleDirectionsService>();

builder.Services.AddSingleton<DeviceTouchQueue>();
builder.Services.AddHostedService<DeviceTouchWorker>();

builder.Services.AddSingleton<PoiVisitQueue>();
builder.Services.AddHostedService<PoiVisitWorker>();

builder.Services.AddScoped<TourService>();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedHost |
                       ForwardedHeaders.XForwardedProto,
    ForwardLimit = null,
    RequireHeaderSymmetry = false,
    KnownIPNetworks = { },
    KnownProxies = { }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var configuredUrls = app.Configuration["urls"] ?? app.Configuration["ASPNETCORE_URLS"];
var hasHttpsEndpoint = !string.IsNullOrWhiteSpace(configuredUrls) &&
                       configuredUrls
                           .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                           .Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

// Keep HTTP-only launches working while still redirecting when an HTTPS
// endpoint is actually configured.
if (hasHttpsEndpoint)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAuthorization();

app.UseMiddleware<DeviceActivityMiddleware>();

app.MapControllers();

app.Run();
