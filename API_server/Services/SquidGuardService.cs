using System.Diagnostics;
using System.Text;
using SquidManagerAPI.Models;
using System.Text.RegularExpressions;

namespace SquidManagerAPI.Services
{
    public class SquidGuardService : ISquidGuardService
    {
        private readonly ILogger<SquidGuardService> _logger;
        private readonly string _blacklistsPath = "/var/lib/squidguard/db/";
        private readonly string _squidGuardConfigPath = "/etc/squidguard/squidGuard.conf";
        private readonly string _squidGuardRulesPath = "/etc/squidguard/squidguard.rules";
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

        public async Task<SquidGuardConfig> GetSquidGuardConfigAsync()
        {
            var config = new SquidGuardConfig();
            
            if (!File.Exists(_squidGuardConfigPath))
            {
                _logger.LogWarning("SquidGuard config file not found at: {Path}", _squidGuardConfigPath);
                return config;
            }
            
            try
            {
                var content = await File.ReadAllTextAsync(_squidGuardConfigPath);
                config.RawConfig = content;
                config.FilePath = _squidGuardConfigPath;
                config.LastModified = File.GetLastWriteTime(_squidGuardConfigPath);
                
                // Парсинг конфигурации для извлечения основных параметров
                ParseConfigContent(content, config);
                
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading SquidGuard configuration");
                throw;
            }
        }

