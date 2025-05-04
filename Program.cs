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

using CSharpAssistant.API.Scripts;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Services;

var builder = WebApplication.CreateBuilder(args);

// 🔐 JWT Authentication
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

// 📦 Controllers + Swagger
builder.Services.AddControllers()
    .AddJsonOptions(x =>
        x.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

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

// 🧩 Registro de serviços
builder.Services.AddScoped<ProductService>();

// 🌐 CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "http://localhost:5173",
            "https://admin-panel-eskimo.vercel.app",
            "https://eskimosites.vercel.app"
        )
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});

// 🔐 HTTPS (opcional)
const int HttpPort = 8080;
const int HttpsPort = 8443;
const string CertPath = "/https/aspnetapp.pfx";
const string CertPassword = "MinhaSenhaForte";

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ListenAnyIP(HttpPort);
    if (File.Exists(CertPath))
    {
        serverOptions.ListenAnyIP(HttpsPort, listenOptions =>
            listenOptions.UseHttps(CertPath, CertPassword));
    }
});

var app = builder.Build();

// 🌟 QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// ✅ Executa script de importação de produtos (se existir JSON)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Só importa se não houver produtos ainda
    if (!db.Products.Any())
    {
        Console.WriteLine("📦 Nenhum produto encontrado. Iniciando importação...");
        ImportProductsFromJson.Run(app);
    }
    else
    {
        Console.WriteLine("✅ Produtos já existem no banco. Ignorando importação.");
    }
}


// 🚀 Pipeline HTTP
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
app.MapControllers();

app.MapGet("/", () => "🚀 e-Commerce API rodando com sucesso! Por: Guilherme Tebaldi");
app.MapMethods("/ping", new[] { "GET", "POST", "HEAD", "OPTIONS" }, () => Results.Ok("pong"));

app.Run();
