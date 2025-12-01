// Controllers/SquidController.cs
using Microsoft.AspNetCore.Mvc;
using SquidManagerAPI.Models;
using SquidManagerAPI.Services;

namespace SquidManagerAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SquidController : ControllerBase
    {
        private readonly ISquidService _squidService;
        private readonly ILogger<SquidController> _logger;

        public SquidController(ISquidService squidService, ILogger<SquidController> logger)
        {
            _squidService = squidService;
            _logger = logger;
        }

        /// <summary>
        /// Получить статус Squid прокси
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult<SquidStatus>> GetStatus()
        {
            try
            {
                var status = await _squidService.GetStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Squid status");
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Запустить Squid
        /// </summary>
        [HttpPost("start")]
        public async Task<ActionResult> StartSquid()
        {
            try
            {
                var result = await _squidService.StartSquidAsync();
                return result ? Ok(new { message = "Squid started successfully" }) 
                             : BadRequest(new { error = "Failed to start Squid" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Squid");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Остановить Squid
        /// </summary>
        [HttpPost("stop")]
        public async Task<ActionResult> StopSquid()
        {
            try
            {
                var result = await _squidService.StopSquidAsync();
                return result ? Ok(new { message = "Squid stopped successfully" }) 
                             : BadRequest(new { error = "Failed to stop Squid" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Squid");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Перезапустить Squid
        /// </summary>
        [HttpPost("restart")]
        public async Task<ActionResult> RestartSquid()
        {
            try
            {
                var result = await _squidService.RestartSquidAsync();
                return result ? Ok(new { message = "Squid restarted successfully" }) 
                             : BadRequest(new { error = "Failed to restart Squid" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting Squid");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Перезагрузить конфигурацию Squid
        /// </summary>
        [HttpPost("reload")]
        public async Task<ActionResult> ReloadConfig()
        {
            try
            {
                var result = await _squidService.ReloadConfigAsync();
                return result ? Ok(new { message = "Configuration reloaded successfully" }) 
                             : BadRequest(new { error = "Failed to reload configuration" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading Squid configuration");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Получить конфигурацию Squid
        /// </summary>
        [HttpGet("config")]
        public async Task<ActionResult<List<SquidConfig>>> GetConfig()
        {
            try
            {
                var config = await _squidService.GetConfigAsync();
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Squid configuration");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Обновить конфигурацию Squid
        /// </summary>
        [HttpPut("config")]
        public async Task<ActionResult> UpdateConfig([FromBody] List<SquidConfig> configs)
        {
            try
            {
                var result = await _squidService.UpdateConfigAsync(configs);
                return result ? Ok(new { message = "Configuration updated successfully" }) 
                             : BadRequest(new { error = "Failed to update configuration" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Squid configuration");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Получить логи доступа
        /// </summary>
        /// <param name="lines">Количество строк для отображения (по умолчанию 100)</param>
        [HttpGet("logs/access")]
        public async Task<ActionResult<List<string>>> GetAccessLogs([FromQuery] int lines = 100)
        {
            try
            {
                var logs = await _squidService.GetAccessLogsAsync(lines);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access logs");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Получить логи кэша
        /// </summary>
        /// <param name="lines">Количество строк для отображения (по умолчанию 100)</param>
        [HttpGet("logs/cache")]
        public async Task<ActionResult<List<string>>> GetCacheLogs([FromQuery] int lines = 100)
        {
            try
            {
                var logs = await _squidService.GetCacheLogsAsync(lines);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache logs");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}