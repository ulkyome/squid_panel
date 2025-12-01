// Models/SquidConfig.cs
namespace SquidManagerAPI.Models
{
    public class SquidConfig
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    public class AccessRule
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Destination { get; set; } = string.Empty;
        public string Action { get; set; } = "allow";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class Blacklist
    {
        public int Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public List<string> Domains { get; set; } = new List<string>();
        public List<string> Urls { get; set; } = new List<string>();
        public List<string> Expressions { get; set; } = new List<string>();
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class SquidStatus
    {
        public bool IsRunning { get; set; }
        public string Version { get; set; } = string.Empty;
        public TimeSpan Uptime { get; set; }
        public int ActiveConnections { get; set; }
        public long MemoryUsage { get; set; }
        public string ServiceStatus { get; set; } = string.Empty;
    }

    public class SystemInfo
    {
        public string OSVersion { get; set; } = string.Empty;
        public string KernelVersion { get; set; } = string.Empty;
        public string SquidVersion { get; set; } = string.Empty;
        public string SquidGuardVersion { get; set; } = string.Empty;
        public DateTime ServerTime { get; set; }
    }
}