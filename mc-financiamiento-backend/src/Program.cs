using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Infrastructure.Telemetry.OpenTelemetryConfig;
using Contoso.Modules.Loan.Services;
using Contoso.Modules.Loan.Repositories;
using Integration.GoRules;
using Infrastructure.Helpers;
using Contoso.Modules.Insight.Repositories;
using Contoso.Modules.eComerce.Repositories;
using Infrastructure.Data.NoSql;
using Infrastructure.Data.Sql;
using Microsoft.EntityFrameworkCore;
using Contoso.Modules.Loan.Config;


var builder = WebApplication.CreateBuilder(args);

// Cargar configuración desde local.settings.json si está disponible
builder.Configuration.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);

// Agregar OpenTelemetry
builder.Services.AddOpenTelemetryMonitoring(builder.Configuration);

// Registrar Data Context
builder.Services.AddSingleton<DapperContext>(provider =>
{
    var configuration = provider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    return new DapperContext(connectionString);
});

// Registrar Data Cosmos Context
builder.Services.AddDbContext<CosmosContext>(options =>
{
    var cosmosConnectionString = builder.Configuration["Data:NoSql.CosmosDb"];
    Console.WriteLine($"Cosmos Connection String: {cosmosConnectionString}");
    options.UseCosmos(cosmosConnectionString, databaseName: "ecomerce");
});

// Registrar configuración de loanConfig
builder.Services.Configure<LoanConfig>(builder.Configuration.GetSection("loanConfig"));

// Registrar servicios
builder.Services.AddScoped<LoanService>();
builder.Services.AddScoped<CustomerRepository>();
builder.Services.AddScoped<ProductRepository>();
builder.Services.AddScoped<CustomerHistoryPreCalculatedRepository>();
builder.Services.AddScoped<PollyHelper>();

// Registrar HttpClient para GoRulesIntegration
builder.Services.AddHttpClient<GoRulesIntegration>();

// Agregar otros servicios
builder.Services.AddControllers();

// Agregar Health Checks
builder.Services.AddHealthChecks();


var app = builder.Build();

app.UseRouting();


app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

await app.RunAsync();
