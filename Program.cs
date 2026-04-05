
using System.Collections.ObjectModel;
using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AScheduler.Api.Middleware;
using AScheduler.Api.Services;
using AScheduler.Data;
using AScheduler.Execution;
using AScheduler.Queue;
using AScheduler.Services;
using AScheduler.Services.Logging;
using Serilog;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

var logConnectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");

var logColumnOptions = CreateApplicationLogColumnOptions();
SelfLog.Enable(message => System.Diagnostics.Debug.WriteLine(message));

builder.Host.UseSerilog((context, _, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
        .MinimumLevel.Override("AScheduler", LogEventLevel.Information)
        .Enrich.FromLogContext()
        .Enrich.With(new ErrorLocationEnricher())
        .WriteTo.File(
            path: "logs/app-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            restrictedToMinimumLevel: context.HostingEnvironment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.Logger(lc => lc
            .Filter.ByIncludingOnly(evt => evt.Level >= LogEventLevel.Warning)
            .WriteTo.MSSqlServer(
                connectionString: logConnectionString,
                sinkOptions: new MSSqlServerSinkOptions
                {
                    TableName = "ApplicationLogs",
                    AutoCreateSqlTable = false,
                    BatchPostingLimit = 50,
                    BatchPeriod = TimeSpan.FromSeconds(2)
                },
                columnOptions: logColumnOptions,
                restrictedToMinimumLevel: LogEventLevel.Warning));

    if (context.HostingEnvironment.IsDevelopment())
    {
        loggerConfiguration.WriteTo.Console(
            restrictedToMinimumLevel: LogEventLevel.Information,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");
    }
});

// ============================================
// CORS Configuration
// ============================================
var configuredAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
var allowedOrigins = SecurityStartupValidator.ValidateAndNormalizeCorsOrigins(
    configuredAllowedOrigins,
    builder.Environment.IsDevelopment(),
    builder.Environment.IsProduction());

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCors", policy =>
    {
        if (allowedOrigins.Length == 0 && builder.Environment.IsDevelopment())
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        if (allowedOrigins.Length > 0)
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy
            .WithOrigins("https://localhost")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// JWT Configuration
var (jwtSecret, usedJwtAppSettingsFallback) = JwtSecretResolver.Resolve(builder.Configuration);
SecurityStartupValidator.ValidateJwtSecretSource(
    usedJwtAppSettingsFallback,
    builder.Environment.IsProduction(),
    JwtSecretResolver.SecretEnvironmentVariableName);
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AScheduler";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ASchedulerAPI";

var key = Encoding.ASCII.GetBytes(jwtSecret);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Add controllers
builder.Services.AddControllers();

// Add Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AScheduler API",
        Version = "v1.0.0",
        Description = "REST API para programación y ejecución de tareas automáticas con autenticación JWT",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "AScheduler Support",
            Email = "support@ascheduler.local"
        },
        License = new Microsoft.OpenApi.Models.OpenApiLicense
        {
            Name = "MIT License"
        }
    });

    // Resolve Swagger server URLs from Kestrel endpoints to prevent
    // "URL scheme must be http or https" errors when using Try It Out.
    var kestrelUrls = builder.Configuration["Kestrel:Endpoints:Http:Url"]
                   ?? builder.Configuration["Urls"]
                   ?? "http://localhost:5000";
    foreach (var url in kestrelUrls.Split(';', StringSplitOptions.RemoveEmptyEntries))
    {
        if (Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            options.AddServer(new Microsoft.OpenApi.Models.OpenApiServer
            {
                Url = $"{uri.Scheme}://{uri.Authority}",
                Description = $"{uri.Scheme.ToUpperInvariant()} endpoint"
            });
        }
    }

    // Configure JWT Bearer security scheme
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML documentation comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// ============================================
// Configuration Bindings
// ============================================
builder.Services.Configure<SmtpNotificationOptions>(
    builder.Configuration.GetSection("Notifications:Smtp"));
