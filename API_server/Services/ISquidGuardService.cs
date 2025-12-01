using SquidManagerAPI.Models;

namespace SquidManagerAPI.Services
{
    public interface ISquidGuardService
    {
        Task<bool> UpdateBlacklistAsync(Blacklist blacklist);
        Task<bool> RemoveFromBlacklistAsync(string category, string domain);
        Task<List<Blacklist>> GetBlacklistsAsync();
        Task<bool> ReloadSquidGuardAsync();
        Task<bool> CompileBlacklistsAsync();

        // Новые методы для работы с конфигурацией
        Task<SquidGuardConfig> GetSquidGuardConfigAsync();
        Task<bool> UpdateSquidGuardConfigAsync(SquidGuardConfig config);
        Task<bool> UpdateSquidGuardConfigPartialAsync(Dictionary<string, string> configUpdates);

        // Методы для работы с правилами доступа
        Task<List<AccessRuleSquidGuard>> GetAccessRulesAsync();
        Task<bool> AddAccessRuleAsync(AccessRuleSquidGuard rule);
        Task<bool> UpdateAccessRuleAsync(AccessRuleSquidGuard rule);
        Task<bool> RemoveAccessRuleAsync(int ruleId);

        Task<List<string>> GetSquidGuardLogsAsync(int lines = 100);
        Task<SquidGuardStatus> GetSquidGuardStatusAsync();
    }
}