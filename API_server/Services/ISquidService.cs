// Services/ISquidService.cs
using SquidManagerAPI.Models;

namespace SquidManagerAPI.Services
{
    public interface ISquidService
    {
        Task<SquidStatus> GetStatusAsync();
        Task<bool> StartSquidAsync();
        Task<bool> StopSquidAsync();
        Task<bool> RestartSquidAsync();
        Task<bool> ReloadConfigAsync();
        Task<List<SquidConfig>> GetConfigAsync();
        Task<bool> UpdateConfigAsync(List<SquidConfig> configs);
        Task<List<string>> GetAccessLogsAsync(int lines = 100);
        Task<List<string>> GetCacheLogsAsync(int lines = 100);
        Task<SystemInfo> GetSystemInfoAsync();
        Task<string> TestConfigAsync();
    }

    public interface ISquidGuardService
    {
        Task<bool> UpdateBlacklistAsync(Blacklist blacklist);
        Task<bool> RemoveFromBlacklistAsync(string category, string domain);
        Task<List<Blacklist>> GetBlacklistsAsync();
        Task<bool> ReloadSquidGuardAsync();
        Task<List<AccessRule>> GetAccessRulesAsync();
        Task<bool> AddAccessRuleAsync(AccessRule rule);
        Task<bool> RemoveAccessRuleAsync(int ruleId);
        Task<bool> CompileBlacklistsAsync();
        Task<List<string>> GetSquidGuardLogsAsync(int lines = 100);
    }
}