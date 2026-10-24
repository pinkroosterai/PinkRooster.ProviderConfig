using System.ClientModel.Primitives;
using System.Text.Json.Nodes;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Responses;
using PinkRooster.ProviderConfig.OpenAI;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.OpenAI.Tests;

public sealed class ResponsesProviderTests
{
    private const string Ok = """
        {"id":"resp_1","object":"response","created_at":1700000000,"status":"completed","model":"m1",
         "output":[{"type":"message","id":"msg_1","status":"completed","role":"assistant",
                    "content":[{"type":"output_text","text":"hi","annotations":[]}]}],
         "usage":{"input_tokens":3,"output_tokens":2,"total_tokens":5}}
        """;

    private const string FailedBody = """
        {"id":"resp_1","object":"response","created_at":1700000000,"status":"failed","model":"m1","output":[],
         "error":{"code":"server_error","message":"boom"}}
        """;

    private const string Refusal = """
        {"id":"resp_1","object":"response","created_at":1700000000,"status":"completed","model":"m1",
         "output":[{"type":"message","id":"msg_1","status":"completed","role":"assistant",
                    "content":[{"type":"refusal","refusal":"no"}]}],
         "usage":{"input_tokens":3,"output_tokens":2,"total_tokens":5}}
        """;

    private const string Created = """{"type":"response.created","sequence_number":0,"response":{"id":"resp_1","object":"response","created_at":1700000000,"status":"in_progress","model":"m1","output":[]}}""";
    private const string Hel = """{"type":"response.output_text.delta","sequence_number":1,"item_id":"msg_1","output_index":0,"content_index":0,"delta":"Hel"}""";
    private const string Lo = """{"type":"response.output_text.delta","sequence_number":2,"item_id":"msg_1","output_index":0,"content_index":0,"delta":"lo"}""";
    private const string ErrorEvent = """{"type":"error","sequence_number":3,"code":"server_error","message":"boom","param":null}""";

    private static readonly string FailedEvent = $$"""{"type":"response.failed","sequence_number":3,"response":{{OneLine(FailedBody)}}}""";
    private static readonly string CompletedEvent = $$"""{"type":"response.completed","sequence_number":3,"response":{{OneLine(Ok)}}}""";

    private const string Failure = "The upstream provider reported a failure: server_error: boom";

    private readonly StubUpstream _stub = new();

    private IChatClient Chat(bool? store = null)
    {
        ServiceCollection services = new();
        services.AddProviderConfig().AddOpenAI().Configure(o => o.Providers["up"] = new ProviderOptions
        {
            Type = "openai_responses",
            ApiKey = "k1",
            Endpoint = new Uri("http://up/v1"),
            Store = store,
        });
        services.AddSingleton(new ProviderHttpHandler(_stub));
        return services.BuildServiceProvider().GetRequiredService<IChatClientFactory>().GetChatClient("up", "m1");
    }

    [Fact]
    public async Task Responses_PostsToResponses_WithKeyAndModel()
    {
        _stub.RespondJson(Ok);

        Assert.Equal("hi", (await Chat().GetResponseAsync("q")).Text);

        Assert.Equal(new Uri("http://up/v1/responses"), _stub.Requests[0].Uri);
        Assert.Equal("Bearer k1", _stub.Requests[0].Headers["Authorization"]);
        Assert.Equal("m1", (string?)_stub.Requests[0].Body!["model"]);
    }

