using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;

namespace CSharpAssistant.API.Controllers
{
    [Route("api/code")]
    [ApiController]
    public class CodeExecutionController : ControllerBase
    {
        [HttpPost("execute")]
        public IActionResult ExecutarAlgumaCoisa([FromBody] CodeRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
            {
                return BadRequest(new { error = "Código C# não pode estar vazio!" });
            }

            try
            {
                string result = RunCode(request.Code);
                return Ok(new { output = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Erro ao executar o código: {ex.Message}" });
            }
        }

        private string RunCode(string code)
        {
            string dockerCommand = $"docker run --rm mcr.microsoft.com/dotnet/sdk:8.0 sh -c \"echo '{code}' | dotnet script\"";

            using (Process process = new Process())
            {
                process.StartInfo.FileName = "/bin/bash";
                process.StartInfo.Arguments = $"-c \"{dockerCommand}\"";
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                return string.IsNullOrEmpty(error) ? output : $"Erro: {error}";
            }
        }
    }

    public class CodeRequest
    {
        public required string Code { get; set; }
    }
}
