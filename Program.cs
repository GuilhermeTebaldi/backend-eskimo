using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;

using CSharpAssistant.API.Scripts;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Services;
using CSharpAssistant.API.Models;

 var builder = WebApplication.CreateBuilder(args);
 
 // 🔧 Configurações Npgsql para PostgreSQL 14+
 AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
 AppContext.SetSwitch("Npgsql.DisableDateTimeInfinityConversions", true);
 
 // 🔐 JWT
 builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
 .AddJwtBearer(options =>
 {
 var key = builder.Configuration["Jwt:Key"];
 options.TokenValidationParameters = new TokenValidationParameters
 {
 ValidateIssuer = true,
 ValidateAudience = true,
 ValidateLifetime = true,
 ValidateIssuerSigningKey = true,
 ValidIssuer = builder.Configuration["Jwt:Issuer"],
 ValidAudience = builder.Configuration["Jwt:Audience"],
 IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key!))
 };
 });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdmin", p => p.RequireRole("admin"));
    options.AddPolicy("RequireOperatorOrAdmin", p => p.RequireRole("admin", "operator"));
});



 builder.Services.AddScoped<TokenService>();
 
 // 📦 Controllers + JSON
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

 // 🗄️ Banco de dados PostgreSQL
 builder.Services.AddDbContext<AppDbContext>(options =>
 {
 options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
 options.EnableSensitiveDataLogging();
 });
 
 // 🧩 Serviços (DI)
 builder.Services.AddScoped<ProductService>();
 builder.Services.AddScoped<MercadoPagoService>(); // 💳 Mercado Pago
 builder.Services.AddHttpClient();
 builder.Services.AddHttpContextAccessor();
 builder.Services.AddHostedService<KeepAliveService>();
 
 // 🌐 CORS
 builder.Services.AddCors(options =>
 {
     options.AddPolicy("AllowFrontend", policy =>
     {
         var allowed = new[]
         {
             "http://localhost:5173",
             "http://127.0.0.1:5173",
             "https://localhost:5173",
             "https://127.0.0.1:5173",
             "https://www.admin.eskimochapeco.com.br",
             "https://admin.eskimochapeco.com.br",
             "https://eskimochapeco.com.br",
             "https://www.eskimochapeco.com.br"
         };

         policy
             .SetIsOriginAllowed(origin =>
             {
                 if (allowed.Contains(origin)) return true;
                 try
                 {
                     var host = new Uri(origin).Host.ToLowerInvariant();
                     return host.EndsWith("eskimochapeco.com.br");
                 }
                 catch
                 {
                     return false;
                 }
             })
             .AllowAnyHeader()
             .AllowAnyMethod()
             .AllowCredentials();
     });
 });
 
 // 📚 Swagger
 builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
c.SwaggerDoc("v1", new OpenApiInfo
{
Title = "e-Commerce API",
Version = "v1",
Description = "API modular para sistema de e-commerce"
});

c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
{
In = ParameterLocation.Header,
Description = "Insira o token JWT no formato: Bearer {seu token}",
Name = "Authorization",
Type = SecuritySchemeType.ApiKey,
Scheme = "Bearer"
});

c.AddSecurityRequirement(new OpenApiSecurityRequirement {
{
new OpenApiSecurityScheme {
Reference = new OpenApiReference {
Type = ReferenceType.SecurityScheme,
Id = "Bearer"
}
},
Array.Empty<string>()
}
});
});

builder.Services.AddSignalR();
 
 // 🔎 Log ConnectionString (debug)
 Console.WriteLine("🔑 ConnectionString atual:");
 Console.WriteLine(builder.Configuration.GetConnectionString("Default"));
 
 var app = builder.Build();

app.Use(async (ctx, next) =>
{
    var origin = ctx.Request.Headers["Origin"].ToString();
    if (!string.IsNullOrEmpty(origin))
    {
        var allow = false;
        try
        {
            var host = new Uri(origin).Host.ToLowerInvariant();
            var allowed = new[]
            {
                "www.admin.eskimochapeco.com.br",
                "admin.eskimochapeco.com.br",
                "eskimochapeco.com.br",
                "www.eskimochapeco.com.br",
                "localhost",
                "127.0.0.1"
            };
            allow = allowed.Any(h => host == h || host.EndsWith("." + h));
        }
        catch { allow = false; }

        if (allow)
        {
            ctx.Response.Headers["Access-Control-Allow-Origin"] = origin;
            ctx.Response.Headers["Vary"] = "Origin";
            ctx.Response.Headers["Access-Control-Allow-Credentials"] = "true";
            ctx.Response.Headers["Access-Control-Allow-Headers"] = "Authorization,Content-Type,X-Store";
            ctx.Response.Headers["Access-Control-Allow-Methods"] = "GET,POST,PUT,PATCH,DELETE,OPTIONS";

            if (HttpMethods.IsOptions(ctx.Request.Method))
            {
                ctx.Response.StatusCode = StatusCodes.Status204NoContent;
                return;
            }
        }
    }

    await next();
});
 
 // 📄 Licença QuestPDF
 QuestPDF.Settings.License = LicenseType.Community;
 
 // 🗃️ Migrar DB
 using (var scope = app.Services.CreateScope())
 {
 var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
 try
 {
 db.Database.Migrate();
 }
 catch (Exception ex)
 {
 app.Logger.LogError(ex, "EF migrate failed (provável PendingModelChangesWarning). Verifique migrations pendentes.");
 }
 
 }
 
 // ⚙️ Proxy/Headers da Render (garante Scheme/Host corretos p/ back_urls/webhook)
 app.UseForwardedHeaders(new ForwardedHeadersOptions
 {
 ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
 });
 
 // 🔍 Swagger
 if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
 {
 app.UseSwagger();
 app.UseSwaggerUI(c =>
 {
 c.SwaggerEndpoint("/swagger/v1/swagger.json", "e-Commerce API v1");
 c.RoutePrefix = "swagger";
 });
 }
 
