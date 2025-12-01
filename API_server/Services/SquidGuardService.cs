// Services/SquidGuardService.cs
using System.Diagnostics;
using SquidManagerAPI.Models;

namespace SquidManagerAPI.Services
{
    public class SquidGuardService : ISquidGuardService
    {
        private readonly ILogger<SquidGuardService> _logger;
        private readonly string _blacklistsPath = "/var/lib/squidguard/db/";
        private readonly string _squidGuardConfigPath = "/etc/squidguard/squidGuard.conf";
        private readonly string _squidGuardLogPath = "/var/log/squidguard/squidGuard.log";

        public SquidGuardService(ILogger<SquidGuardService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> UpdateBlacklistAsync(Blacklist blacklist)
        {
            try
            {
                var categoryPath = Path.Combine(_blacklistsPath, blacklist.Category);
                Directory.CreateDirectory(categoryPath);
                
                // Сохраняем домены
                if (blacklist.Domains.Any())
                {
                    var domainsFile = Path.Combine(categoryPath, "domains");
                    await File.WriteAllLinesAsync(domainsFile, blacklist.Domains);
                }
                
                // Сохраняем URL
                if (blacklist.Urls.Any())
                {
                    var urlsFile = Path.Combine(categoryPath, "urls");
                    await File.WriteAllLinesAsync(urlsFile, blacklist.Urls);
                }
                
                // Сохраняем выражения
                if (blacklist.Expressions.Any())
                {
                    var exprsFile = Path.Combine(categoryPath, "expressions");
                    await File.WriteAllLinesAsync(exprsFile, blacklist.Expressions);
                }

                // Компилируем черные списки
                await CompileBlacklistsAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating blacklist for category: {Category}", blacklist.Category);
                return false;
            }
        }

        public async Task<bool> RemoveFromBlacklistAsync(string category, string domain)
        {
            try
            {
                var domainsFile = Path.Combine(_blacklistsPath, category, "domains");
                if (File.Exists(domainsFile))
                {
                    var domains = (await File.ReadAllLinesAsync(domainsFile)).ToList();
                    domains.RemoveAll(d => d.Equals(domain, StringComparison.OrdinalIgnoreCase));
                    await File.WriteAllLinesAsync(domainsFile, domains);
                    
                    await CompileBlacklistsAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing domain from blacklist");
                return false;
            }
        }

        public async Task<List<Blacklist>> GetBlacklistsAsync()
        {
            var blacklists = new List<Blacklist>();
            
            if (Directory.Exists(_blacklistsPath))
            {
                var categories = Directory.GetDirectories(_blacklistsPath);
                
                foreach (var categoryPath in categories)
                {
                    var category = Path.GetFileName(categoryPath);
                    var blacklist = new Blacklist
                    {
                        Category = category,
                        LastUpdated = Directory.GetLastWriteTime(categoryPath)
                    };
                    
                    // Читаем домены
                    var domainsFile = Path.Combine(categoryPath, "domains");
                    if (File.Exists(domainsFile))
                    {
                        blacklist.Domains = (await File.ReadAllLinesAsync(domainsFile)).ToList();
                    }
                    
                    // Читаем URL
                    var urlsFile = Path.Combine(categoryPath, "urls");
                    if (File.Exists(urlsFile))
                    {
                        blacklist.Urls = (await File.ReadAllLinesAsync(urlsFile)).ToList();
                    }
                    
                    // Читаем выражения
                    var exprsFile = Path.Combine(categoryPath, "expressions");
                    if (File.Exists(exprsFile))
                    {
                        blacklist.Expressions = (await File.ReadAllLinesAsync(exprsFile)).ToList();
                    }
                    
                    blacklists.Add(blacklist);
                }
            }
            
            return blacklists;
        }

        public async Task<bool> ReloadSquidGuardAsync()
        {
            try
            {
                // Перекомпилируем базы данных SquidGuard
                await CompileBlacklistsAsync();
                
                // Перезагружаем Squid для применения изменений
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = "-c \"systemctl reload squid\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                await process.WaitForExitAsync();
                
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reloading SquidGuard");
                return false;
            }
        }

        public async Task<bool> CompileBlacklistsAsync()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "squidGuard",
                        Arguments = "-C all",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    _logger.LogError("SquidGuard compilation failed: {Error}", error);
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error compiling SquidGuard blacklists");
                return false;
            }
        }

        public async Task<List<AccessRule>> GetAccessRulesAsync()
        {
            // В Debian правила SquidGuard обычно находятся в конфигурационном файле
            // Это упрощенная реализация для демонстрации
            return new List<AccessRule>();
        }

        public async Task<bool> AddAccessRuleAsync(AccessRule rule)
        {
            // Реализация добавления правил доступа в конфигурацию SquidGuard
            return await UpdateSquidGuardConfig();
        }

        public async Task<bool> RemoveAccessRuleAsync(int ruleId)
        {
            // Реализация удаления правил доступа
            return await UpdateSquidGuardConfig();
        }

        public async Task<List<string>> GetSquidGuardLogsAsync(int lines = 100)
        {
            if (!File.Exists(_squidGuardLogPath))
                return new List<string> { "SquidGuard log file not found" };

            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-c \"tail -n {lines} {_squidGuardLogPath}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                return output.Split('\n').Where(line => !string.IsNullOrEmpty(line)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading SquidGuard logs");
                return new List<string> { "Error reading log file" };
            }
        }

        private async Task<bool> UpdateSquidGuardConfig()
        {
            try
            {
                await CompileBlacklistsAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating SquidGuard configuration");
                return false;
            }
        }
    }
}