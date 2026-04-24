using Microsoft.EntityFrameworkCore;
using APIHU.Application.Interfaces;
using APIHU.Application.Services;
using APIHU.Domain.Interfaces;
using APIHU.Infrastructure.AI;
using APIHU.Infrastructure.Logging;
using APIHU.Infrastructure.Middleware;
using APIHU.Infrastructure.Persistence;
using APIHU.Infrastructure.BackgroundServices;
using DotNetEnv;

// Cargar variables del archivo .env si existe (buscando hacia arriba desde cwd)
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// Permitir que las variables de entorno sobrescriban appsettings (soporta ANTHROPIC__APIKEY, etc.)
builder.Configuration.AddEnvironmentVariables();

// ============================================
// 1. CONFIGURACIÓN DE LOGGING (Serilog)
// ============================================
LoggingConfiguration.ConfigureLogging(builder);

// ============================================
// 2. CONFIGURACIÓN DE SERVICIOS
// ============================================

// Controllers
builder.Services.AddControllers();

// Swagger mejorado
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "API de Generación de Historias de Usuario v2.0 (Production Ready)",
        Version = "v1",
        Description = @"API REST empresarial para generar Historias de Usuario usando IA.

## Arquitectura
La API utiliza Clean Architecture con las siguientes capas:
- **API**: Controllers y endpoints
- **Application**: Casos de uso y servicios de negocio
- **Domain**: Entidades e interfaces
- **Infrastructure**: IA, persistencia y logging

## Pipeline de Procesamiento
El procesamiento se realiza en 3 etapas:
1. **Limpieza**: Normaliza y limpia el texto de entrada
2. **Estructuración**: Identifica requerimientos y funcionalidades
3. **Generación**: Crea las HUs con criterios y tareas técnicas

## Características de Producción
- Correlation ID para trazabilidad
- Rate Limiting
- API Key validation
- Logging estructurado con Serilog
- Validación de HUs
- Background Service para procesamiento asíncrono",
        Contact = new() { Name = "Equipo de Desarrollo", Email = "dev@empresa.com" },
        License = new() { Name = "MIT" }
    });
});

// Entity Framework Core
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<APIHUDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
        sqlOptions.CommandTimeout(30);
    }));

// ============================================
// 3. CONFIGURACIÓN DE IA (chain de proveedores con fallback automático)
// ============================================
// AI:ProviderChain define el orden de proveedores. Si el primero falla
// por completo, se intenta el siguiente. Solo se registran los que tienen
// API key válida (los demás se saltan con un warning).
//
// Valores válidos: anthropic, gemini, openrouter, groq
// Configuración por .env:
//   AI__ProviderChain=openrouter,groq,gemini,anthropic    (chain)
//   AI__Provider=openrouter                                (legacy, un solo provider)

var chainRaw = builder.Configuration["AI:ProviderChain"]
    ?? Environment.GetEnvironmentVariable("AI__ProviderChain")
    ?? builder.Configuration["AI:Provider"]
    ?? Environment.GetEnvironmentVariable("AI_PROVIDER")
    ?? "openrouter,groq,gemini,anthropic";

var chain = chainRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
    .Select(s => s.Trim().ToLowerInvariant())
    .Where(s => !string.IsNullOrWhiteSpace(s))
    .Distinct()
    .ToList();

Console.WriteLine($">> AI Provider Chain solicitado: [{string.Join(" → ", chain)}]");

// Tipos de providers registrados exitosamente (con key válida)
var tiposRegistrados = new List<Type>();
var resumenRegistro = new List<string>();

