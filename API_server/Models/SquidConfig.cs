using System.Text.Json.Serialization;

namespace SquidManagerAPI.Models
{
    public class SquidConfig
    {
        public bool IsEnabled { get; set; }
        public string SourceFile { get; set; }

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
        [JsonPropertyName("isRunning")]
        public bool IsRunning { get; set; }

        [JsonPropertyName("serviceStatus")]
        public string ServiceStatus { get; set; } = "unknown";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "Unknown";

        [JsonPropertyName("uptime")]
        public TimeSpan? Uptime { get; set; }

        [JsonPropertyName("activeConnections")]
        public int ActiveConnections { get; set; }

        [JsonPropertyName("memoryUsage")]
        public long MemoryUsage { get; set; } // in bytes

        [JsonPropertyName("processId")]
        public int? ProcessId { get; set; } // Добавлено свойство

        [JsonPropertyName("lastChecked")]
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("cpuUsage")]
        public double? CpuUsage { get; set; }

        [JsonPropertyName("cacheSize")]
        public long CacheSize { get; set; } // in bytes

        [JsonPropertyName("cacheObjects")]
        public int CacheObjects { get; set; }

        [JsonPropertyName("cacheHits")]
        public int CacheHits { get; set; }

        [JsonPropertyName("cacheMisses")]
        public int CacheMisses { get; set; }

        [JsonPropertyName("cacheHitRatio")]
        public double CacheHitRatio => CacheHits + CacheMisses > 0 ?
            (double)CacheHits / (CacheHits + CacheMisses) * 100 : 0;
    }

    public class SystemInfo
    {
        [JsonPropertyName("serverTime")]
        public DateTime ServerTime { get; set; }

        [JsonPropertyName("osVersion")]
        public string OSVersion { get; set; } = "Debian";

        [JsonPropertyName("kernelVersion")]
        public string KernelVersion { get; set; } = "Unknown";

        [JsonPropertyName("squidVersion")]
        public string SquidVersion { get; set; } = "Unknown";

        [JsonPropertyName("squidGuardVersion")]
        public string SquidGuardVersion { get; set; } = "Not installed";

        [JsonPropertyName("uptime")]
        public TimeSpan Uptime { get; set; }

        [JsonPropertyName("memoryUsage")]
        public string MemoryUsage { get; set; } = "Unknown"; // Исправлено на string

        [JsonPropertyName("diskUsage")]
        public string DiskUsage { get; set; } = "Unknown";

        [JsonPropertyName("cpuCores")]
        public int CpuCores { get; set; }

        [JsonPropertyName("loadAverage")]
        public string LoadAverage { get; set; } = "Unknown";

        [JsonPropertyName("hostname")]
        public string Hostname { get; set; } = "unknown";

        [JsonPropertyName("ipAddress")]
        public string IpAddress { get; set; } = "unknown";

        [JsonPropertyName("lastBoot")]
        public DateTime LastBoot { get; set; }

        [JsonPropertyName("systemLanguage")]
        public string SystemLanguage { get; set; } = "en_US.UTF-8";

        [JsonPropertyName("timeZone")]
        public string TimeZone { get; set; } = "UTC";
    }
}