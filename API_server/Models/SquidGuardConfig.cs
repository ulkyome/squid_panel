namespace SquidManagerAPI.Models
{
    public class SquidGuardConfig
    {
        public string RawConfig { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
        public string DbHome { get; set; } = string.Empty;
        public string LogDir { get; set; } = string.Empty;
        public string TimeSettings { get; set; } = string.Empty;
        public List<string> Sources { get; set; } = new();
        public List<string> Destinations { get; set; } = new();
        public List<string> AclRules { get; set; } = new();
    }

    public class SquidGuardStatus
    {
        public bool ConfigExists { get; set; }
        public bool RulesExist { get; set; }
        public bool BlacklistsExist { get; set; }
        public bool LogFileExists { get; set; }
        public bool SquidServiceActive { get; set; }
        public int CategoriesCount { get; set; }
        public long TotalDatabaseSize { get; set; }
    }

    public class AccessRuleSquidGuard
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string> Sources { get; set; } = new();
        public List<string> ExcludedSources { get; set; } = new();
        public string Action { get; set; } = string.Empty; // "pass" или "block"
        public string RedirectUrl { get; set; } = string.Empty;
        public string InAddr { get; set; } = string.Empty; // "in-addr" или "!in-addr"
        public string Description { get; set; } = string.Empty;
    }
}