app.UseRouting();
app.UseCors("AllowFrontend"); // aplica política CORS antes de Auth
app.UseAuthentication();
app.UseAuthorization(); 

// === Middleware de bloqueio fora do horário ===
// Bloqueia métodos potencialmente mutantes quando loja fechada.
 // Allowlist: status, isOpen, swagger, settings GET.
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
    var method = context.Request.Method?.ToUpperInvariant() ?? "GET";

    bool isAllowlisted =
        path.StartsWith("/swagger") ||
        path.StartsWith("/healthz") ||
        path.StartsWith("/ping") ||
        path.StartsWith("/api/auth") ||
        path.StartsWith("/api/users") ||
        path.StartsWith("/api/status") ||
        path.StartsWith("/api/settings") ||
        path.StartsWith("/api/store-settings") ||
        path.StartsWith("/api/storefront") ||
        path.StartsWith("/api/promotions") ||
        path.StartsWith("/api/store-customers") ||
        path.StartsWith("/api/payments") ||
        method == "OPTIONS";

    var isAdmin = context.User?.IsInRole("admin") == true;

    if (isAllowlisted || isAdmin)
    {
        await next();
        return;
    }

    if (method == "GET")
    {
        await next();
        return;
    }

    string? store =
        context.Request.Headers["X-Store"].FirstOrDefault()
        ?? context.Request.Query["store"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(store) && path.StartsWith("/api/orders") && method == "POST")
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("store", out var sProp) && sProp.ValueKind == JsonValueKind.String)
            {
                store = sProp.GetString();
            }
        }
        catch
        {
            // ignore parse errors
        }
    }

    try
    {
        var db = context.RequestServices.GetRequiredService<CSharpAssistant.API.Data.AppDbContext>();

        string? tz = null;
        string? hours = null;
        string? exceptions = null;

        if (!string.IsNullOrWhiteSpace(store))
        {
            var key = store.Trim().ToLowerInvariant();
            var cfg = await db.Set<CSharpAssistant.API.Models.StoreSetting>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Store == key);
            if (cfg != null)
            {
                tz = cfg.TimeZone;
                hours = cfg.OpeningHoursJson;
                exceptions = cfg.ExceptionsJson;
            }
        }

        if (tz == null && hours == null && exceptions == null)
        {
            var global = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
            if (global == null)
            {
                await next();
                return;
            }

            tz = global.TimeZone;
            hours = global.OpeningHoursJson;
            exceptions = global.ExceptionsJson;
        }

        var payload = StatusControllerStatic.EvaluateStatus(tz, hours, exceptions);
        if (!payload.IsOpen)
        {
            context.Response.StatusCode = StatusCodes.Status423Locked;
            await context.Response.WriteAsJsonAsync(new { error = payload.Message, nextOpening = payload.NextOpening });
            return;
        }

        await next();
    }
    catch
    {
        await next();
    }
});

// Responde a qualquer preflight OPTIONS com 200
app.MapMethods("{*path}", new[] { "OPTIONS" }, () => Results.Ok())
   .WithDisplayName("CorsPreflight");

app.MapControllers(); // 🚨 expõe Controllers (Products, Payments, etc.)
 
 // 🧪 Rotas teste
 app.MapGet("/", () => "🚀 e-Commerce API rodando com sucesso! Por: Guilherme Tebaldi");
 app.MapMethods("/ping", new[] { "GET", "POST", "HEAD", "OPTIONS" }, () => Results.Ok("pong"));
 
 // 🛠️ Endpoint para rodar importador manualmente
app.MapPost("/run-importer", async (AppDbContext db) =>
{
try
{
Console.WriteLine("📥 Executando importação manual via /run-importer");
await Task.Run(() => ImportProductsFromJson.Run(app));
return Results.Ok("✅ Importação realizada com sucesso.");
}
catch (Exception ex)
{
Console.WriteLine("❌ Erro completo:");
Console.WriteLine(ex.ToString());
return Results.Problem("Erro ao importar produtos: " + ex.Message);
}
});

app.MapHub<CSharpAssistant.API.Hubs.UpdateHub>("/updateHub");

// ✅ Log final
Console.WriteLine("✅ API iniciada e pronta para receber requisições.");

app.Run();
