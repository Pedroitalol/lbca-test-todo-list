using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TbcaTest.Application.IoC;
using TbcaTest.CrossCutting.Configuration;
using TbcaTest.Api.Middlewares;
using TbcaTest.Infra.Contexts;
using TbcaTest.Infra.IoC;

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port) &&
    string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls($"http://*:{port}");
}

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
});

builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["AllowedHosts"] = NormalizeAllowedHosts(builder.Configuration["AllowedHosts"])
});

var databaseStartupOptions = builder.Configuration
    .GetSection(DatabaseStartupOptions.SectionName)
    .Get<DatabaseStartupOptions>()
    ?? new DatabaseStartupOptions();
var appSecurityOptions = builder.Configuration
    .GetSection(AppSecurityOptions.SectionName)
    .Get<AppSecurityOptions>()
    ?? new AppSecurityOptions();
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is required.");

if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    throw new InvalidOperationException("Jwt:Key is required.");
}

builder.Services
    .AddInfra(builder.Configuration)
    .AddApplication();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "TbcaTest API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            []
        }
    });
});

var key = Encoding.UTF8.GetBytes(jwtOptions.Key);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        var corsRules = GetCorsRules(builder.Configuration["Cors:AllowedOrigins"]);

        if (corsRules is null)
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            }

            return;
        }

        policy.SetIsOriginAllowed(origin => IsAllowedOrigin(origin, corsRules))
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = static async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            BuildOperationalErrorResponse(
                context.HttpContext,
                "Too many requests. Please slow down and try again shortly.",
                "Protect API availability and security."),
            cancellationToken);
    };
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var partitionKey = BuildRateLimitPartitionKey(httpContext);
        var permitLimit = ResolvePermitLimit(httpContext, appSecurityOptions);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});

var app = builder.Build();
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

app.UseForwardedHeaders();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.Use(async (context, next) =>
{
    var maxLimit = context.Request.Path.StartsWithSegments("/api/tasks/import", StringComparison.OrdinalIgnoreCase)
        ? appSecurityOptions.ImportMaxRequestBodySizeBytes
        : appSecurityOptions.MaxRequestBodySizeBytes;

    if (context.Request.ContentLength > maxLimit)
    {
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(BuildOperationalErrorResponse(
            context,
            "Request body is too large.",
            "Protect API availability and security."));
        return;
    }

    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseCors("DefaultCors");
app.UseHttpsRedirection();
app.UseRateLimiter();
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

if (databaseStartupOptions.ApplyMigrationsOnStartup || app.Environment.IsProduction())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TbcaTestContext>();
        startupLogger.LogInformation("Applying database migrations on startup.");
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        startupLogger.LogCritical(ex, "Application startup failed during database migration.");
        throw;
    }
}

app.MapControllers();
app.Run();

static string NormalizeAllowedHosts(string? allowedHostsValue)
{
    if (string.IsNullOrWhiteSpace(allowedHostsValue) || allowedHostsValue.Trim() == "*")
    {
        return "*";
    }

    var hosts = allowedHostsValue
        .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(NormalizeAllowedHost)
        .Where(static host => !string.IsNullOrWhiteSpace(host))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return hosts.Length == 0 ? "*" : string.Join(';', hosts);
}

static string? NormalizeAllowedHost(string value)
{
    var normalizedValue = value.Trim().TrimEnd('/');
    if (Uri.TryCreate(normalizedValue, UriKind.Absolute, out var uri))
    {
        return uri.IsDefaultPort ? uri.Host : uri.Authority;
    }

    return normalizedValue;
}

static CorsRules? GetCorsRules(string? allowedHostsValue)
{
    if (string.IsNullOrWhiteSpace(allowedHostsValue) || allowedHostsValue.Trim() == "*")
    {
        return null;
    }

    var rules = allowedHostsValue
        .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(NormalizeCorsRule)
        .Where(static rule => rule is not null)
        .Cast<CorsRule>()
        .Distinct()
        .ToArray();

    return rules.Length == 0 ? null : new CorsRules(rules);
}