foreach (var proveedor in chain)
{
    switch (proveedor)
    {
        case "anthropic":
        {
            var opts = builder.Configuration.GetSection(AnthropicOptions.SectionName).Get<AnthropicOptions>()
                ?? new AnthropicOptions();
            var envKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey)) opts.ApiKey = envKey;

            if (string.IsNullOrWhiteSpace(opts.ApiKey) || opts.ApiKey.StartsWith("sk-ant-PEGA"))
            {
                resumenRegistro.Add($"  [skip] anthropic       (sin API key)");
                break;
            }
            builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection(AnthropicOptions.SectionName));
            builder.Services.AddSingleton(opts);
            builder.Services.AddHttpClient<AnthropicProviderService>(c =>
                c.Timeout = TimeSpan.FromSeconds(opts.TimeoutSegundos));
            tiposRegistrados.Add(typeof(AnthropicProviderService));
            resumenRegistro.Add($"  [ok]   anthropic       ({opts.Modelo})");
            break;
        }

        case "gemini":
        {
            var opts = builder.Configuration.GetSection(GeminiOptions.SectionName).Get<GeminiOptions>()
                ?? new GeminiOptions();
            var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey)) opts.ApiKey = envKey;

            if (string.IsNullOrWhiteSpace(opts.ApiKey) || opts.ApiKey.StartsWith("AIza-PEGA"))
            {
                resumenRegistro.Add($"  [skip] gemini          (sin API key)");
                break;
            }
            builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection(GeminiOptions.SectionName));
            builder.Services.AddSingleton(opts);
            builder.Services.AddHttpClient<GeminiProviderService>(c =>
                c.Timeout = TimeSpan.FromSeconds(opts.TimeoutSegundos));
            tiposRegistrados.Add(typeof(GeminiProviderService));
            resumenRegistro.Add($"  [ok]   gemini          ({opts.Modelo})");
            break;
        }

        case "openrouter":
        {
            var opts = builder.Configuration.GetSection(OpenRouterOptions.SectionName).Get<OpenRouterOptions>()
                ?? new OpenRouterOptions();
            var envKey = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey)) opts.ApiKey = envKey;

            if (string.IsNullOrWhiteSpace(opts.ApiKey) || opts.ApiKey.StartsWith("sk-or-PEGA"))
            {
                resumenRegistro.Add($"  [skip] openrouter      (sin API key)");
                break;
            }
            builder.Services.Configure<OpenRouterOptions>(builder.Configuration.GetSection(OpenRouterOptions.SectionName));
            builder.Services.AddSingleton(opts);
            builder.Services.AddHttpClient<OpenRouterProviderService>(c =>
                c.Timeout = TimeSpan.FromSeconds(opts.TimeoutSegundos));
            tiposRegistrados.Add(typeof(OpenRouterProviderService));
            var fallbackCount = (opts.ModelosFallback?.Length ?? 0);
            resumenRegistro.Add($"  [ok]   openrouter      ({opts.Modelo}, +{fallbackCount} fallbacks internos)");
            break;
        }

        case "groq":
        {
            var opts = builder.Configuration.GetSection(GroqOptions.SectionName).Get<GroqOptions>()
                ?? new GroqOptions();
            var envKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey)) opts.ApiKey = envKey;

            if (string.IsNullOrWhiteSpace(opts.ApiKey) || opts.ApiKey.StartsWith("gsk-PEGA"))
            {
                resumenRegistro.Add($"  [skip] groq            (sin API key)");
                break;
            }
            builder.Services.Configure<GroqOptions>(builder.Configuration.GetSection(GroqOptions.SectionName));
            builder.Services.AddSingleton(opts);
            builder.Services.AddHttpClient<GroqProviderService>(c =>
                c.Timeout = TimeSpan.FromSeconds(opts.TimeoutSegundos));
            tiposRegistrados.Add(typeof(GroqProviderService));
            resumenRegistro.Add($"  [ok]   groq            ({opts.Modelo})");
            break;
        }

        default:
            resumenRegistro.Add($"  [skip] {proveedor,-15} (desconocido)");
            break;
    }
}

Console.WriteLine(">> Registro de providers:");
foreach (var linea in resumenRegistro) Console.WriteLine(linea);

if (tiposRegistrados.Count == 0)
{
    throw new InvalidOperationException(
        "No hay ningún proveedor de IA configurado con API key válida. " +
        "Define al menos una: ANTHROPIC_API_KEY, GEMINI_API_KEY, OPENROUTER_API_KEY o GROQ_API_KEY.");
}

// Registrar IAIProviderService:
// - Si solo hay uno configurado, se resuelve directo (sin overhead del wrapper)
// - Si hay varios, se envuelven en MultiProviderFallbackService
if (tiposRegistrados.Count == 1)
{
    var tipoUnico = tiposRegistrados[0];
    builder.Services.AddScoped<IAIProviderService>(sp =>
        (IAIProviderService)sp.GetRequiredService(tipoUnico));
}
else
{
    builder.Services.AddScoped<IAIProviderService>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<MultiProviderFallbackService>>();
        var proveedores = tiposRegistrados
            .Select(t => (IAIProviderService)sp.GetRequiredService(t))
            .ToList();
        return new MultiProviderFallbackService(proveedores, logger);
    });
}

var chainActivaResumen = string.Join(" → ",
    tiposRegistrados.Select(t => t.Name.Replace("ProviderService", "").ToLowerInvariant()));

