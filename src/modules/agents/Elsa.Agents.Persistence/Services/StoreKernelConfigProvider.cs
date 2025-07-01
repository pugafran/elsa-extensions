using Elsa.Agents;
using Elsa.Agents.Persistence.Contracts;
using Microsoft.Extensions.Options;

namespace Elsa.Agents.Persistence;

public class StoreKernelConfigProvider(IApiKeyStore apiKeyStore, IServiceStore serviceStore, IAgentStore agentStore, IOptions<AgentsOptions> options) : IKernelConfigProvider
{
    public async Task<KernelConfig> GetKernelConfigAsync(CancellationToken cancellationToken = default)
    {
        var kernelConfig = new KernelConfig();
        var apiKeys = await apiKeyStore.ListAsync(cancellationToken);
        var services = await serviceStore.ListAsync(cancellationToken);
        var agents = await agentStore.ListAsync(cancellationToken);
        foreach (var apiKey in apiKeys) kernelConfig.ApiKeys[apiKey.Name] = apiKey.ToApiKeyConfig();
        foreach (var service in services) kernelConfig.Services[service.Name] = service.ToServiceConfig();
        foreach (var agent in agents) kernelConfig.Agents[agent.Name] = agent.ToAgentConfig();
        foreach (var mcp in options.Value.Mcps) kernelConfig.Mcps[mcp.Name] = mcp;
        return kernelConfig;
    }
}