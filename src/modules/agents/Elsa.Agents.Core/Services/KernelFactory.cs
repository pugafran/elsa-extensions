using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
using ModelContextProtocol.SemanticKernel.Extensions;

#pragma warning disable SKEXP0001
#pragma warning disable SKEXP0010

namespace Elsa.Agents;

public class KernelFactory(IServiceDiscoverer serviceDiscoverer, ILoggerFactory loggerFactory, IServiceProvider serviceProvider, ILogger<KernelFactory> logger)
{
    public Task<Kernel> CreateKernelAsync(KernelConfig kernelConfig, string agentName)
    {
        var agent = kernelConfig.Agents[agentName];
        return CreateKernelAsync(kernelConfig, agent);
    }

    public async Task<Kernel> CreateKernelAsync(KernelConfig kernelConfig, AgentConfig agentConfig)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddLogging(services => services.AddConsole().SetMinimumLevel(LogLevel.Trace));
        builder.Services.AddSingleton(agentConfig);

        ApplyAgentConfig(builder, kernelConfig, agentConfig);

        var kernel = builder.Build();


        return kernel;
    }

    public Kernel CreateKernel(KernelConfig kernelConfig, string agentName) =>
        CreateKernelAsync(kernelConfig, agentName).GetAwaiter().GetResult();

    public Kernel CreateKernel(KernelConfig kernelConfig, AgentConfig agentConfig) =>
        CreateKernelAsync(kernelConfig, agentConfig).GetAwaiter().GetResult();

    private void ApplyAgentConfig(IKernelBuilder builder, KernelConfig kernelConfig, AgentConfig agentConfig)
    {
        var services = serviceDiscoverer.Discover().ToDictionary(x => x.Name);
        
        foreach (string serviceName in agentConfig.Services)
        {
            if (!kernelConfig.Services.TryGetValue(serviceName, out var serviceConfig))
            {
                logger.LogWarning($"Service {serviceName} not found");
                continue;
            }

            AddService(builder, kernelConfig, serviceConfig, services);
        }

        //AddPlugins(builder, agentConfig);
        AddAgents(builder, kernelConfig, agentConfig);
    }

    private void AddService(IKernelBuilder builder, KernelConfig kernelConfig, ServiceConfig serviceConfig, Dictionary<string, IAgentServiceProvider> services)
    {
        if (!services.TryGetValue(serviceConfig.Type, out var serviceProvider))
        {
            logger.LogWarning($"Service provider {serviceConfig.Type} not found");
            return;
        }
        
        var context = new KernelBuilderContext(builder, kernelConfig, serviceConfig);
        serviceProvider.ConfigureKernel(context);
    }
    
    //private void AddPlugins(IKernelBuilder builder, AgentConfig agent)
    //{
    //    // Implementation removed: legacy plugin system is no longer used.
    //}

    private void AddAgents(IKernelBuilder builder, KernelConfig kernelConfig, AgentConfig agent)
    {
        foreach (var agentName in agent.Agents)
        {
            if (!kernelConfig.Agents.TryGetValue(agentName, out var subAgent))
            {
                logger.LogWarning($"Agent {agentName} not found");
                continue;
            }

            var promptExecutionSettings = subAgent.ToOpenAIPromptExecutionSettings();
            var promptExecutionSettingsDictionary = new Dictionary<string, PromptExecutionSettings>
            {
                [PromptExecutionSettings.DefaultServiceId] = promptExecutionSettings,
            };
            var promptTemplateConfig = new PromptTemplateConfig
            {
                Name = subAgent.FunctionName,
                Description = subAgent.Description,
                Template = subAgent.PromptTemplate,
                ExecutionSettings = promptExecutionSettingsDictionary,
                AllowDangerouslySetContent = true,
                InputVariables = subAgent.InputVariables.Select(x => new InputVariable
                {
                    Name = x.Name,
                    Description = x.Description,
                    IsRequired = true,
                    AllowDangerouslySetContent = true
                }).ToList()
            };

            var subAgentFunction = KernelFunctionFactory.CreateFromPrompt(promptTemplateConfig, loggerFactory: loggerFactory);
            var agentPlugin = KernelPluginFactory.CreateFromFunctions(subAgent.Name, subAgent.Description, [subAgentFunction]);
            builder.Plugins.Add(agentPlugin);
        }
    }
}