// ============================================
// 4. SERVICIOS DE APLICACIÓN (v2.0 Production)
// ============================================
builder.Services.AddScoped<IPromptService, PromptService>();
builder.Services.AddScoped<IPipelineProcesamientoService, PipelineProcesamientoService>();
builder.Services.AddScoped<IGeneracionHUService, GeneracionHUService>();

// NEW: Orchestrator del Pipeline
builder.Services.AddScoped<IHuProcessingOrchestrator, HuProcessingOrchestrator>();

// NEW: Validador de HUs
builder.Services.AddScoped<IHuValidatorService, HuValidatorService>();

// ============================================
// 5. REPOSITORIOS
// ============================================
builder.Services.AddScoped<IHistoriaUsuarioRepository, HistoriaUsuarioRepository>();
builder.Services.AddScoped<IGeneracionRepository, GeneracionRepository>();

// ============================================
// 6. MIDDLEWARES DE PRODUCCIÓN
// ============================================

// NEW: Rate Limiting
builder.Services.AddRateLimiting(builder.Configuration);

// NEW: API Key
builder.Services.AddApiKey(builder.Configuration);

// NEW: Background Service (deshabilitado por defecto)
builder.Services.AddHuBackgroundService(builder.Configuration);

// ============================================
// 7. CORS
// ============================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ============================================
// 8. PIPELINE DE LA APLICACIÓN
// ============================================

var app = builder.Build();

// NEW: Correlation ID middleware (debe ser primero)
app.UseCorrelationId();

// Logging de request
app.UseRequestLogging();

// NEW: Rate Limiting
app.UseRateLimiting();

// NEW: API Key validation
app.UseApiKey();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
        c.RoutePrefix = "swagger";
        c.DisplayRequestDuration();
        c.DisplayOperationId();
    });
}

// Manejo de errores global
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var correlationId = context.GetCorrelationId() ?? "UNKNOWN";
        
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            Tipo = "UnhandledError",
            Mensaje = "Error interno del servidor",
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        });
    });
});

app.UseCors("AllowAll");
app.UseRouting();
app.MapControllers();

// ============================================
// 9. INICIALIZACIÓN DE BASE DE DATOS
// ============================================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var dbContext = services.GetRequiredService<APIHUDbContext>();

    try
    {
        logger.LogInformation("Verificando estado de la base de datos...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Base de datos actualizada correctamente");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error al inicializar la base de datos");
        try
        {
            await dbContext.Database.EnsureCreatedAsync();
            logger.LogInformation("Base de datos creada correctamente");
        }
        catch (Exception ex2)
        {
            logger.LogError(ex2, "No se pudo crear la base de datos");
        }
    }
}

// ============================================
// 10. INICIO DE LA APLICACIÓN
// ============================================

var port = builder.Configuration["PORT"] ?? "5000";
var urls = $"http://0.0.0.0:{port}";

// Obtener configuración de rate limiting para mostrar
var rateLimitEnabled = builder.Configuration["RateLimiting:Enabled"] ?? "true";
var rateLimitMax = builder.Configuration["RateLimiting:MaxRequests"] ?? "100";
var apiKeyEnabled = builder.Configuration["ApiKey:Enabled"] ?? "false";

Console.WriteLine($"""
    ╔══════════════════════════════════════════════════════════╗
    ║  API de Generación de Historias de Usuario v2.0          ║
    ║  Production Ready - Enterprise Grade                     ║
    ║  ========================================================  ║
    ║  Swagger UI:     http://localhost:{port}/swagger          ║
    ║  API Endpoint:   http://localhost:{port}/api/hu           ║
    ║  Health Check:   http://localhost:{port}/api/hu/health    ║
    ║                                                          ║
    ║  ARQUITECTURA:                                          ║
    ║  • Clean Architecture                                     ║
    ║  • Pipeline: 3 etapas (Limpieza→Estructuración→HU)       ║
    ║  • IA: {chainActivaResumen,-49} ║
    ║                                                          ║
    ║  PRODUCCIÓN:                                             ║
    ║  • Correlation ID: ✓                                     ║
    ║  • Rate Limiting: {rateLimitEnabled,-5} ({rateLimitMax} req/min)          ║
    ║  • API Key: {apiKeyEnabled,-5}                                     ║
    ║  • Serilog: ✓                                            ║
    ║  • Background Service: Preparado                         ║
    ╚══════════════════════════════════════════════════════════╝
    """);

app.Run(urls);