builder.Services.AddDataProtection();

// ============================================
// Queue Configuration
// ============================================
builder.Services.AddSingleton<AScheduler.Queue.ITaskQueue, AScheduler.Queue.TaskQueue>();

// ============================================
// Timeout Configuration
// ============================================
// Use task timeout from WorkerPool config; fall back to Scheduler default if not configured
var taskTimeoutSeconds = builder.Configuration.GetValue<int>("WorkerPool:TaskTimeoutSeconds", 300);
var taskTimeout = TimeSpan.FromSeconds(taskTimeoutSeconds);

// ============================================
// Executors
// ============================================
builder.Services.AddTransient<ExeExecutor>(_ => new ExeExecutor(taskTimeout));
builder.Services.AddTransient<BatExecutor>(_ => new BatExecutor(taskTimeout));
builder.Services.AddTransient<PythonExecutor>(_ => new PythonExecutor(taskTimeout));
builder.Services.AddTransient<ApiExecutor>((sp) =>
{
    var logger = sp.GetRequiredService<ILogger<ApiExecutor>>();
    return new ApiExecutor(logger, taskTimeout);
});

// ============================================
// Factory
// ============================================
builder.Services.AddSingleton<ExecutorFactory>();

// ============================================
// API Services
// ============================================
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ISearchService, DatabaseSearchService>();
builder.Services.AddSingleton<IExecutionLogger, ExecutionLogger>();
builder.Services.AddSingleton<INotificationSecretProtector, NotificationSecretProtector>();
builder.Services.AddSingleton<ISmtpMailSender, MailKitSmtpMailSender>();
builder.Services.AddSingleton<ITaskFailureNotificationService, SmtpTaskFailureNotificationService>();

// ============================================
// Data Access
// ============================================
builder.Services.AddSingleton<IBoxRepository, BoxRepository>();
builder.Services.AddSingleton<ITaskRepository, TaskRepository>();
builder.Services.AddSingleton<IExecutionRepository, ExecutionRepository>();
builder.Services.AddSingleton<IDepartmentRepository, DepartmentRepository>();
builder.Services.AddSingleton<INotificationSettingsRepository, NotificationSettingsRepository>();

// ============================================
// Task Execution Service (SINGLE ENTRY POINT)
// All task executions must go through this service.
// ============================================
builder.Services.AddSingleton<IBoxRunMetricsService, BoxRunMetricsService>();
builder.Services.AddSingleton<IStaleThresholdProvider, StaleThresholdProvider>();
builder.Services.AddSingleton<ITaskExecutionService, TaskExecutionService>();

// ============================================
// Background Services
// ============================================
builder.Services.AddHostedService<SchedulerService>();
builder.Services.AddSingleton<ConfigurableWorkerPool>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ConfigurableWorkerPool>());
builder.Services.AddSingleton<IWorkerStateService>(sp => sp.GetRequiredService<ConfigurableWorkerPool>());

// ============================================
// Health Checks
// ============================================
builder.Services
    .AddHealthChecks()
    .AddCheck<DatabaseConnectivityHealthCheck>("database", tags: new[] { "ready" })
    .AddCheck<WorkerPoolHealthCheck>("workerpool", tags: new[] { "ready" })
    .AddCheck("self", () => HealthCheckResult.Healthy("API process is alive."), tags: new[] { "live" });

var app = builder.Build();

if (usedJwtAppSettingsFallback)
{
    app.Logger.LogInformation(
        "JWT secret loaded from appsettings fallback in non-production because environment variable {EnvironmentVariable} is not set.",
        JwtSecretResolver.SecretEnvironmentVariableName);
}

