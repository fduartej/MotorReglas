using Orchestrator;
using Orchestrator.Features.FlowProcessing.Interfaces;
using Orchestrator.Features.FlowProcessing.Services;
using Orchestrator.Shared.Extensions;
using Orchestrator.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry monitoring
builder.Services.AddOpenTelemetryMonitoring(builder.Configuration);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JSON options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

// Register application configuration
builder.Services.Configure<AppSettings>(builder.Configuration);

// Register external configuration
builder.Services.AddSingleton<ExternalConfig>(provider =>
{
    var appSettings = provider.GetRequiredService<IConfiguration>().Get<AppSettings>()!;
    var flowsPath = Environment.GetEnvironmentVariable("EXTERNAL_FLOWS_PATH") ?? appSettings.ExternalConfig.FlowsPath;
    var templatesPath = Environment.GetEnvironmentVariable("EXTERNAL_TEMPLATES_PATH") ?? appSettings.ExternalConfig.TemplatesPath;
    var cacheSeconds = int.TryParse(Environment.GetEnvironmentVariable("EXTERNAL_CACHE_SECONDS"), out var cs) ? cs : appSettings.ExternalConfig.CacheSeconds;
    
    return new ExternalConfig(flowsPath, templatesPath, cacheSeconds);
});

// Register orchestrator services
builder.Services.AddOrchestrator(builder.Configuration);

// Register main orchestrator interface
builder.Services.AddScoped<IFlowOrchestrator, FlowOrchestrator>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Additional health check endpoint (redundant with controller, but useful for load balancers)
app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    service = "orchestrator-api" 
}));

await app.RunAsync();