static CorsRule? NormalizeCorsRule(string value)
{
    var normalizedValue = value.Trim().TrimEnd('/');
    if (TryParseCorsOrigin(normalizedValue, out var exactRule))
    {
        return exactRule;
    }

    if (TryParseCorsWildcardOrigin(normalizedValue, out var wildcardRule))
    {
        return wildcardRule;
    }

    if (TryParseCorsOrigin($"https://{normalizedValue}", out var httpsRule))
    {
        return httpsRule;
    }

    return TryParseCorsOrigin($"http://{normalizedValue}", out var httpRule) ? httpRule : null;
}

static bool TryParseCorsOrigin(string value, out CorsRule rule)
{
    rule = default;
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        return false;
    }

    rule = new CorsRule(uri.Scheme, uri.Host, uri.IsDefaultPort ? null : uri.Port, false);
    return true;
}

static bool TryParseCorsWildcardOrigin(string value, out CorsRule rule)
{
    rule = default;
    if (!value.StartsWith("https://*.", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("http://*.", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var schemeSeparatorIndex = value.IndexOf("://", StringComparison.Ordinal);
    var scheme = value[..schemeSeparatorIndex].ToLowerInvariant();
    var rootHost = value[(schemeSeparatorIndex + 6)..];
    if (rootHost.Contains('/') || rootHost.Contains(':') || rootHost.StartsWith('.'))
    {
        return false;
    }

    rule = new CorsRule(scheme, rootHost, null, true);
    return true;
}

static bool IsAllowedOrigin(string origin, CorsRules corsRules)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) ||
        (originUri.Scheme != Uri.UriSchemeHttp && originUri.Scheme != Uri.UriSchemeHttps))
    {
        return false;
    }

    foreach (var rule in corsRules.Items)
    {
        if (!string.Equals(originUri.Scheme, rule.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (rule.AllowSubdomains && originUri.IsDefaultPort &&
            originUri.Host.EndsWith($".{rule.Host}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        int? originPort = originUri.IsDefaultPort ? null : originUri.Port;
        if (!rule.AllowSubdomains &&
            string.Equals(originUri.Host, rule.Host, StringComparison.OrdinalIgnoreCase) &&
            originPort == rule.Port)
        {
            return true;
        }
    }

    return false;
}

static string BuildRateLimitPartitionKey(HttpContext context)
{
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var pathGroup = ResolveRateLimitPathGroup(context.Request.Path);
    return $"{pathGroup}:{remoteIp}";
}

static string ResolveRateLimitPathGroup(PathString path)
{
    if (path.StartsWithSegments("/webhooks", StringComparison.OrdinalIgnoreCase))
    {
        return "webhooks";
    }

    if (path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase))
    {
        return "auth";
    }

    return "default";
}

static int ResolvePermitLimit(HttpContext context, AppSecurityOptions appSecurityOptions)
{
    var pathGroup = ResolveRateLimitPathGroup(context.Request.Path);
    return pathGroup switch
    {
        "webhooks" => Math.Max(1, appSecurityOptions.RateLimiting.WebhookRequestsPerSecond),
        "auth" => Math.Max(1, appSecurityOptions.RateLimiting.AuthRequestsPerSecond),
        _ => Math.Max(1, appSecurityOptions.RateLimiting.DefaultRequestsPerSecond)
    };
}

static object BuildOperationalErrorResponse(HttpContext context, string message, string purpose)
    => new
    {
        errors = new[] { message },
        meta = new
        {
            traceId = context.TraceIdentifier,
            dataClassification = "operational",
            lgpd = new
            {
                legalBasis = "legitimate-interest",
                purpose,
                retention = "Retain only while needed for audit, security and legal obligations.",
                dataCategories = new[] { "operational" }
            }
        }
    };

readonly record struct CorsRule(string Scheme, string Host, int? Port, bool AllowSubdomains);

sealed class CorsRules(CorsRule[] items)
{
    public CorsRule[] Items { get; } = items;
}

public partial class Program { }


