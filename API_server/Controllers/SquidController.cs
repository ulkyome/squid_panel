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
        /// Получить статус Squid сервиса
        /// </summary>
        [HttpGet("status")]
        [ProducesResponseType(typeof(SquidStatus), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetStatus()
        {
            try
            {
                var status = await _squidService.GetStatusAsync();
                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Squid status");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Запустить Squid сервис
        /// </summary>
        [HttpPost("start")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StartSquid()
        {
            try
            {
                var result = await _squidService.StartSquidAsync();
                return result ? Ok(new { message = "Squid started successfully" })
                             : StatusCode(500, new { error = "Failed to start Squid" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting Squid");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Остановить Squid сервис
        /// </summary>
        [HttpPost("stop")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> StopSquid()
        {
            try
            {
                var result = await _squidService.StopSquidAsync();
                return result ? Ok(new { message = "Squid stopped successfully" })
                             : StatusCode(500, new { error = "Failed to stop Squid" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping Squid");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Перезапустить Squid сервис
        /// </summary>
        [HttpPost("restart")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> RestartSquid()
        {
            try
            {
                var result = await _squidService.RestartSquidAsync();
                return result ? Ok(new { message = "Squid restarted successfully" })
                             : StatusCode(500, new { error = "Failed to restart Squid" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restarting Squid");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Перезагрузить конфигурацию Squid
        /// </summary>
        [HttpPost("reload")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ReloadConfig()
        {
            try
            {
                var result = await _squidService.ReloadConfigAsync();
                return result ? Ok(new { message = "Configuration reloaded successfully" })
                             : StatusCode(500, new { error = "Failed to reload configuration" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading configuration");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Проверить конфигурацию Squid
        /// </summary>
        [HttpGet("test-config")]
        [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> TestConfig()
        {
            try
            {
                var result = await _squidService.TestConfigAsync();
                return Ok(new { result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing configuration");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Получить текущую конфигурацию Squid
        /// </summary>
        [HttpGet("config")]
        [ProducesResponseType(typeof(List<SquidConfig>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetConfig()
        {
            try
            {
                var config = await _squidService.GetConfigAsync();
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting configuration");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Обновить конфигурацию Squid
        /// </summary>
        [HttpPut("config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateConfig([FromBody] List<SquidConfig> configs)
        {
            try
            {
                if (configs == null || configs.Count == 0)
                {
                    return BadRequest(new { error = "Configuration data is required" });
                }

                var result = await _squidService.UpdateConfigAsync(configs);
                return result ? Ok(new { message = "Configuration updated successfully" })
                             : StatusCode(500, new { error = "Failed to update configuration" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Получить логи доступа
        /// </summary>
        /// <param name="lines">Количество строк (по умолчанию 100)</param>
        [HttpGet("logs/access")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAccessLogs([FromQuery] int lines = 100)
        {
            try
            {
                var logs = await _squidService.GetAccessLogsAsync(lines);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access logs");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Получить логи кэша
        /// </summary>
        /// <param name="lines">Количество строк (по умолчанию 100)</param>
        [HttpGet("logs/cache")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetCacheLogs([FromQuery] int lines = 100)
        {
            try
            {
                var logs = await _squidService.GetCacheLogsAsync(lines);
                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache logs");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Получить информацию о системе
        /// </summary>
        [HttpGet("system/info")]
        [ProducesResponseType(typeof(SystemInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSystemInfo()
        {
            try
            {
                var info = await _squidService.GetSystemInfoAsync();
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting system info");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Получить список черных списков SquidGuard
        /// </summary>
        [HttpGet("squidguard/blacklists")]
        [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSquidGuardBlacklists()
        {
            try
            {
                var blacklists = await _squidService.GetSquidGuardBlacklistsAsync();
                return Ok(blacklists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SquidGuard blacklists");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Обновить черные списки SquidGuard
        /// </summary>
        [HttpPost("squidguard/update")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateSquidGuard()
        {
            try
            {
                var result = await _squidService.UpdateSquidGuardAsync();
                return result ? Ok(new { message = "SquidGuard updated successfully" })
                             : StatusCode(500, new { error = "Failed to update SquidGuard" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating SquidGuard");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }

        /// <summary>
        /// Получить статистику SquidGuard
        /// </summary>
        [HttpGet("squidguard/stats")]
        [ProducesResponseType(typeof(Dictionary<string, string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetSquidGuardStats()
        {
            try
            {
                var stats = await _squidService.GetSquidGuardStatsAsync();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SquidGuard stats");
                return StatusCode(500, new { error = "Internal server error", message = ex.Message });
            }
        }
    }
}