var smtpSection = app.Configuration.GetSection("Notifications:Smtp");
var smtpEnabled = smtpSection.GetValue<bool>("Enabled");
var smtpHost = smtpSection.GetValue<string>("Host") ?? "";
var smtpPort = smtpSection.GetValue<int>("Port", 587);
var smtpSsl = smtpSection.GetValue<bool>("EnableSsl", true);
var smtpFrom = smtpSection.GetValue<string>("FromAddress") ?? "";
app.Logger.LogInformation(
    "SMTP notification configuration loaded. Enabled={Enabled}, Host={Host}, Port={Port}, EnableSsl={EnableSsl}, FromAddress={FromAddress}",
    smtpEnabled,
    smtpHost,
    smtpPort,
    smtpSsl,
    smtpFrom);

// ============================================
// Middleware Pipeline
// ============================================

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

app.UseMiddleware<GlobalExceptionHandlingMiddleware>();


// Enable CORS for Swagger UI and API
app.UseCors("FrontendCors");

// Enable Swagger only in non-production environments
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AScheduler API v1.0");
        options.RoutePrefix = string.Empty; // Serve Swagger UI at root
        options.DocumentTitle = "AScheduler API Documentation";
    });
}

var enableHttpsRedirection = builder.Configuration.GetValue<bool>("HttpsRedirection:Enabled", false);
SecurityStartupValidator.ValidateHttpsPipelineConsistency(
    enableHttpsRedirection,
    allowedOrigins,
    builder.Environment.IsProduction());

if (enableHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthResponseAsync
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponseAsync
});

app.Run();

static ColumnOptions CreateApplicationLogColumnOptions()
{
    var columnOptions = new ColumnOptions
    {
        AdditionalColumns = new Collection<SqlColumn>
        {
            new() { ColumnName = "LogFileName", DataType = SqlDbType.NVarChar, DataLength = 100, AllowNull = false, PropertyName = "LogFileName" },
            new() { ColumnName = "ErrorFile", DataType = SqlDbType.NVarChar, DataLength = 255, AllowNull = true, PropertyName = "ErrorFile" },
            new() { ColumnName = "ErrorMethod", DataType = SqlDbType.NVarChar, DataLength = 255, AllowNull = true, PropertyName = "ErrorMethod" },
            new() { ColumnName = "ErrorLine", DataType = SqlDbType.Int, AllowNull = true, PropertyName = "ErrorLine" },
            new() { ColumnName = "ExceptionType", DataType = SqlDbType.NVarChar, DataLength = 255, AllowNull = true, PropertyName = "ExceptionType" },
            new() { ColumnName = "Source", DataType = SqlDbType.NVarChar, DataLength = 255, AllowNull = true, PropertyName = "SourceContext" },
            new() { ColumnName = "CorrelationId", DataType = SqlDbType.UniqueIdentifier, AllowNull = true, PropertyName = "CorrelationId" },
            new() { ColumnName = "UserId", DataType = SqlDbType.Int, AllowNull = true, PropertyName = "UserId" },
            new() { ColumnName = "RequestPath", DataType = SqlDbType.NVarChar, DataLength = 500, AllowNull = true, PropertyName = "RequestPath" },
            new() { ColumnName = "StatusCode", DataType = SqlDbType.Int, AllowNull = true, PropertyName = "StatusCode" }
        }
    };

    columnOptions.Store.Clear();
    columnOptions.Store.Add(StandardColumn.TimeStamp);
    columnOptions.Store.Add(StandardColumn.Level);
    columnOptions.Store.Add(StandardColumn.Message);

    columnOptions.TimeStamp.ColumnName = "Timestamp";
    columnOptions.TimeStamp.ConvertToUtc = true;
    columnOptions.Level.ColumnName = "Level";
    columnOptions.Message.ColumnName = "Message";
    columnOptions.Message.DataLength = 1000;

    return columnOptions;
}

static Task WriteHealthResponseAsync(HttpContext context, Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport report)
{
    context.Response.ContentType = "application/json";

    var payload = new
    {
        status = report.Status.ToString(),
        totalDurationMs = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new
            {
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds,
                data = entry.Value.Data
            })
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}