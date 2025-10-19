using System;
using Microsoft.AspNetCore.Mvc;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class KeepAliveController : ControllerBase
    {
        private static bool _enabled;
        private static DateTime _lastPing = DateTime.MinValue;

        [HttpGet("status")]
        public IActionResult GetStatus()
            => Ok(new { enabled = _enabled, lastPing = _lastPing });

        [HttpPost("enable")]
        public IActionResult Enable()
        {
            _enabled = true;
            return Ok(new { message = "KeepAlive ativado" });
        }

        [HttpPost("disable")]
        public IActionResult Disable()
        {
            _enabled = false;
            return Ok(new { message = "KeepAlive desativado" });
        }

        public static bool IsEnabled() => _enabled;
        public static void RecordPing() => _lastPing = DateTime.UtcNow;
    }
}
