using CsvProcessing.Api.Data;
using CsvProcessing.Api.Infrastructure;
using CsvProcessing.Api.Middleware;
using CsvProcessing.Api.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

var configuredApiKeys = SplitApiKeys(builder.Configuration["ApiKey:Key"]);
if (configuredApiKeys.Count == 0)
{
    throw new InvalidOperationException(
        "No API key is configured. Set 'ApiKey:Key' with user-secrets " +
        "(dotnet user-secrets set \"ApiKey:Key\" \"<value>\"), with the ApiKey__Key environment " +
        "variable, or through configuration before starting the application.");
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=App_Data/csvprocessing.db";

builder.Services.Configure<CsvOptions>(builder.Configuration.GetSection(CsvOptions.SectionName));
var csvOptions = builder.Configuration.GetSection(CsvOptions.SectionName).Get<CsvOptions>() ?? new CsvOptions();

var maxRequestBodyBytes = csvOptions.MaxFileSizeBytes + (1024 * 1024);

builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxRequestBodyBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxRequestBodyBytes);

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

builder.Services.AddSingleton<ICsvFileValidator, CsvFileValidator>();
builder.Services.AddScoped<ICsvParserService, CsvParserService>();
builder.Services.AddScoped<IAggregateCalculator, AggregateCalculator>();
builder.Services.AddScoped<IProcessingService, ProcessingService>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services
    .AddAuthentication(ApiKeyDefaults.AuthenticationScheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyDefaults.AuthenticationScheme,
        options =>
        {
            foreach (var key in configuredApiKeys)
            {
                options.Keys.Add(key);
            }
        });

builder.Services.AddAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CSV Processing API",
        Version = "v1",
        Description = "Uploads CSV files, calculates numeric column aggregates, and reports on processing history."
    });

    options.AddSecurityDefinition(ApiKeyDefaults.AuthenticationScheme, new OpenApiSecurityScheme
    {
        Description = "API key sent in the X-Api-Key header.",
        Name = ApiKeyDefaults.HeaderName,
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    options.OperationFilter<ApiKeyHeaderOperationFilter>();
});

var app = builder.Build();

if (app.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true))
{
    SqliteDatabaseLocation.EnsureDirectoryExists(connectionString);

    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "CSV Processing API v1");
    options.DocumentTitle = "CSV Processing API";
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static List<string> SplitApiKeys(string? configuredKeys) =>
    string.IsNullOrWhiteSpace(configuredKeys)
        ? []
        : configuredKeys.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

public partial class Program;
