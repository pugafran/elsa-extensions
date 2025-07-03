using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.SemanticKernel.Extensions;
using Microsoft.SemanticKernel.PromptTemplates;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.ChatCompletion;

#pragma warning disable SKEXP0010
#pragma warning disable SKEXP0001

namespace Elsa.Agents;

public class AgentInvoker(KernelFactory kernelFactory, IKernelConfigProvider kernelConfigProvider)
{
    public async Task<InvokeAgentResult> InvokeAgentAsync(string agentName, IDictionary<string, object?> input, CancellationToken cancellationToken = default)
    {

        Console.WriteLine("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        var kernelConfig = await kernelConfigProvider.GetKernelConfigAsync(cancellationToken);
        var kernel = await kernelFactory.CreateKernelAsync(kernelConfig, agentName);
        var agentConfig = kernelConfig.Agents[agentName];
        var executionSettings = agentConfig.ExecutionSettings;

        kernel = await InjectMcpFunctions(kernel, kernelConfig.Mcps);

        var promptExecutionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = executionSettings.Temperature,
            TopP = executionSettings.TopP,
            MaxTokens = executionSettings.MaxTokens,
            PresencePenalty = executionSettings.PresencePenalty,
            FrequencyPenalty = executionSettings.FrequencyPenalty,
            ResponseFormat = executionSettings.ResponseFormat,
            ChatSystemPrompt = agentConfig.PromptTemplate,
            ServiceId = "default",
            ModelId = "Qwen/Qwen3-14B-AWQ",
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()

        };

        var promptExecutionSettingsDict = new Dictionary<string, PromptExecutionSettings>
        {
            [promptExecutionSettings.ServiceId ?? "default"] = promptExecutionSettings
        };

        var promptTemplateConfig = new PromptTemplateConfig
        {
            Name = agentConfig.FunctionName,
            Description = agentConfig.Description,
            Template = agentConfig.PromptTemplate,
            ExecutionSettings = promptExecutionSettingsDict,
            AllowDangerouslySetContent = true,
            InputVariables = agentConfig.InputVariables.Select(x => new InputVariable
            {
                Name = x.Name,
                Description = x.Description,
                IsRequired = true,
                AllowDangerouslySetContent = true
            }).ToList()
        };

        var kernelArguments = new KernelArguments(input);

        var templateFactory = new HandlebarsPromptTemplateFactory();

        var manolo = new PromptTemplateConfig
        {
            Template = agentConfig.PromptTemplate,
            TemplateFormat = "handlebars",
            Name = agentConfig.FunctionName
        };

        var promptTemplate = templateFactory.Create(manolo);
        string renderedPrompt = await promptTemplate.RenderAsync(kernel, kernelArguments);

        // Mostramos el prompt final con valores sustituidos
        Console.WriteLine("==== Prompt Renderizado! ====");
        Console.WriteLine(renderedPrompt);

        ChatHistory chatHistory = [];
        chatHistory.AddUserMessage(renderedPrompt);

        IChatCompletionService chatCompletion = kernel.GetRequiredService<IChatCompletionService>();

        OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var response = await chatCompletion.GetChatMessageContentAsync(
            chatHistory,
            executionSettings: openAIPromptExecutionSettings,
            kernel: kernel);

        Console.WriteLine("========== AgentInvoker ejecutándose ==========");
        Console.WriteLine("ServiceId: " + promptExecutionSettings.ServiceId);
        Console.WriteLine("PromptExecutionSettings serializado:");
        Console.WriteLine(JsonSerializer.Serialize(promptExecutionSettings));

        return new(agentConfig, response);
    }



    private async Task<Kernel> InjectMcpFunctions(Kernel kernel, IDictionary<string, McpConfig> mcps)
    {

        Console.WriteLine($"INJECT");

        foreach (var mcp in mcps.Values)
        {
            if (string.IsNullOrWhiteSpace(mcp.Endpoint))
                continue;

            var name = string.IsNullOrWhiteSpace(mcp.Name) ? "McpPlugin" : mcp.Name;

            if (!kernel.Plugins.Contains(name))
            {
                Console.WriteLine($"Plugin {name} registered in Kernel.");
                await kernel.Plugins.AddMcpFunctionsFromSseServerAsync(name, mcp.Endpoint);

               
            }
            else
            {
                Console.WriteLine($"Plugin {name} already registered in Kernel. Skipping.");
            }
        }

        await kernel.Plugins.AddMcpFunctionsFromStdioServerAsync("Filesystem", "docker", [
            "run",
            "-i",
            "--rm",
            "--volume=C:\\Users\\Francisco.puga\\mcp:/mcp",
            "ghcr.io/mark3labs/mcp-filesystem-server:latest",
            "/mcp"
        ]);


        return kernel;
    }
}
