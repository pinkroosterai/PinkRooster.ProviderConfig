using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Jev.Net;

namespace PinkRooster.ProviderConfig.Jev;

/// <summary>
/// Wraps a shared <see cref="ITypeSafeClient"/> and sends a fixed model on every System One call that names none. The caller's
/// <see cref="SystemOneOptions"/> is never mutated: when it names no model, a copy is sent. The wrapper does not own the inner client.
/// </summary>
/// <param name="inner">The shared SDK client.</param>
/// <param name="model">The model to send when the caller names none.</param>
internal sealed class ModelPinnedTypeSafeClient(ITypeSafeClient inner, string model) : ITypeSafeClient
{
    /// <inheritdoc />
    public IModelsResource Models => inner.Models;

    /// <inheritdoc />
    public Task<SystemOneResponse> SystemOneAsync(
        JsonContent state,
        IReadOnlyDictionary<string, Question> questions,
        SystemOneOptions? options = null,
        CancellationToken cancellationToken = default) =>
        inner.SystemOneAsync(state, questions, Pin(options), cancellationToken);

    /// <inheritdoc />
    public Task<TResponse> SystemOneAsync<TResponse>(
        JsonContent state,
        IReadOnlyDictionary<string, Question> questions,
        SystemOneOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResponse : SystemOneResponse, new() =>
        inner.SystemOneAsync<TResponse>(state, questions, Pin(options), cancellationToken);

    /// <inheritdoc />
    public Task<TResponse> SystemOneAsync<TResponse>(
        JsonContent state,
        IReadOnlyDictionary<string, Question> questions,
        SystemOneOptions? options,
        JsonTypeInfo<TResponse> typeInfo,
        CancellationToken cancellationToken = default)
        where TResponse : class =>
        inner.SystemOneAsync(state, questions, Pin(options), typeInfo, cancellationToken);

    /// <inheritdoc />
    public Task<TResponse> SystemOneAsync<TResponse>(
        JsonContent state,
        IReadOnlyDictionary<string, Question> questions,
        SystemOneOptions? options,
        JsonSerializerOptions serializerOptions,
        CancellationToken cancellationToken = default)
        where TResponse : class =>
        inner.SystemOneAsync<TResponse>(state, questions, Pin(options), serializerOptions, cancellationToken);

    private SystemOneOptions Pin(SystemOneOptions? options)
    {
        if (options is null)
        {
            return new SystemOneOptions { Model = model };
        }

        if (options.Model is not null)
        {
            return options;
        }

        return new SystemOneOptions
        {
            Model = model,
            ExtraBody = options.ExtraBody,
            Retry = options.Retry,
            Timeout = options.Timeout,
            ExtraHeaders = options.ExtraHeaders,
        };
    }
}
