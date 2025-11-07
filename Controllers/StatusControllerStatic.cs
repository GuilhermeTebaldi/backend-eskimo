using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CSharpAssistant.API.Models
{
    public static class StatusControllerStatic
    {
        public class StatusResponse
        {
            [JsonPropertyName("isOpen")] public bool IsOpen { get; set; }
            [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
            [JsonPropertyName("now")] public string Now { get; set; } = string.Empty;
            [JsonPropertyName("nextOpening")] public string? NextOpening { get; set; }
        }

        private class TimeRange
        {
            [JsonPropertyName("start")] public string Start { get; set; } = "00:00";
            [JsonPropertyName("end")] public string End { get; set; } = "00:00";
        }

        private class ExceptionDay
        {
            [JsonPropertyName("date")] public string Date { get; set; } = "";
            [JsonPropertyName("closed")] public bool? Closed { get; set; }
            [JsonPropertyName("ranges")] public List<TimeRange>? Ranges { get; set; }
        }

        public static StatusResponse EvaluateStatus(string? timeZone, string? openingHoursJson, string? exceptionsJson)
        {
            var tzId = string.IsNullOrWhiteSpace(timeZone) ? "America/Sao_Paulo" : timeZone!;
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { tz = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }

            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var dateKey = nowLocal.ToString("yyyy-MM-dd");
            var hoursJson = openingHoursJson ?? "{}";
            var exceptionsJsonSafe = exceptionsJson ?? "[]";

            var isEmptyHours = string.IsNullOrWhiteSpace(hoursJson) || hoursJson.Trim() == "{}";
            var isEmptyExceptions = string.IsNullOrWhiteSpace(exceptionsJsonSafe) || exceptionsJsonSafe.Trim() == "[]";
            if (isEmptyHours && isEmptyExceptions)
            {
                return new StatusResponse
                {
                    IsOpen = true,
                    Message = "Sem configuração. Considerado aberto.",
                    Now = nowLocal.ToString("yyyy-MM-dd HH:mm")
                };
            }

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

            var hours = ParseOpeningHours(hoursJson);
            var exceptions = ParseExceptions(exceptionsJsonSafe);

            var response = new StatusResponse
            {
                Now = nowLocal.ToString("yyyy-MM-dd HH:mm")
            };

            var exc = exceptions.FirstOrDefault(e => e.Date == dateKey);
            if (exc != null)
            {
                if (exc.Closed == true)
                {
                    var next = FindNextOpening(nowLocal, tz, hours, exceptions);
                    response.IsOpen = false;
                    response.Message = "Fechado hoje por exceção.";
                    response.NextOpening = next?.ToString("yyyy-MM-dd HH:mm");
                    return response;
                }

                if (exc.Ranges?.Count > 0)
                {
                    var open = IsWithinRanges(nowLocal, exc.Ranges);
                    response.IsOpen = open;
                    response.Message = open
                        ? "Aberto por faixa excepcional."
                        : "Fora de faixa excepcional de hoje.";
                    if (!open)
                    {
                        var next = FindNextOpening(nowLocal, tz, hours, exceptions);
                        response.NextOpening = next?.ToString("yyyy-MM-dd HH:mm");
                    }
                    return response;
                }
            }

            hours.TryGetValue(dowKey, out var ranges);
            var openDefault = IsWithinRanges(nowLocal, ranges ?? new List<TimeRange>());
            response.IsOpen = openDefault;
            response.Message = openDefault ? "Aberto" : "Fechado";
            if (!openDefault)
            {
                var next = FindNextOpening(nowLocal, tz, hours, exceptions);
                response.NextOpening = next?.ToString("yyyy-MM-dd HH:mm");
            }

            return response;
        }

        private static bool IsWithinRanges(DateTime localNow, List<TimeRange> ranges)
        {
            if (ranges == null || ranges.Count == 0) return false;
            foreach (var tr in ranges)
            {
                if (!TryParseTime(localNow.Date, tr.Start, out var start)) continue;
                if (!TryParseTime(localNow.Date, tr.End, out var end)) continue;

                if (start <= end)
                {
                    if (localNow >= start && localNow <= end) return true;
                }
                else
                {
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
    }
}
