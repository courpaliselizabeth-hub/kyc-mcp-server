using KYCMcpServer.Api.Configuration;
using KYCMcpServer.Api.Middleware;
using KYCMcpServer.Core.Interfaces;
using KYCMcpServer.Services;
using Serilog;

// Bootstrap Serilog early so startup errors are captured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ──────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) => config
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: "logs/kyc-mcp-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30));

    // ── Configuration ─────────────────────────────────────────────────────────
    // Environment variables override appsettings.json values.
    // Supported variables are documented in .env.example.
    var settings = new AppSettings();
    builder.Configuration.GetSection("AppSettings").Bind(settings);
    OverrideFromEnv(settings);
    builder.Services.AddSingleton(settings);

    // ── HttpClient / Banking API ──────────────────────────────────────────────
    builder.Services.AddHttpClient<IBankingApiClient, BankingApiClient>(client =>
    {
        client.BaseAddress = new Uri(settings.BankingApiBaseUrl);
        client.DefaultRequestHeaders.Add("X-API-Key", settings.BankingApiKey ?? string.Empty);
        client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
    });

    // ── KYC Services ─────────────────────────────────────────────────────────
    builder.Services.AddScoped<IDocumentValidationService,   DocumentValidationService>();
    builder.Services.AddScoped<IIdentityVerificationService, IdentityVerificationService>();
    builder.Services.AddScoped<IRiskAssessmentService,       RiskAssessmentService>();
    builder.Services.AddScoped<IMcpToolHandler,              McpToolHandler>();

    // ── ASP.NET Core ─────────────────────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.PropertyNamingPolicy = null; // preserve PascalCase for MCP spec
            o.JsonSerializerOptions.DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title       = "KYC MCP Server",
            Version     = "v1",
            Description = "Model Context Protocol server for KYC customer verification. " +
                          "Exposes tools for document validation, identity verification, and risk assessment."
        });
    });

    builder.Services.AddCors(o =>
        o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    // ── App pipeline ─────────────────────────────────────────────────────────
    var app = builder.Build();

    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseSerilogRequestLogging(o =>
    {
        o.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "KYC MCP Server v1"));
    }

    app.UseCors();
    app.MapControllers();

    Log.Information("KYC MCP Server starting. Environment: {Env}", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "KYC MCP Server terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// ── Helpers ──────────────────────────────────────────────────────────────────
static void OverrideFromEnv(AppSettings s)
{
    s.BankingApiKey                  = Env("BANKING_API_KEY")                   ?? s.BankingApiKey;
    s.BankingApiBaseUrl              = Env("BANKING_API_BASE_URL")               ?? s.BankingApiBaseUrl;
    s.IdentityVerificationApiKey     = Env("IDENTITY_VERIFICATION_API_KEY")      ?? s.IdentityVerificationApiKey;
    s.IdentityVerificationApiBaseUrl = Env("IDENTITY_VERIFICATION_API_BASE_URL") ?? s.IdentityVerificationApiBaseUrl;
    s.DocumentVerificationApiKey     = Env("DOCUMENT_VERIFICATION_API_KEY")      ?? s.DocumentVerificationApiKey;
    s.DocumentVerificationApiBaseUrl = Env("DOCUMENT_VERIFICATION_API_BASE_URL") ?? s.DocumentVerificationApiBaseUrl;
    s.AmlApiKey                      = Env("AML_API_KEY")                        ?? s.AmlApiKey;
    s.AmlApiBaseUrl                  = Env("AML_API_BASE_URL")                   ?? s.AmlApiBaseUrl;

    if (int.TryParse(Env("REQUEST_TIMEOUT_SECONDS"), out var timeout))
        s.RequestTimeoutSeconds = timeout;
    if (bool.TryParse(Env("ENABLE_DETAILED_ERRORS"), out var detailed))
        s.EnableDetailedErrors = detailed;
}

static string? Env(string key) =>
    Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : null;
