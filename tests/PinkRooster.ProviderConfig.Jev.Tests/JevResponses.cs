using System.Text.Json.Nodes;

namespace PinkRooster.ProviderConfig.Jev.Tests;

/// <summary>A canned Jev System One response body, in the shape of the real API.</summary>
internal static class JevResponses
{
    public static string Noul(string model = "jev-1.14", double p = 0.9) => new JsonObject
    {
        ["model"] = model,
        ["answers"] = new JsonObject { ["q"] = new JsonObject { ["type"] = "noul", ["noul"] = p } },
        ["usage"] = new JsonObject { ["input_tokens"] = 10, ["output_tokens"] = 2 },
    }.ToJsonString();
}
