using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AgentForge.Api.Endpoints;
using AgentForge.Api.Filters;
using AgentForge.Api.Middleware;
using AgentForge.Infrastructure;
using AgentForge.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var isDevelopment = builder.Environment.IsDevelopment();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1_100_000);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
});
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Authentication:Authority"];
        options.Audience = builder.Configuration["Authentication:Audience"];
        options.RequireHttpsMetadata = !isDevelopment;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrWhiteSpace(options.Authority),
            ValidateAudience = !string.IsNullOrWhiteSpace(options.Audience),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 120,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
    options.AddPolicy("chat", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

if (!isDevelopment
    && (string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Authority"])
        || string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Audience"])))
{
    throw new InvalidOperationException("Production requires Authentication:Authority and Authentication:Audience.");
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/", () => TypedResults.Ok(new
    {
        service = "AgentForge API",
        health = "/health",
        documentation = isDevelopment ? "/swagger" : null
    }))
    .WithName("ApiInfo")
    .WithSummary("Show API entry points")
    .WithOpenApi();

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok", service = "AgentForge API" }))
    .WithName("Health")
    .WithSummary("Check API liveness")
    .WithOpenApi();

var api = app.MapGroup("")
    .RequireRateLimiting("api")
    .AddEndpointFilter<ValidationFilter>();
if (!isDevelopment)
{
    api.RequireAuthorization();
}

api.MapAgents(enforceAdminRole: !isDevelopment);
api.MapChat();
api.MapKnowledge(enforceAdminRole: !isDevelopment);
api.MapWorkspace();
api.MapInterop(enforceAdminRole: !isDevelopment);

if (app.Configuration.GetValue("Database:ApplyMigrations", true))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AgentForgeDbContext>();
    await DatabaseInitializer.InitializeAsync(dbContext, CancellationToken.None);
}

app.Run();

public partial class Program;
