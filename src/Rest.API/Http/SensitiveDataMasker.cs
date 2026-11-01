using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rest.API.Http;

/// <summary>
/// Masks passwords, tokens and secrets inside JSON bodies before they reach the
/// audit table. Invalid or non-JSON payloads are returned unchanged.
/// </summary>
public static class SensitiveDataMasker
{
    public static string? MaskJson(string? body, IEnumerable<string> keys)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        var sensitiveKeys = keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (sensitiveKeys.Count == 0)
        {
            return body;
        }

        try
        {
            var node = JsonNode.Parse(body, documentOptions: new JsonDocumentOptions
            {
                MaxDepth = 64,
            });

            Mask(node, sensitiveKeys);
            return node?.ToJsonString() ?? body;
        }
        catch (JsonException)
        {
            return body;
        }
    }

    private static void Mask(JsonNode? node, HashSet<string> sensitiveKeys)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var property in jsonObject.ToList())
                {
                    if (sensitiveKeys.Contains(property.Key))
                    {
                        jsonObject[property.Key] = "***";
                        continue;
                    }

                    Mask(property.Value, sensitiveKeys);
                }

                break;

            case JsonArray jsonArray:
                foreach (var item in jsonArray)
                {
                    Mask(item, sensitiveKeys);
                }

                break;
        }
    }
}
