// Services/SquidService.cs
using System.Diagnostics;
using System.Text;
using SquidManagerAPI.Models;

namespace SquidManagerAPI.Services
{
    public class SquidService : ISquidService
    {
        private readonly ILogger<SquidService> _logger;
        private readonly IConfiguration _configuration;

        // Пути для Debian
        private readonly string _squidConfigPath = "/etc/squid/squid.conf";
        private readonly string _squidConfigDir = "/etc/squid/conf.d/";
        private readonly string _accessLogPath = "/var/log/squid/access.log";
        private readonly string _cacheLogPath = "/var/log/squid/cache.log";
        private readonly string _squidGuardConfigPath = "/etc/squidguard/squidGuard.conf";
        private readonly string _blacklistsPath = "/var/lib/squidguard/db/";

        public SquidService(ILogger<SquidService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<SquidStatus> GetStatusAsync()
        {
            try
            {
                var status = new SquidStatus();
                
                // Проверяем статус службы через systemctl
                var systemctlResult = await ExecuteBashCommand("systemctl is-active squid");
                status.IsRunning = systemctlResult.Output?.Trim() == "active";
                status.ServiceStatus = systemctlResult.Output?.Trim() ?? "unknown";

                // Получаем детальную информацию о службе
                var serviceStatus = await ExecuteBashCommand("systemctl status squid");
                if (serviceStatus.Success)
                {
                    var lines = serviceStatus.Output?.Split('\n');
                    if (lines != null)
                    {
                        foreach (var line in lines)
                        {
                            if (line.Contains("Memory:"))
                            {
                                var memoryLine = line.Trim();
                                // Парсим использование памяти
                                if (memoryLine.Contains("Memory:"))
                                {
                                    var memoryParts = memoryLine.Split("Memory:")[1].Trim().Split(' ');
                                    if (memoryParts.Length > 0 && long.TryParse(memoryParts[0], out long memory))
                                    {
                                        status.MemoryUsage = memory;
                                    }
                                }
                            }
                        }
                    }
                }

                // Получаем версию Squid
                var versionResult = await ExecuteBashCommand("squid -v");
                if (versionResult.Success && !string.IsNullOrEmpty(versionResult.Output))
                {
                    var versionLine = versionResult.Output.Split('\n').FirstOrDefault();
                    status.Version = versionLine?.Trim() ?? "Unknown";
                }

                // Получаем время работы
                var uptimeResult = await ExecuteBashCommand("systemctl show squid --property=ActiveEnterTimestamp");
                if (uptimeResult.Success && !string.IsNullOrEmpty(uptimeResult.Output))
                {
                    var timestampLine = uptimeResult.Output.Split('=').LastOrDefault();
                    if (DateTime.TryParse(timestampLine, out var startTime))
                    {
                        status.Uptime = DateTime.Now - startTime;
                    }
                }

                // Получаем количество активных соединений
                var connectionsResult = await ExecuteBashCommand("netstat -an | grep :3128 | grep ESTABLISHED | wc -l");
                if (connectionsResult.Success && int.TryParse(connectionsResult.Output?.Trim(), out int connections))
                {
                    status.ActiveConnections = connections;
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Squid status");
                throw;
            }
        }

        public async Task<bool> StartSquidAsync()
        {
            var result = await ExecuteBashCommand("systemctl start squid");
            if (!result.Success)
            {
                _logger.LogError("Failed to start Squid: {Error}", result.Error);
            }
            return result.Success;
        }

        public async Task<bool> StopSquidAsync()
        {
            var result = await ExecuteBashCommand("systemctl stop squid");
            if (!result.Success)
            {
                _logger.LogError("Failed to stop Squid: {Error}", result.Error);
            }
            return result.Success;
        }

        public async Task<bool> RestartSquidAsync()
        {
            var result = await ExecuteBashCommand("systemctl restart squid");
            if (!result.Success)
            {
                _logger.LogError("Failed to restart Squid: {Error}", result.Error);
            }
            return result.Success;
        }

        public async Task<bool> ReloadConfigAsync()
        {
            // Сначала проверяем конфигурацию
            var testResult = await TestConfigAsync();
            if (testResult != "OK")
            {
                _logger.LogError("Configuration test failed: {Error}", testResult);
                return false;
            }

            var result = await ExecuteBashCommand("systemctl reload squid");
            if (!result.Success)
            {
                _logger.LogError("Failed to reload Squid configuration: {Error}", result.Error);
            }
            return result.Success;
        }

        public async Task<string> TestConfigAsync()
        {
            var result = await ExecuteBashCommand("squid -k parse");
            if (result.Success)
            {
                return "OK";
            }
            else
            {
                return result.Error ?? "Configuration test failed";
            }
        }

        public async Task<List<SquidConfig>> GetConfigAsync()
        {
            var configs = new List<SquidConfig>();
            
            if (File.Exists(_squidConfigPath))
            {
                var lines = await File.ReadAllLinesAsync(_squidConfigPath);
                int id = 1;
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (!string.IsNullOrEmpty(trimmedLine))
                    {
                        // Пропускаем комментарии, но сохраняем их как описание
                        if (trimmedLine.StartsWith("#"))
                        {
                            // Это комментарий, можем использовать для следующей конфигурации
                            continue;
                        }
                        
                        var parts = trimmedLine.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            configs.Add(new SquidConfig
                            {
                                Id = id++,
                                Name = parts[0],
                                Value = parts[1],
                                Description = string.Empty
                            });
                        }
                    }
                }
            }
            
            return configs;
        }

