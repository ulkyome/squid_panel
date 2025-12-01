using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using SquidManagerAPI.Models;

namespace SquidManagerAPI.Services
{
    public class SquidService : ISquidService
    {
        private readonly ILogger<SquidService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(1);

        private readonly string _squidConfigPath = "/etc/squid/squid.conf";
        private readonly string _squidConfigDir = "/etc/squid/conf.d/";
        private readonly string _accessLogPath = "/var/log/squid/access.log";
        private readonly string _cacheLogPath = "/var/log/squid/cache.log";
        private readonly string _squidGuardConfigPath = "/etc/squidguard/squidGuard.conf";
        private readonly string _blacklistsPath = "/var/lib/squidguard/db/";

        private const string CACHE_KEY_SYSTEM_INFO = "SystemInfo";
        private const string CACHE_KEY_STATUS = "SquidStatus";
        private const string CACHE_KEY_CONFIG = "SquidConfig";

        public SquidService(ILogger<SquidService> logger, IConfiguration configuration, IMemoryCache cache)
        {
            _logger = logger;
            _configuration = configuration;
            _cache = cache;
        }

        public async Task<SquidStatus> GetStatusAsync()
        {
            try
            {
                if (_cache.TryGetValue(CACHE_KEY_STATUS, out SquidStatus cachedStatus))
                {
                    return cachedStatus;
                }

                var status = new SquidStatus();

                var tasks = new[]
                {
                    ExecuteBashCommandAsync("systemctl is-active squid"),
                    ExecuteBashCommandAsync("systemctl status squid --no-pager"),
                    ExecuteBashCommandAsync("squid -v 2>/dev/null || echo 'Unknown'"),
                    ExecuteBashCommandAsync("systemctl show squid --property=ActiveEnterTimestamp --value"),
                    ExecuteBashCommandAsync("ss -tun state established '( sport = :3128 or dport = :3128 )' | wc -l")
                };

                var results = await Task.WhenAll(tasks);

                status.IsRunning = results[0].Success && results[0].Output?.Trim() == "active";
                status.ServiceStatus = results[0].Output?.Trim() ?? "unknown";

                if (results[1].Success && !string.IsNullOrEmpty(results[1].Output))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        results[1].Output,
                        @"Memory:\s*(\d+\.?\d*)([KMG])?B");
                    if (match.Success)
                    {
                        status.MemoryUsage = ParseMemoryUsage(match.Groups[1].Value, match.Groups[2].Value);
                    }
                }

                if (results[2].Success)
                {
                    status.Version = ExtractSquidVersion(results[2].Output);
                }

                if (results[3].Success && DateTime.TryParse(results[3].Output?.Trim(), out var startTime))
                {
                    status.Uptime = DateTime.Now - startTime;
                }

                if (results[4].Success && int.TryParse(results[4].Output?.Trim(), out int connections))
                {
                    status.ActiveConnections = connections;
                }

                // Получаем PID
                var pidResult = await ExecuteBashCommandAsync("systemctl show squid --property=MainPID --value");
                if (pidResult.Success && int.TryParse(pidResult.Output?.Trim(), out int pid) && pid > 0)
                {
                    status.ProcessId = pid;
                }

                // Получаем статистику кэша
                await UpdateCacheStats(status);

