using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
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
 policy.WithOrigins(
 "http://localhost:5173",
 "http://127.0.0.1:5173",
 "https://localhost:5173",
 "http://localhost:5174",
 "https://127.0.0.1:5173",
 
 // Admin / Site públicos
 "https://www.admin.eskimochapeco.com.br",
 "https://admin.eskimochapeco.com.br",
 "https://eskimochapeco.com.br",
 "https://www.eskimochapeco.com.br",
 
 // Vercel antigos/atuais
 "https://eskimosites.vercel.app",
 "https://admin-panel-eskimo.vercel.app",
 "https://site-eskimo.vercel.app"
 )
 .AllowAnyHeader()
 .AllowAnyMethod()
 .AllowCredentials(); // habilite se precisar enviar cookies/autenticação cross-site
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
 app.UseCors("AllowFrontend");
 app.UseAuthentication();
 app.UseAuthorization(); 
 
 // === Middleware de bloqueio fora do horário ===
 // Bloqueia métodos potencialmente mutantes quando loja fechada.
 // Allowlist: status, isOpen, swagger, settings GET.
 app.Use(async (context, next) =>
 {
     var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
     var method = context.Request.Method.ToUpperInvariant();
 
    bool isAllowlisted =
        path.StartsWith("/swagger") ||
        path.StartsWith("/api/status") ||
        // Admin deve funcionar sempre
        path.StartsWith("/api/auth") ||          // login, refresh, etc.
        path.StartsWith("/api/users") ||         // gestão de usuários
        path.StartsWith("/api/settings") ||      // editar horários mesmo fechado
        method == "OPTIONS";                     // CORS preflight
 
     if (isAllowlisted)
     {
         await next();
         return;
     }
 
    // Apenas bloqueia quando não for GET, ou seja, POST/PUT/PATCH/DELETE.
    // Se quiser bloquear tudo inclusive GET de certos recursos, adapte aqui.
    if (method == "GET")
     {
         await next();
         return;
     }
 
     // Carrega status isOpen
     try
     {
         var db = context.RequestServices.GetRequiredService<CSharpAssistant.API.Data.AppDbContext>();
         var setting = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
 
         // Sem settings configurado => permite
         if (setting == null)
         {
             await next();
             return;
         }
 
         // Reusa o cálculo chamando o controller logicamente: duplicamos apenas o essencial
         var tzId = string.IsNullOrWhiteSpace(setting.TimeZone) ? "America/Sao_Paulo" : setting.TimeZone;
         TimeZoneInfo tz;
         try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
         catch { tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
 
         var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
 
         bool isOpen = await IsOpenInternal(nowLocal, setting);
 
         if (!isOpen)
         {
             context.Response.StatusCode = 423; // Locked
             context.Response.ContentType = "application/json";
             var payload = JsonSerializer.Serialize(new { error = "Fora do horário de funcionamento." });
             await context.Response.WriteAsync(payload);
             return;
         }
 
         await next();
     }
     catch
     {
         // Em falha de checagem, não bloqueia.
         await next();
     }
 
     // Função local para reaproveitar cálculo mínimo sem duplicar helpers públicos
     static async Task<bool> IsOpenInternal(DateTime nowLocal, CSharpAssistant.API.Models.Setting setting)
     {
         // Parsing reduzido
         Dictionary<string, List<(string start, string end)>> hours;
         List<(string date, bool closed, List<(string start, string end)> ranges)> exceptions;
         try
         {
             var hoursDict = JsonSerializer.Deserialize<Dictionary<string, List<Dictionary<string, string>>>>(
                 string.IsNullOrWhiteSpace(setting.OpeningHoursJson) ? "{}" : setting.OpeningHoursJson
             ) ?? new();
             hours = hoursDict.ToDictionary(
                 kv => kv.Key.ToLowerInvariant(),
                 kv => kv.Value.Select(v => (v.GetValueOrDefault("start") ?? "00:00", v.GetValueOrDefault("end") ?? "00:00")).ToList()
             );
         }
         catch { hours = new(); }
 
         try
         {
             var excList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
                 string.IsNullOrWhiteSpace(setting.ExceptionsJson) ? "[]" : setting.ExceptionsJson
             ) ?? new();
             exceptions = excList.Select(e =>
             {
                 var date = e.ContainsKey("date") ? e["date"]?.ToString() ?? "" : "";
                 var closed = e.ContainsKey("closed") && e["closed"] is bool b && b;
                 var ranges = new List<(string start, string end)>();
                 if (e.TryGetValue("ranges", out var rv) && rv is JsonElement je && je.ValueKind == JsonValueKind.Array)
                 {
                     foreach (var it in je.EnumerateArray())
                     {
                         var s = it.TryGetProperty("start", out var js) ? js.GetString() ?? "00:00" : "00:00";
                         var en = it.TryGetProperty("end", out var jee) ? jee.GetString() ?? "00:00" : "00:00";
                         ranges.Add((s, en));
                     }
                 }
                 return (date, closed, ranges);
             }).ToList();
         }
         catch { exceptions = new(); }
 
         var dateKey = nowLocal.ToString("yyyy-MM-dd");
         var dowKey = nowLocal.DayOfWeek switch
         {
             DayOfWeek.Monday => "monday",
             DayOfWeek.Tuesday => "tuesday",
             DayOfWeek.Wednesday => "wednesday",
             DayOfWeek.Thursday => "thursday",
             DayOfWeek.Friday => "friday",
             DayOfWeek.Saturday => "saturday",
             DayOfWeek.Sunday => "sunday",
             _ => "monday"
         };
 
         var exc = exceptions.FirstOrDefault(e => e.date == dateKey);
         if (!string.IsNullOrEmpty(exc.date))
         {
             if (exc.closed) return false;
             if (exc.ranges.Count > 0) return Within(nowLocal, exc.ranges);
         }
 
         hours.TryGetValue(dowKey, out var ranges);
         return Within(nowLocal, ranges ?? new());
 
         static bool Within(DateTime local, List<(string start, string end)> ranges)
         {
             foreach (var r in ranges)
             {
                 if (!Try(local.Date, r.start, out var s)) continue;
                 if (!Try(local.Date, r.end, out var e)) continue;
                 if (s <= e)
                 {
                     if (local >= s && local <= e) return true;
                 }
                 else // cruza meia-noite
                 {
                     if (local >= s || local <= e) return true;
                 }
             }
             return false;
 
             static bool Try(DateTime d, string hhmm, out DateTime t)
             {
                 t = d;
                 var parts = hhmm.Split(':');
                 if (parts.Length != 2) return false;
                 if (!int.TryParse(parts[0], out var h)) return false;
                 if (!int.TryParse(parts[1], out var m)) return false;
                 t = d.Date.AddHours(h).AddMinutes(m);
                 return true;
             }
         }
     }
 });
 
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
