using System.Reflection;
using System.Threading.Channels;
using System.Text.Json.Serialization;
using Gekko.Waybills.Api.BackgroundServices;
using Gekko.Waybills.Api.Middleware;
using Gekko.Waybills.Api.Swagger;
using Gekko.Waybills.Api.Services;
using Gekko.Waybills.Application.Abstractions;
using Gekko.Waybills.Application.Events;
using Gekko.Waybills.Application.Imports;
using Gekko.Waybills.Application.Locks;
using Gekko.Waybills.Application.Queries;
using Gekko.Waybills.Domain;
using Gekko.Waybills.Infrastructure;
using Gekko.Waybills.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Logging.AddConsole();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    options.OperationFilter<TenantHeaderOperationFilter>();
});
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.AddScoped<IWaybillImportService, WaybillImportService>();
builder.Services.AddScoped<IWaybillQueryService, WaybillQueryService>();
builder.Services.AddScoped<IExecutionLockService, ExecutionLockService>();
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISummaryCacheVersionProvider, SummaryCacheVersionProvider>();
builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection("Cache"));
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
builder.Services.AddSingleton(Channel.CreateUnbounded<ImportJobWorkItem>());
builder.Services.AddSingleton<IImportJobQueue, ImportJobQueue>();
builder.Services.AddHostedService<WaybillsImportAuditConsumer>();
builder.Services.AddHostedService<ImportJobWorker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<TenantMiddleware>();
app.MapControllers();

app.Run();

public partial class Program
{
}