        public async Task<bool> UpdateConfigAsync(List<SquidConfig> configs)
        {
            try
            {
                var lines = new List<string>();
                lines.Add("# Squid configuration file - Generated by API");
                lines.Add("# Debian Squid Manager API");
                lines.Add("# Updated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                lines.Add("");
                
                foreach (var config in configs)
                {
                    var line = $"{config.Name} {config.Value}";
                    if (!string.IsNullOrEmpty(config.Description))
                    {
                        line += $" # {config.Description}";
                    }
                    lines.Add(line);
                }
                
                // Создаем backup
                if (File.Exists(_squidConfigPath))
                {
                    var backupPath = _squidConfigPath + ".backup." + DateTime.Now.ToString("yyyyMMddHHmmss");
                    await ExecuteBashCommand($"cp {_squidConfigPath} {backupPath}");
                }
                
                await File.WriteAllLinesAsync(_squidConfigPath, lines);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Squid configuration");
                return false;
            }
        }

        public async Task<List<string>> GetAccessLogsAsync(int lines = 100)
        {
            return await GetLogLines(_accessLogPath, lines);
        }

        public async Task<List<string>> GetCacheLogsAsync(int lines = 100)
        {
            return await GetLogLines(_cacheLogPath, lines);
        }

        public async Task<SystemInfo> GetSystemInfoAsync()
        {
            var info = new SystemInfo
            {
                ServerTime = DateTime.Now
            };

            // Информация об ОС
            var osInfo = await ExecuteBashCommand("cat /etc/os-release | grep PRETTY_NAME");
            if (osInfo.Success)
            {
                info.OSVersion = osInfo.Output?.Split('=').LastOrDefault()?.Trim('\"') ?? "Debian";
            }

            // Версия ядра
            var kernelInfo = await ExecuteBashCommand("uname -r");
            if (kernelInfo.Success)
            {
                info.KernelVersion = kernelInfo.Output?.Trim() ?? "Unknown";
            }

            // Версия Squid
            var squidVersion = await ExecuteBashCommand("squid -v | head -1");
            if (squidVersion.Success)
            {
                info.SquidVersion = squidVersion.Output?.Trim() ?? "Unknown";
            }

            // Версия SquidGuard
            var sgVersion = await ExecuteBashCommand("squidguard -v 2>/dev/null | head -1 || echo \"Not installed\"");
            if (sgVersion.Success)
            {
                info.SquidGuardVersion = sgVersion.Output?.Trim() ?? "Not installed";
            }

            return info;
        }

        private async Task<List<string>> GetLogLines(string logPath, int lines)
        {
            if (!File.Exists(logPath))
                return new List<string> { $"Log file not found: {logPath}" };

            try
            {
                var result = await ExecuteBashCommand($"tail -n {lines} {logPath}");
                if (result.Success && !string.IsNullOrEmpty(result.Output))
                {
                    return result.Output.Split('\n').Where(line => !string.IsNullOrEmpty(line)).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading log file: {LogPath}", logPath);
            }

            return new List<string> { "Error reading log file" };
        }

        private async Task<(bool Success, string? Output, string? Error)> ExecuteBashCommand(string command)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{command}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing bash command: {Command}", command);
                return (false, null, ex.Message);
            }
        }
    }
}