    [Fact]
    public async Task Responses_RawResult_ExposesTheEchoedStoreFlag_ThroughTheSerializedJson()
    {
        _stub.RespondJson(Ok.Replace("\"status\":\"completed\",\"model\"", "\"status\":\"completed\",\"store\":false,\"model\"", StringComparison.Ordinal));

        ChatResponse response = await Chat().GetResponseAsync("q");

        JsonObject raw = (JsonObject)JsonNode.Parse(ModelReaderWriter.Write((ResponseResult)response.RawRepresentation!))!;
        Assert.False(raw["store"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Responses_SendsStoreFalse_ByDefault()
    {
        _stub.RespondJson(Ok);

        await Chat().GetResponseAsync("q");

        Assert.False((bool?)_stub.Requests[0].Body!["store"]);
    }

    [Fact]
    public async Task Responses_SendsStoreTrue_WhenTheProviderSaysSo()
    {
        _stub.RespondJson(Ok);

        await Chat(store: true).GetResponseAsync("q");

        Assert.True((bool?)_stub.Requests[0].Body!["store"]);
    }

    [Fact]
    public async Task Responses_CallersStoreWins()
    {
        _stub.RespondJson(Ok);
        ChatOptions options = new() { RawRepresentationFactory = _ => new CreateResponseOptions { StoredOutputEnabled = true } };

        await Chat().GetResponseAsync("q", options);

        Assert.True((bool?)_stub.Requests[0].Body!["store"]);
    }

    [Fact]
    public async Task Responses_StillSendsStoreFalse_WhenTheCallersFactoryReturnsAnotherType()
    {
        _stub.RespondJson(Ok);
        ChatOptions options = new() { RawRepresentationFactory = _ => new object() };

        await Chat().GetResponseAsync("q", options);

        Assert.False((bool?)_stub.Requests[0].Body!["store"]);
    }

    [Fact]
    public async Task Responses_DoesNotChangeTheCallersOptions()
    {
        _stub.RespondJson(Ok);
        Func<IChatClient, object?> original = _ => null;
        ChatOptions options = new() { RawRepresentationFactory = original };

        await Chat().GetResponseAsync("q", options);

        Assert.Same(original, options.RawRepresentationFactory);
    }

    [Fact]
    public async Task Responses_SendsConversationIdAsPreviousResponseId()
    {
        _stub.RespondJson(Ok);

        await Chat().GetResponseAsync("q", new ChatOptions { ConversationId = "resp_0" });

        Assert.Equal("resp_0", (string?)_stub.Requests[0].Body!["previous_response_id"]);
    }

    [Fact]
    public async Task Responses_HasNoConversationId_WhenNotStored()
    {
        _stub.RespondJson(Ok);

        ChatResponse response = await Chat().GetResponseAsync("q");

        Assert.Null(response.ConversationId);
    }

    [Fact]
    public async Task Responses_StreamsTextAndUsage()
    {
        _stub.RespondSse(Created, Hel, Lo, CompletedEvent);

        List<ChatResponseUpdate> updates = [];
        await foreach (ChatResponseUpdate update in Chat().GetStreamingResponseAsync("q"))
        {
            updates.Add(update);
        }

        ChatResponse response = updates.ToChatResponse();
        Assert.Equal("Hello", string.Concat(updates.Select(update => update.Text)));
        Assert.Equal(5, response.Usage?.TotalTokenCount);
        Assert.Equal(ChatFinishReason.Stop, updates.Last(update => update.FinishReason is not null).FinishReason);
    }

    [Fact]
    public async Task Responses_Throws_ForAFailedWholeResponse()
    {
        _stub.RespondJson(FailedBody);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Chat().GetResponseAsync("q"));

        Assert.Equal(Failure, exception.Message);
    }

    [Fact]
    public async Task Responses_Throws_ForAStreamedErrorEvent_AfterTheTextBeforeIt()
    {
        _stub.RespondSse(Created, Hel, Lo, ErrorEvent);

        StringBuilder text = new();
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (ChatResponseUpdate update in Chat().GetStreamingResponseAsync("q"))
            {
                text.Append(update.Text);
            }
        });

        Assert.Equal("Hello", text.ToString());
        Assert.Equal(Failure, exception.Message);
    }

    [Fact]
    public async Task Responses_Throws_ForAStreamedFailedEvent_WithoutAnErrorEvent()
    {
        _stub.RespondSse(Created, Hel, Lo, FailedEvent);

        StringBuilder text = new();
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (ChatResponseUpdate update in Chat().GetStreamingResponseAsync("q"))
            {
                text.Append(update.Text);
            }
        });

        Assert.Equal("Hello", text.ToString());
        Assert.Equal(Failure, exception.Message);
    }

    [Fact]
    public async Task Responses_PassesARefusalThrough()
    {
        _stub.RespondJson(Refusal);

        ChatResponse response = await Chat().GetResponseAsync("q");

        ErrorContent refusal = Assert.Single(response.Messages.SelectMany(message => message.Contents).OfType<ErrorContent>());
        Assert.Equal("Refusal", refusal.ErrorCode);
    }

    [Fact]
    public async Task Responses_DisposingTheClient_LeavesTheProviderUsable()
    {
        _stub.RespondJson(Ok).RespondJson(Ok);
        ServiceCollection services = new();
        services.AddProviderConfig().AddOpenAI().Configure(o => o.Providers["up"] = new ProviderOptions
        {
            Type = "openai_responses",
            ApiKey = "k1",
            Endpoint = new Uri("http://up/v1"),
        });
        services.AddSingleton(new ProviderHttpHandler(_stub));
        IChatClientFactory factory = services.BuildServiceProvider().GetRequiredService<IChatClientFactory>();
        IChatClient first = factory.GetChatClient("up", "m1");
        await first.GetResponseAsync("q");
        first.Dispose();

        IChatClient second = factory.GetChatClient("up", "m1");

        Assert.Equal("hi", (await second.GetResponseAsync("q")).Text);
    }

    [Fact]
    public void Responses_GetChatClient_MakesNoRequest()
    {
        Chat();

        Assert.Empty(_stub.Requests);
    }

    private static string OneLine(string json) => json.ReplaceLineEndings("").Replace("  ", "");
}
