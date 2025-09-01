using System.ComponentModel.DataAnnotations;

namespace Orchestrator;

public class AppSettings
{
    [Required]
    public ExternalConfig ExternalConfig { get; set; } = new("/config/Flows", "/config/Templates", 30);
    
    [Required]
    public ConnectionStrings ConnectionStrings { get; set; } = new();
    
    [Required]
    public RedisConfig Redis { get; set; } = new("localhost:6379");
}

public class ConnectionStrings
{
    public string Primary { get; set; } = string.Empty;
    public string Audit { get; set; } = string.Empty;
}