        public async Task<bool> UpdateSquidGuardConfigAsync(SquidGuardConfig config)
        {
            try
            {
                if (!string.IsNullOrEmpty(config.RawConfig))
                {
                    // Создаем backup текущей конфигурации
                    await CreateConfigBackup();
                    
                    // Сохраняем новую конфигурацию
                    await File.WriteAllTextAsync(_squidGuardConfigPath, config.RawConfig);
                    
                    // Проверяем синтаксис конфигурации
                    if (await ValidateSquidGuardConfig())
                    {
                        await ReloadSquidGuardAsync();
                        return true;
                    }
                    else
                    {
                        // Восстанавливаем из backup в случае ошибки
                        await RestoreConfigBackup();
                        return false;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating SquidGuard configuration");
                await RestoreConfigBackup();
                return false;
            }
        }

        public async Task<bool> UpdateSquidGuardConfigPartialAsync(Dictionary<string, string> configUpdates)
        {
            try
            {
                if (!File.Exists(_squidGuardConfigPath))
                    return false;
                
                // Читаем текущую конфигурацию
                var content = await File.ReadAllTextAsync(_squidGuardConfigPath);
                
                // Применяем обновления
                foreach (var update in configUpdates)
                {
                    var pattern = $@"^\s*{Regex.Escape(update.Key)}\s+.*$";
                    var replacement = $"{update.Key} {update.Value}";
                    
                    if (Regex.IsMatch(content, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
                    {
                        content = Regex.Replace(content, pattern, replacement, 
                            RegexOptions.Multiline | RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        // Добавляем новую директиву если она не существует
                        content += $"\n{update.Key} {update.Value}";
                    }
                }
                
                // Создаем backup
                await CreateConfigBackup();
                
                // Сохраняем обновленную конфигурацию
                await File.WriteAllTextAsync(_squidGuardConfigPath, content);
                
                // Проверяем синтаксис
                if (await ValidateSquidGuardConfig())
                {
                    await ReloadSquidGuardAsync();
                    return true;
                }
                else
                {
                    await RestoreConfigBackup();
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error partially updating SquidGuard configuration");
                await RestoreConfigBackup();
                return false;
            }
        }

        public async Task<List<AccessRuleSquidGuard>> GetAccessRulesAsync()
        {
            var rules = new List<AccessRuleSquidGuard>();
            
            if (!File.Exists(_squidGuardRulesPath))
            {
                _logger.LogWarning("SquidGuard rules file not found at: {Path}", _squidGuardRulesPath);
                return rules;
            }
            
            try
            {
                var content = await File.ReadAllTextAsync(_squidGuardRulesPath);
                var lines = content.Split('\n');
                var currentRule = new AccessRuleSquidGuard();
                var inRule = false;
                var ruleId = 1;
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    if (string.IsNullOrEmpty(trimmedLine))
                        continue;
                    
                    // Начало нового ACL
                    if (trimmedLine.EndsWith("{"))
                    {
                        if (inRule)
                        {
                            // Закрываем предыдущее правило если было открыто
                            rules.Add(currentRule);
                        }
                        
                        currentRule = new AccessRuleSquidGuard
                        {
                            Id = ruleId++,
                            Name = trimmedLine.Substring(0, trimmedLine.Length - 1).Trim()
                        };
                        inRule = true;
                    }
                    // Конец ACL
                    else if (trimmedLine == "}")
                    {
                        if (inRule)
                        {
                            rules.Add(currentRule);
                            inRule = false;
                        }
                    }
                    // Содержимое правила
                    else if (inRule)
                    {
                        if (trimmedLine.StartsWith("pass") || trimmedLine.StartsWith("block"))
                        {
                            var parts = trimmedLine.Split(' ');
                            if (parts.Length > 0)
                            {
                                currentRule.Action = parts[0];
                                // Извлекаем источники и категории
                                for (int i = 1; i < parts.Length; i++)
                                {
                                    if (parts[i].StartsWith("!"))
                                        currentRule.ExcludedSources.Add(parts[i].Substring(1));
                                    else
                                        currentRule.Sources.Add(parts[i]);
                                }
                            }
                        }
                        else if (trimmedLine.StartsWith("redirect"))
                        {
                            currentRule.RedirectUrl = trimmedLine.Substring("redirect".Length).Trim();
                        }
                        else if (trimmedLine.StartsWith("!in-addr") || trimmedLine.StartsWith("in-addr"))
                        {
                            currentRule.InAddr = trimmedLine;
                        }
                    }
                }
                
                return rules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing access rules");
                return new List<AccessRuleSquidGuard>();
            }
        }

        public async Task<bool> AddAccessRuleAsync(AccessRuleSquidGuard rule)
        {
            try
            {
                var rules = await GetAccessRulesAsync();
                rule.Id = rules.Count > 0 ? rules.Max(r => r.Id) + 1 : 1;
                rules.Add(rule);
                
                return await SaveAccessRulesAsync(rules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding access rule");
                return false;
            }
        }

        public async Task<bool> UpdateAccessRuleAsync(AccessRuleSquidGuard rule)
        {
            try
            {
                var rules = await GetAccessRulesAsync();
                var existingRule = rules.FirstOrDefault(r => r.Id == rule.Id);
                
                if (existingRule == null)
                    return false;
                
                // Обновляем правило
                existingRule.Name = rule.Name;
                existingRule.Sources = rule.Sources;
                existingRule.ExcludedSources = rule.ExcludedSources;
                existingRule.Action = rule.Action;
                existingRule.RedirectUrl = rule.RedirectUrl;
                existingRule.InAddr = rule.InAddr;
                existingRule.Description = rule.Description;
                
                return await SaveAccessRulesAsync(rules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating access rule");
                return false;
            }
        }

        public async Task<bool> RemoveAccessRuleAsync(int ruleId)
        {
            try
            {
                var rules = await GetAccessRulesAsync();
                var ruleToRemove = rules.FirstOrDefault(r => r.Id == ruleId);
                
                if (ruleToRemove == null)
                    return false;
                
                rules.Remove(ruleToRemove);
                
                return await SaveAccessRulesAsync(rules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing access rule");
                return false;
            }
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

        public async Task<SquidGuardStatus> GetSquidGuardStatusAsync()
        {
            try
            {
                var status = new SquidGuardStatus
                {
                    ConfigExists = File.Exists(_squidGuardConfigPath),
                    RulesExist = File.Exists(_squidGuardRulesPath),
                    BlacklistsExist = Directory.Exists(_blacklistsPath),
                    LogFileExists = File.Exists(_squidGuardLogPath)
                };
                
                // Проверяем активен ли squidGuard
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = "-c \"systemctl is-active squid\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                
                status.SquidServiceActive = output.Trim() == "active";
                
                // Получаем статистику базы данных
                if (Directory.Exists(_blacklistsPath))
                {
                    var categories = Directory.GetDirectories(_blacklistsPath);
                    status.CategoriesCount = categories.Length;
                    
                    foreach (var category in categories)
                    {
                        var categoryName = Path.GetFileName(category);
                        var dbFile = Path.Combine(category, "domains.db");
                        if (File.Exists(dbFile))
                        {
                            var fileInfo = new FileInfo(dbFile);
                            status.TotalDatabaseSize += fileInfo.Length;
                        }
                    }
                }
                
                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SquidGuard status");
                return new SquidGuardStatus();
            }
        }

        #region Private Methods

        private void ParseConfigContent(string content, SquidGuardConfig config)
        {
            try
            {
                var lines = content.Split('\n');
                
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;
                    
                    // Извлекаем основные директивы
                    if (trimmedLine.StartsWith("dbhome"))
                    {
                        config.DbHome = ExtractValue(trimmedLine);
                    }
                    else if (trimmedLine.StartsWith("logdir"))
                    {
                        config.LogDir = ExtractValue(trimmedLine);
                    }
                    else if (trimmedLine.StartsWith("time"))
                    {
                        config.TimeSettings = trimmedLine;
                    }
                    else if (trimmedLine.StartsWith("src"))
                    {
                        var srcMatch = Regex.Match(trimmedLine, @"src\s+(\w+)");
                        if (srcMatch.Success)
                        {
                            config.Sources.Add(srcMatch.Groups[1].Value);
                        }
                    }
                    else if (trimmedLine.StartsWith("dest"))
                    {
                        var destMatch = Regex.Match(trimmedLine, @"dest\s+(\w+)");
                        if (destMatch.Success)
                        {
                            config.Destinations.Add(destMatch.Groups[1].Value);
                        }
                    }
                    else if (trimmedLine.StartsWith("acl"))
                    {
                        var aclMatch = Regex.Match(trimmedLine, @"acl\s+(.+)$");
                        if (aclMatch.Success)
                        {
                            config.AclRules.Add(aclMatch.Groups[1].Value.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing SquidGuard config content");
            }
        }

        private string ExtractValue(string line)
        {
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length > 1 ? parts[1].Trim('"', '\'') : string.Empty;
        }

        private async Task<bool> SaveAccessRulesAsync(List<AccessRuleSquidGuard> rules)
        {
            try
            {
                var sb = new StringBuilder();
                
                foreach (var rule in rules.OrderBy(r => r.Id))
                {
                    sb.AppendLine($"{rule.Name} {{");
                    
                    // Добавляем действие с источниками
                    var sources = new List<string>();
                    
                    if (!string.IsNullOrEmpty(rule.InAddr))
                        sources.Add(rule.InAddr);
                    
                    sources.AddRange(rule.ExcludedSources.Select(s => $"!{s}"));
                    sources.AddRange(rule.Sources);
                    
                    var actionLine = $"{rule.Action} {string.Join(" ", sources)}";
                    sb.AppendLine($"  {actionLine}");
                    
                    if (!string.IsNullOrEmpty(rule.RedirectUrl))
                    {
                        sb.AppendLine($"  redirect {rule.RedirectUrl}");
                    }
                    
                    sb.AppendLine("}");
                    sb.AppendLine();
                }
                
                // Создаем backup
                if (File.Exists(_squidGuardRulesPath))
                {
                    var backupPath = $"{_squidGuardRulesPath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                    File.Copy(_squidGuardRulesPath, backupPath, true);
                }
                
                await File.WriteAllTextAsync(_squidGuardRulesPath, sb.ToString());
                await ReloadSquidGuardAsync();
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving access rules");
                return false;
            }
        }

        private async Task<bool> ValidateSquidGuardConfig()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "squidGuard",
                        Arguments = $"-c {_squidGuardConfigPath} -C",
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
                    _logger.LogError("SquidGuard config validation failed: {Error}", error);
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating SquidGuard configuration");
                return false;
            }
        }

        private async Task CreateConfigBackup()
        {
            try
            {
                if (File.Exists(_squidGuardConfigPath))
                {
                    var backupPath = $"{_squidGuardConfigPath}.backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                    await File.WriteAllTextAsync(backupPath, await File.ReadAllTextAsync(_squidGuardConfigPath));
                    
                    // Удаляем старые backup файлы (оставляем последние 5)
                    var backupFiles = Directory.GetFiles(Path.GetDirectoryName(_squidGuardConfigPath)!, 
                        $"{Path.GetFileName(_squidGuardConfigPath)}.backup_*")
                        .OrderByDescending(f => f)
                        .Skip(5);
                    
                    foreach (var oldBackup in backupFiles)
                    {
                        File.Delete(oldBackup);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating config backup");
            }
        }

        private async Task RestoreConfigBackup()
        {
            try
            {
                var backupFiles = Directory.GetFiles(Path.GetDirectoryName(_squidGuardConfigPath)!, 
                    $"{Path.GetFileName(_squidGuardConfigPath)}.backup_*")
                    .OrderByDescending(f => f);
                
                if (backupFiles.Any())
                {
                    var latestBackup = backupFiles.First();
                    await File.WriteAllTextAsync(_squidGuardConfigPath, await File.ReadAllTextAsync(latestBackup));
                    _logger.LogInformation("Restored SquidGuard config from backup: {Backup}", latestBackup);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring config from backup");
            }
        }

        #endregion
    }
}