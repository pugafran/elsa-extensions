namespace Elsa.Agents;

public class KernelConfig
{
    public IDictionary<string, McpConfig> Mcps { get; } = new Dictionary<string, McpConfig>();
    public IDictionary<string, ApiKeyConfig> ApiKeys { get; set; } = new Dictionary<string, ApiKeyConfig>();
    public IDictionary<string, ServiceConfig> Services { get; } = new Dictionary<string, ServiceConfig>();
    public IDictionary<string, AgentConfig> Agents { get; } = new Dictionary<string, AgentConfig>();
}