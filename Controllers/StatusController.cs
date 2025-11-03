using Microsoft.AspNetCore.Mvc;
using CSharpAssistant.API.Data;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace CSharpAssistant.API.Models
{
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StatusController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus()
        {
            var dbOk = await _context.Database.CanConnectAsync();
            return Ok(new
            {
                message = "🟢 API e-commerce está rodando!",
                dbStatus = dbOk ? "🟢 DB conectado" : "🔴 DB com erro"
            });
        }

        [HttpGet("isOpen")]
        public async Task<IActionResult> GetIsOpen([FromServices] Data.AppDbContext db)
        {
            var setting = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
            if (setting == null)
            {
                // Sem settings configurado: considera aberto.
                return Ok(new { isOpen = true, message = "Sem configuração. Considerado aberto." });
            }

            // Timezone
            var tzId = string.IsNullOrWhiteSpace(setting.TimeZone) ? "America/Sao_Paulo" : setting.TimeZone;
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }

            var nowUtc = DateTime.UtcNow;
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
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

            // Parse OpeningHours
            var hours = ParseOpeningHours(setting.OpeningHoursJson);
            var exceptions = ParseExceptions(setting.ExceptionsJson);

            // Exceptions por data têm precedência
            var exc = exceptions.FirstOrDefault(e => e.Date == dateKey);
            if (exc != null)
            {
                if (exc.Closed == true)
                {
                    var next = FindNextOpening(nowLocal, tz, hours, exceptions);
                    return Ok(new
                    {
                        isOpen = false,
                        message = "Fechado hoje por exceção.",
                        now = nowLocal.ToString("yyyy-MM-dd HH:mm"),
                        nextOpening = next?.ToString("yyyy-MM-dd HH:mm")
                    });
                }
                if (exc.Ranges?.Count > 0)
                {
                    var open = IsWithinRanges(nowLocal, exc.Ranges);
                    var next = open ? null : FindNextOpening(nowLocal, tz, hours, exceptions);
                    return Ok(new
                    {
                        isOpen = open,
                        message = open ? "Aberto por faixa excepcional." : "Fora de faixa excepcional de hoje.",
                        now = nowLocal.ToString("yyyy-MM-dd HH:mm"),
                        nextOpening = next?.ToString("yyyy-MM-dd HH:mm")
                    });
                }
            }

            // Regra semanal padrão
            hours.TryGetValue(dowKey, out var ranges);
            var isOpen = IsWithinRanges(nowLocal, ranges ?? new List<TimeRange>());
            var nextOpeningDefault = isOpen ? null : FindNextOpening(nowLocal, tz, hours, exceptions);

            return Ok(new
            {
                isOpen,
                message = isOpen ? "Aberto" : "Fechado",
                now = nowLocal.ToString("yyyy-MM-dd HH:mm"),
                nextOpening = nextOpeningDefault?.ToString("yyyy-MM-dd HH:mm")
            });
        }

        // ===== Helpers locais =====
        private static bool IsWithinRanges(DateTime localNow, List<TimeRange> ranges)
        {
            if (ranges == null || ranges.Count == 0) return false;
            foreach (var tr in ranges)
            {
                if (!TryParseTime(localNow.Date, tr.Start, out var start)) continue;
                if (!TryParseTime(localNow.Date, tr.End, out var end)) continue;

                // Suporta faixa que cruza meia-noite: start > end
                if (start <= end)
                {
                    if (localNow >= start && localNow <= end) return true;
                }
                else
                {
                    // Ex.: 22:00-02:00
                    if (localNow >= start || localNow <= end) return true;
                }
            }
            return false;
        }

        private static bool TryParseTime(DateTime date, string hhmm, out DateTime result)
        {
            result = date;
            if (string.IsNullOrWhiteSpace(hhmm)) return false;
            var parts = hhmm.Split(':');
            if (parts.Length != 2) return false;
            if (!int.TryParse(parts[0], out var h)) return false;
            if (!int.TryParse(parts[1], out var m)) return false;
            result = date.Date.AddHours(h).AddMinutes(m);
            return true;
        }

        private static DateTime? FindNextOpening(
            DateTime baseLocal,
            TimeZoneInfo tz,
            Dictionary<string, List<TimeRange>> hours,
            List<ExceptionDay> exceptions)
        {
            // Varre próximos 14 dias no máximo
            for (int d = 0; d < 14; d++)
            {
                var day = baseLocal.Date.AddDays(d);
                var key = day.DayOfWeek switch
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

                // Exceção do dia
                var exc = exceptions.FirstOrDefault(e => e.Date == day.ToString("yyyy-MM-dd"));
                var ranges = exc?.Closed == true
                    ? new List<TimeRange>()
                    : (exc?.Ranges?.Count > 0 ? exc.Ranges : (hours.TryGetValue(key, out var dayRanges) ? dayRanges : new List<TimeRange>()));

                foreach (var tr in ranges)
                {
                    if (!TryParseTime(day, tr.Start, out var start)) continue;
                    if (!TryParseTime(day, tr.End, out var end)) continue;
                    if (d == 0 && baseLocal <= end && baseLocal <= start)
                    {
                        return start;
                    }
                    if (d > 0)
                    {
                        return start;
                    }
                }
            }
            return null;
        }

        private static Dictionary<string, List<TimeRange>> ParseOpeningHours(string json)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<Dictionary<string, List<TimeRange>>>(string.IsNullOrWhiteSpace(json) ? "{}" : json, options)
                       ?? new Dictionary<string, List<TimeRange>>();
            }
            catch { return new Dictionary<string, List<TimeRange>>(); }
        }

        private static List<ExceptionDay> ParseExceptions(string json)
        {
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<List<ExceptionDay>>(string.IsNullOrWhiteSpace(json) ? "[]" : json, options)
                       ?? new List<ExceptionDay>();
            }
            catch { return new List<ExceptionDay>(); }
        }

        // Tipos auxiliares
        private class TimeRange
        {
            [JsonPropertyName("start")] public string Start { get; set; } = "00:00";
            [JsonPropertyName("end")] public string End { get; set; } = "00:00";
        }
        private class ExceptionDay
        {
            [JsonPropertyName("date")] public string Date { get; set; } = ""; // yyyy-MM-dd
            [JsonPropertyName("closed")] public bool? Closed { get; set; }
            [JsonPropertyName("ranges")] public List<TimeRange>? Ranges { get; set; }
        }
    }
}
