using Microsoft.AspNetCore.Authentication.JwtBearer; 
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.ComponentModel.DataAnnotations.Schema;
using QuestPDF.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IO;

using CSharpAssistant.API.Scripts;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Services;

var builder = WebApplication.CreateBuilder(args);

// 🔐 Autenticação JWT
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

builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenService>();

// 📦 Controllers + JSON
builder.Services.AddControllers()
    .AddJsonOptions(x =>
        x.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

// 🧩 Serviços
builder.Services.AddScoped<ProductService>();

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

// 🗄️ Banco de dados PostgreSQL
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
    options.EnableSensitiveDataLogging();
});

// 🌐 CORS (liberar acesso ao site público correto)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "https://eskimosites.vercel.app",            // ✅ DOMÍNIO REAL DO SITE PÚBLICO
            "https://admin-panel-eskimo.vercel.app"       // ✅ DOMÍNIO DO PAINEL ADMIN
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

var app = builder.Build();

// 📄 Licença QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// Executar importação apenas uma vez, sem deletar o banco inteiro
// Desativado temporariamente para evitar queda de conexão na Render
// ImportProductsFromJson.Run(app); 

// 🧪 Testar existência dos arquivos no ambiente Render
Console.WriteLine("🧪 Arquivos na pasta atual:");
foreach (var f in Directory.GetFiles(Directory.GetCurrentDirectory(), "*", SearchOption.AllDirectories))
{
    Console.WriteLine("📄 " + f);
}

// 🚀 Middlewares
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
app.UseCors("AllowFrontend");   // ✅ TEM QUE VIR ANTES DA AUTENTICAÇÃO
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 🧪 Rotas simples
app.MapGet("/", () => "🚀 e-Commerce API rodando com sucesso! Por: Guilherme Tebaldi");
app.MapMethods("/ping", new[] { "GET", "POST", "HEAD", "OPTIONS" }, () => Results.Ok("pong"));

// 🧪 Endpoint de debug para rodar importação manual
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

app.Run();
