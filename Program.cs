
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using AScheduler.Api.Services;
using AScheduler.Data;
using AScheduler.Execution;
using AScheduler.Queue;
using AScheduler.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// CORS Configuration
// ============================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSwaggerUI",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()
    );
});

// JWT Configuration
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Missing Jwt:Secret in configuration");
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
builder.Services.AddSingleton<IExecutionLogger, ExecutionLogger>();

// ============================================
// Data Access
// ============================================
builder.Services.AddSingleton<IBoxRepository, BoxRepository>();
builder.Services.AddSingleton<ITaskRepository, TaskRepository>();
builder.Services.AddSingleton<IExecutionRepository, ExecutionRepository>();

// ============================================
// Task Execution Service (SINGLE ENTRY POINT)
// All task executions must go through this service.
// ============================================
builder.Services.AddSingleton<IBoxRunMetricsService, BoxRunMetricsService>();
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
builder.Services.AddHealthChecks();

var app = builder.Build();

// ============================================
// Middleware Pipeline
// ============================================


// Enable CORS for Swagger UI and API
app.UseCors("AllowSwaggerUI");

// Enable Swagger in all environments for API documentation
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "AScheduler API v1.0");
    options.RoutePrefix = string.Empty; // Serve Swagger UI at root
    options.DocumentTitle = "AScheduler API Documentation";
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();