                _cache.Set(CACHE_KEY_STATUS, status, _cacheDuration);
                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Squid status");
                throw new ApplicationException("Failed to retrieve Squid status", ex);
            }
        }

        private async Task UpdateCacheStats(SquidStatus status)
        {
            try
            {
                var cacheResult = await ExecuteBashCommandAsync(
                    "squidclient -p 3128 mgr:info 2>/dev/null | " +
                    "grep -E '(cache size|store_entries|storeIOStats|Hits as % of all)' | " +
                    "head -10");

                if (cacheResult.Success && !string.IsNullOrEmpty(cacheResult.Output))
                {
                    var lines = cacheResult.Output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("store_entries"))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"store_entries\s*:\s*(\d+)");
                            if (match.Success)
                            {
                                status.CacheObjects = int.Parse(match.Groups[1].Value);
                            }
                        }
                        else if (line.Contains("Hits as % of all"))
                        {
                            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+\.\d+)%");
                            if (match.Success)
                            {
                                var hitRate = double.Parse(match.Groups[1].Value);
                                if (hitRate > 0)
                                {
                                    status.CacheHits = (int)(100 * hitRate / 100);
                                    status.CacheMisses = 100 - status.CacheHits;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get cache statistics");
            }
        }

        public async Task<bool> StartSquidAsync()
        {
            _cache.Remove(CACHE_KEY_STATUS);
            var result = await ExecuteBashCommandAsync("systemctl start squid");

            if (!result.Success)
            {
                _logger.LogError("Failed to start Squid. Error: {Error}", result.Error);
                throw new ApplicationException($"Failed to start Squid: {result.Error}");
            }

            await Task.Delay(1000);
            return result.Success;
        }

        public async Task<bool> StopSquidAsync()
        {
            _cache.Remove(CACHE_KEY_STATUS);
            var result = await ExecuteBashCommandAsync("systemctl stop squid");

            if (!result.Success)
            {
                _logger.LogError("Failed to stop Squid. Error: {Error}", result.Error);
                throw new ApplicationException($"Failed to stop Squid: {result.Error}");
            }

            return result.Success;
        }

        public async Task<bool> RestartSquidAsync()
        {
            _cache.Remove(CACHE_KEY_STATUS);
            var result = await ExecuteBashCommandAsync("systemctl restart squid");

            if (!result.Success)
            {
                _logger.LogError("Failed to restart Squid. Error: {Error}", result.Error);
                throw new ApplicationException($"Failed to restart Squid: {result.Error}");
            }

            await Task.Delay(2000);
            return result.Success;
        }

        public async Task<bool> ReloadConfigAsync()
        {
            var testResult = await TestConfigAsync();
            if (testResult != "OK")
            {
                var errorMessage = $"Configuration test failed: {testResult}";
                _logger.LogError(errorMessage);
                throw new ApplicationException(errorMessage);
            }

            _cache.Remove(CACHE_KEY_STATUS);
            _cache.Remove(CACHE_KEY_CONFIG);

            var result = await ExecuteBashCommandAsync("systemctl reload squid");

            if (!result.Success)
            {
                _logger.LogError("Failed to reload Squid configuration. Error: {Error}", result.Error);
                throw new ApplicationException($"Failed to reload configuration: {result.Error}");
            }

            return result.Success;
        }

        public async Task<string> TestConfigAsync()
        {
            var result = await ExecuteBashCommandAsync("squid -k parse 2>&1");

            if (result.Success)
            {
                return "OK";
            }

            var error = result.Error ?? "Unknown configuration error";

            var lines = error.Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line) &&
                              !line.Contains("WARNING:") &&
                              !line.Contains("Note:"))
                .Take(5)
                .ToList();

            return string.Join("; ", lines);
        }

        public async Task<List<SquidConfig>> GetConfigAsync()
        {
            if (_cache.TryGetValue(CACHE_KEY_CONFIG, out List<SquidConfig> cachedConfig))
            {
                return cachedConfig;
            }

            var configs = new List<SquidConfig>();

            try
            {
                if (!File.Exists(_squidConfigPath))
                {
                    _logger.LogWarning("Squid configuration file not found: {Path}", _squidConfigPath);
                    return configs;
                }

                var lines = await File.ReadAllLinesAsync(_squidConfigPath);
                var currentComment = new StringBuilder();
                int id = 1;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        currentComment.Clear();
                        continue;
                    }

                    if (trimmedLine.StartsWith("#"))
                    {
                        currentComment.AppendLine(trimmedLine.TrimStart('#').Trim());
                        continue;
                    }

                    var directive = ParseConfigLine(trimmedLine);
                    if (directive != null)
                    {
                        directive.Id = id++;
                        if (currentComment.Length > 0)
                        {
                            directive.Description = currentComment.ToString().Trim();
                            currentComment.Clear();
                        }
                        configs.Add(directive);
                    }
                }

                if (Directory.Exists(_squidConfigDir))
                {
                    var confFiles = Directory.GetFiles(_squidConfigDir, "*.conf");
                    foreach (var confFile in confFiles)
                    {
                        id = await ParseAdditionalConfigFile(confFile, configs, id);
                    }
                }

                _cache.Set(CACHE_KEY_CONFIG, configs, _cacheDuration);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Squid configuration");
                throw new ApplicationException("Failed to read Squid configuration", ex);
            }

            return configs;
        }

        public async Task<bool> UpdateConfigAsync(List<SquidConfig> configs)
        {
            try
            {
                _cache.Remove(CACHE_KEY_CONFIG);

                if (File.Exists(_squidConfigPath))
                {
                    var backupDir = "/var/backups/squid/";
                    Directory.CreateDirectory(backupDir);
                    var backupPath = Path.Combine(backupDir,
                        $"squid.conf.backup.{DateTime.Now:yyyyMMddHHmmss}");
                    await ExecuteBashCommandAsync($"cp {_squidConfigPath} {backupPath}");
                    _logger.LogInformation("Configuration backup created: {BackupPath}", backupPath);
                }

                var lines = new List<string>
                {
                    "# Squid configuration file - Generated by API",
                    "# Debian Squid Manager API",
                    $"# Updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    ""
                };

                string currentSection = "";
                foreach (var config in configs.OrderBy(c => c.Name))
                {
                    var section = GetConfigSection(config.Name);
                    if (section != currentSection && !string.IsNullOrEmpty(section))
                    {
                        lines.Add($"");
                        lines.Add($"# {section}");
                        currentSection = section;
                    }

                    var line = $"{config.Name} {config.Value}";
                    if (!string.IsNullOrEmpty(config.Description))
                    {
                        line += $" # {config.Description}";
                    }
                    lines.Add(line);
                }

                var tempPath = _squidConfigPath + ".tmp";
                await File.WriteAllLinesAsync(tempPath, lines, Encoding.UTF8);

                File.Move(tempPath, _squidConfigPath, true);

                _logger.LogInformation("Squid configuration updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating Squid configuration");
                throw new ApplicationException("Failed to update Squid configuration", ex);
            }
        }

        public async Task<List<string>> GetAccessLogsAsync(int lines)
        {
            return await GetLogLines(_accessLogPath, lines);
        }

        public async Task<List<string>> GetCacheLogsAsync(int lines)
        {
            return await GetLogLines(_cacheLogPath, lines);
        }

        public async Task<SystemInfo> GetSystemInfoAsync()
        {
            if (_cache.TryGetValue(CACHE_KEY_SYSTEM_INFO, out SystemInfo cachedInfo))
            {
                return cachedInfo;
            }

            var info = new SystemInfo
            {
                ServerTime = DateTime.Now,
                Uptime = await GetSystemUptimeAsync()
            };

            var tasks = new[]
            {
                ExecuteBashCommandAsync("cat /etc/os-release | grep PRETTY_NAME"),
                ExecuteBashCommandAsync("uname -r"),
                ExecuteBashCommandAsync("squid -v 2>/dev/null | head -1 || echo 'Not installed'"),
                ExecuteBashCommandAsync("which squidguard >/dev/null 2>&1 && squidguard -v 2>/dev/null | head -1 || echo 'Not installed'"),
                ExecuteBashCommandAsync("free -m | awk 'NR==2{printf \"%.1f/%.0f MB (%.1f%%)\", $3,$2,$3*100/$2 }'"),
                ExecuteBashCommandAsync("df -h / | awk 'NR==2{print $5}'"),
                ExecuteBashCommandAsync("grep -c ^processor /proc/cpuinfo"),
                ExecuteBashCommandAsync("uptime | awk -F'[a-z]:' '{print $2}' | xargs"),
                ExecuteBashCommandAsync("hostname"),
                ExecuteBashCommandAsync("hostname -I | awk '{print $1}'"),
                ExecuteBashCommandAsync("who -b | awk '{print $3 \" \" $4}'")
            };

            var results = await Task.WhenAll(tasks);

            info.OSVersion = results[0].Success ?
                results[0].Output?.Split('=').LastOrDefault()?.Trim('\"') ?? "Debian" : "Unknown";
            info.KernelVersion = results[1].Success ? results[1].Output?.Trim() : "Unknown";
            info.SquidVersion = results[2].Success ? results[2].Output?.Trim() : "Not installed";
            info.SquidGuardVersion = results[3].Success ? results[3].Output?.Trim() : "Not installed";
            info.MemoryUsage = results[4].Success ? results[4].Output?.Trim() : "Unknown";
            info.DiskUsage = results[5].Success ? results[5].Output?.Trim() : "Unknown";
            info.CpuCores = results[6].Success && int.TryParse(results[6].Output?.Trim(), out int cores) ? cores : 0;
            info.LoadAverage = results[7].Success ? results[7].Output?.Trim() : "Unknown";
            info.Hostname = results[8].Success ? results[8].Output?.Trim() : "unknown";
            info.IpAddress = results[9].Success ? results[9].Output?.Trim() : "unknown";

            if (results[10].Success && DateTime.TryParse(results[10].Output?.Trim(), out var lastBoot))
            {
                info.LastBoot = lastBoot;
            }

            _cache.Set(CACHE_KEY_SYSTEM_INFO, info, TimeSpan.FromMinutes(5));
            return info;
        }

        public async Task<List<string>> GetSquidGuardBlacklistsAsync()
        {
            if (!Directory.Exists(_blacklistsPath))
                return new List<string> { "Blacklists directory not found" };

            try
            {
                var result = await ExecuteBashCommandAsync($"ls -la {_blacklistsPath} | grep '^d' | awk '{{print $9}}'");
                if (result.Success && !string.IsNullOrEmpty(result.Output))
                {
                    return result.Output.Split('\n')
                        .Where(line => !string.IsNullOrWhiteSpace(line))
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading SquidGuard blacklists");
            }

            return new List<string>();
        }

        public async Task<bool> UpdateSquidGuardAsync()
        {
            try
            {
                _logger.LogInformation("Updating SquidGuard blacklists...");
                var result = await ExecuteBashCommandAsync("squidguard -C all 2>&1");

                if (!result.Success)
                {
                    _logger.LogError("Failed to update SquidGuard: {Error}", result.Error);
                    return false;
                }

                await ExecuteBashCommandAsync("squidguard -b");

                _logger.LogInformation("SquidGuard updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating SquidGuard");
                return false;
            }
        }

        public async Task<Dictionary<string, string>> GetSquidGuardStatsAsync()
        {
            var stats = new Dictionary<string, string>();

            try
            {
                var result = await ExecuteBashCommandAsync("grep -E '(BLOCK|PASS)' /var/log/squid/access.log | wc -l");
                if (result.Success && int.TryParse(result.Output?.Trim(), out int totalBlocks))
                {
                    stats["TotalFilteredRequests"] = totalBlocks.ToString();
                }

                var categoriesResult = await ExecuteBashCommandAsync(
                    "grep BLOCK /var/log/squid/access.log 2>/dev/null | " +
                    "awk -F'/' '{print $NF}' | sort | uniq -c | sort -rn | head -10");

                if (categoriesResult.Success && !string.IsNullOrEmpty(categoriesResult.Output))
                {
                    stats["TopBlockedCategories"] = categoriesResult.Output;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SquidGuard stats");
            }

            return stats;
        }

        private async Task<List<string>> GetLogLines(string logPath, int lines)
        {
            if (!File.Exists(logPath))
                return new List<string> { $"Log file not found: {logPath}" };

            try
            {
                string command = $"tail -n {lines} {logPath}";

                var result = await ExecuteBashCommandAsync(command);
                if (result.Success && !string.IsNullOrEmpty(result.Output))
                {
                    return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
                }

                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading log file: {LogPath}", logPath);
                return new List<string> { $"Error reading log: {ex.Message}" };
            }
        }

        private async Task<CommandResult> ExecuteBashCommandAsync(string command)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"{EscapeCommand(command)}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    }
                };

                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(outputTask, process.WaitForExitAsync());

                return new CommandResult
                {
                    Success = process.ExitCode == 0,
                    Output = (await outputTask)?.Trim(),
                    Error = (await errorTask)?.Trim()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing bash command: {Command}", command);
                return new CommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        private string EscapeCommand(string command) => command.Replace("\"", "\\\"");

        private long ParseMemoryUsage(string value, string unit)
        {
            if (double.TryParse(value, out double bytes))
            {
                return unit.ToUpper() switch
                {
                    "K" => (long)(bytes * 1024),
                    "M" => (long)(bytes * 1024 * 1024),
                    "G" => (long)(bytes * 1024 * 1024 * 1024),
                    _ => (long)bytes
                };
            }
            return 0;
        }

        private string ExtractSquidVersion(string versionText)
        {
            if (string.IsNullOrEmpty(versionText))
                return "Unknown";

            var match = System.Text.RegularExpressions.Regex.Match(versionText, @"Squid\s+Cache:\s+Version\s+([\d\.]+)");
            if (match.Success)
                return $"Squid {match.Groups[1].Value}";

            return versionText.Split('\n').FirstOrDefault()?.Trim() ?? "Unknown";
        }

        private async Task<TimeSpan> GetSystemUptimeAsync()
        {
            try
            {
                var result = await ExecuteBashCommandAsync("cat /proc/uptime");
                if (result.Success && double.TryParse(result.Output?.Split(' ')?.FirstOrDefault(), out double seconds))
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
            catch
            {
                // Игнорируем ошибки
            }
            return TimeSpan.Zero;
        }

        private SquidConfig ParseConfigLine(string line)
        {
            var parts = line.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                return null;

            return new SquidConfig
            {
                Name = parts[0],
                Value = parts[1],
                IsEnabled = true
            };
        }

        // Исправленный метод - возвращает новый id вместо использования ref
        private async Task<int> ParseAdditionalConfigFile(string filePath, List<SquidConfig> configs, int startId)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                var currentComment = new StringBuilder();
                int id = startId;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        currentComment.Clear();
                        continue;
                    }

                    if (trimmedLine.StartsWith("#"))
                    {
                        currentComment.AppendLine(trimmedLine.TrimStart('#').Trim());
                        continue;
                    }

                    var directive = ParseConfigLine(trimmedLine);
                    if (directive != null)
                    {
                        directive.Id = id++;
                        directive.SourceFile = Path.GetFileName(filePath);
                        if (currentComment.Length > 0)
                        {
                            directive.Description = currentComment.ToString().Trim();
                            currentComment.Clear();
                        }
                        configs.Add(directive);
                    }
                }

                return id; // Возвращаем обновленный id
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing additional config file: {FilePath}", filePath);
                return startId;
            }
        }

        private string GetConfigSection(string directive)
        {
            if (directive.StartsWith("acl")) return "Access Control Lists";
            if (directive.StartsWith("http_access")) return "Access Rules";
            if (directive.StartsWith("http_port")) return "Network Settings";
            if (directive.StartsWith("cache_dir")) return "Cache Settings";
            if (directive.StartsWith("refresh_pattern")) return "Refresh Patterns";
            if (directive.StartsWith("visible_hostname")) return "General Settings";
            return "Other Settings";
        }

        private class CommandResult
        {
            public bool Success { get; set; }
            public string Output { get; set; }
            public string Error { get; set; }
        }
    }
}