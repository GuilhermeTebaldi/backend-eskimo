using System;
using System.Threading;
using System.Threading.Tasks;
using CSharpAssistant.API.Controllers;
using CSharpAssistant.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CSharpAssistant.API.Services
{
    public class KeepAliveService : BackgroundService
    {
        private readonly IServiceProvider _services;

        public KeepAliveService(IServiceProvider services) => _services = services;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (KeepAliveController.IsEnabled())
                {
                    try
                    {
                        using var scope = _services.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken: stoppingToken);
                        KeepAliveController.RecordPing();
                    }
                    catch
                    {
                        // Ignore transient failures; service will retry.
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }
    }
}
