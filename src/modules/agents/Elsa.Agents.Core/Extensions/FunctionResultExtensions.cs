using System.Text.Json;
using Microsoft.SemanticKernel;

namespace Elsa.Agents;

public static class FunctionResultExtensions
{
    public static async Task<JsonElement> AsJsonElementAsync(this Task<InvokeAgentResult> resultTask)
    {
        var result = await resultTask;
        return result.FunctionResult.AsJsonElement();
    }

    public static JsonElement AsJsonElement(this ChatMessageContent result)
    {
        var content = result.Content?.Trim();

        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("El contenido del mensaje está vacío.");

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(content!);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Error al deserializar el contenido del mensaje como JSON:\n{content}", ex);
        }
    }
}
