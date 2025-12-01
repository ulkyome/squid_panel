// Controllers/SystemController.cs
using Microsoft.AspNetCore.Mvc;
using SquidManagerAPI.Models;
using SquidManagerAPI.Services;

namespace SquidManagerAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SystemController : ControllerBase
    {
        private readonly ISquidService _squidService;
        private readonly ILogger<SystemController> _logger;

        public SystemController(ISquidService squidService, ILogger<SystemController> logger)
        {
            _squidService = squidService;
            _logger = logger;
        }

        /// <summary>
        /// Получить системную информацию
        /// </summary>
        [HttpGet("info")]
        public async Task<ActionResult<SystemInfo>> GetSystemInfo()
        {
            try
            {
                var info = await _squidService.GetSystemInfoAsync();
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system info");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Проверить конфигурацию Squid
        /// </summary>
        [HttpGet("test-config")]
        public async Task<ActionResult> TestConfig()
        {
            try
            {
                var result = await _squidService.TestConfigAsync();
                return Ok(new { status = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing configuration");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}