using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using QuestPDF.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IO;

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

// 🌐 CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            // Domínios novos do site
            "https://eskimochapeco.com.br",
            "https://www.eskimochapeco.com.br",

            // Domínios antigos/atuais (manter enquanto necessário)
            "https://eskimosites.vercel.app",
            "https://admin-panel-eskimo.vercel.app"
        )
        .AllowAnyHeader()
        .AllowAnyMethod();
    });
});

// 🔎 Log ConnectionString
Console.WriteLine("🔑 ConnectionString atual:");
Console.WriteLine(builder.Configuration.GetConnectionString("Default"));

var app = builder.Build();
// 🚀 Cria as tabelas no banco se não existirem
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}


// 📄 Licença QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

// ✅ Debug: listar arquivos no container Render
Console.WriteLine("🧪 Arquivos no ambiente Render:");
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
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

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

// ✅ Log final ao iniciar
Console.WriteLine("✅ API iniciada e pronta para receber requisições.");

app.Run();
