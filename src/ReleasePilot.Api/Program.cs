using Microsoft.EntityFrameworkCore;
using ReleasePilot.Api.Endpoints;
using ReleasePilot.Api.Middleware;
using ReleasePilot.Application.Interfaces;
using ReleasePilot.Domain.Ports;
using ReleasePilot.Infrastructure.Adapters;
using ReleasePilot.Infrastructure.Consumers;
using ReleasePilot.Infrastructure.Messaging;
using ReleasePilot.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog ---
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "releasepilot-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30));

// --- Database ---
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=releasepilot;Username=releasepilot;Password=releasepilot";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// --- MediatR (CQRS) ---
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(ReleasePilot.Application.Commands.RequestPromotionCommand).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(ReleasePilot.Infrastructure.Consumers.NotificationEventHandler).Assembly);
});

// --- RabbitMQ Settings ---
var rabbitMqConnection = builder.Configuration.GetValue<string>("RabbitMq:ConnectionString")
    ?? "amqp://guest:guest@localhost:5672/";

builder.Services.AddSingleton(new RabbitMqSettings { ConnectionString = rabbitMqConnection });

// --- Repositories ---
builder.Services.AddScoped<IPromotionRepository, PromotionRepository>();
builder.Services.AddScoped<IPromotionReadRepository, PromotionReadRepository>();

// --- Unit of Work ---
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// --- Event Bus (Outbox Pattern) ---
builder.Services.AddScoped<IEventBus, OutboxEventBus>();

// --- Ports (External System Adapters) ---
builder.Services.AddScoped<IDeploymentPort, StubDeploymentAdapter>();
builder.Services.AddScoped<IIssueTrackerPort, StubIssueTrackerAdapter>();
builder.Services.AddScoped<INotificationPort, StubNotificationAdapter>();

// --- Background Services ---
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<AuditLogConsumer>();

// --- Agent (optional - configured when LLM settings are provided) ---
ReleasePilot.Agent.AgentServiceRegistration.Register(builder.Services, builder.Configuration);

// --- Swagger / OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- Swagger UI (development only) ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ReleasePilot API v1");
        options.RoutePrefix = "swagger";
    });
}

// --- Middleware ---
app.UseMiddleware<DomainExceptionMiddleware>();

// --- Auto-migrate database ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// --- Endpoints ---
app.MapPromotionEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
