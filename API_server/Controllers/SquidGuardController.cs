// Controllers/SquidGuardController.cs
using Microsoft.AspNetCore.Mvc;
using SquidManagerAPI.Models;
using SquidManagerAPI.Services;

namespace SquidManagerAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SquidGuardController : ControllerBase
    {
        private readonly ISquidGuardService _squidGuardService;
        private readonly ILogger<SquidGuardController> _logger;

        public SquidGuardController(ISquidGuardService squidGuardService, ILogger<SquidGuardController> logger)
        {
            _squidGuardService = squidGuardService;
            _logger = logger;
        }

        /// <summary>
        /// Получить все черные списки
        /// </summary>
        [HttpGet("blacklists")]
        public async Task<ActionResult<List<Blacklist>>> GetBlacklists()
        {
            try
            {
                var blacklists = await _squidGuardService.GetBlacklistsAsync();
                return Ok(blacklists);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blacklists");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Обновить черный список
        /// </summary>
        [HttpPut("blacklists")]
        public async Task<ActionResult> UpdateBlacklist([FromBody] Blacklist blacklist)
        {
            try
            {
                var result = await _squidGuardService.UpdateBlacklistAsync(blacklist);
                return result ? Ok(new { message = "Blacklist updated successfully" }) 
                             : BadRequest(new { error = "Failed to update blacklist" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating blacklist");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Удалить домен из черного списка
        /// </summary>
        [HttpDelete("blacklists/{category}/{domain}")]
        public async Task<ActionResult> RemoveFromBlacklist(string category, string domain)
        {
            try
            {
                var result = await _squidGuardService.RemoveFromBlacklistAsync(category, domain);
                return result ? Ok(new { message = "Domain removed from blacklist successfully" }) 
                             : BadRequest(new { error = "Failed to remove domain from blacklist" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing domain from blacklist");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Перезагрузить SquidGuard
        /// </summary>
        [HttpPost("reload")]
        public async Task<ActionResult> ReloadSquidGuard()
        {
            try
            {
                var result = await _squidGuardService.ReloadSquidGuardAsync();
                return result ? Ok(new { message = "SquidGuard reloaded successfully" }) 
                             : BadRequest(new { error = "Failed to reload SquidGuard" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading SquidGuard");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Получить правила доступа
        /// </summary>
        [HttpGet("rules")]
        public async Task<ActionResult<List<AccessRule>>> GetAccessRules()
        {
            try
            {
                var rules = await _squidGuardService.GetAccessRulesAsync();
                return Ok(rules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting access rules");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Добавить правило доступа
        /// </summary>
        [HttpPost("rules")]
        public async Task<ActionResult> AddAccessRule([FromBody] AccessRuleSquidGuard rule)
        {
            try
            {
                var result = await _squidGuardService.AddAccessRuleAsync(rule);
                return result ? Ok(new { message = "Access rule added successfully" }) 
                             : BadRequest(new { error = "Failed to add access rule" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding access rule");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Удалить правило доступа
        /// </summary>
        [HttpDelete("rules/{id}")]
        public async Task<ActionResult> RemoveAccessRule(int id)
        {
            try
            {
                var result = await _squidGuardService.RemoveAccessRuleAsync(id);
                return result ? Ok(new { message = "Access rule removed successfully" }) 
                             : BadRequest(new { error = "Failed to remove access rule" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing access rule");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }
    }
}