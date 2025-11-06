using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Supabase.Realtime.Channel;

/// <summary>
/// Represents configuration options for a Realtime channel.
/// </summary>
/// <remarks>
/// This class contains all the necessary configuration options for establishing and maintaining
/// a Realtime channel connection, including authentication, parameters, and serialization settings.
/// </remarks>
public class ChannelOptions
{
    /// <summary>
    /// A function that returns the current access token.
    /// </summary>
    public Func<string?> RetrieveAccessToken { get; private set; }

    /// <summary>
    /// Parameters that are sent to the channel when opened (JSON Serializable)
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }

    /// <summary>
    /// The Client Options
    /// </summary>
    public ClientOptions ClientOptions { get; }

    /// <summary>
    /// The Serializer Settings
    /// </summary>
    public JsonSerializerSettings SerializerSettings { get; }

    /// <summary>
    /// Gets a value indicating whether the channel is private.
    /// </summary>
    /// <value>
    /// <c>true</c> if the channel is private; otherwise, <c>false</c>.
    /// </value>
    public bool IsPrivate { get; } = false;

    /// <summary>
    /// The Channel Options (typically only called from within the <see cref="Client"/>)
    /// </summary>
    /// <param name="clientOptions">The client configuration options.</param>
    /// <param name="retrieveAccessToken">A function that returns the current access token.</param>
    /// <param name="serializerSettings">The JSON serializer settings to be used for message serialization.</param>
    public ChannelOptions(
        ClientOptions clientOptions,
        Func<string?> retrieveAccessToken,
        JsonSerializerSettings serializerSettings
    )
    {
        ClientOptions = clientOptions;
        SerializerSettings = serializerSettings;
        RetrieveAccessToken = retrieveAccessToken;
    }

    /// <summary>
    /// The Channel Options (typically only called from within the <see cref="Client"/>)
    /// </summary>
    /// <param name="clientOptions">The client configuration options.</param>
    /// <param name="retrieveAccessToken">A function that returns the current access token.</param>
    /// <param name="serializerSettings">The JSON serializer settings to be used for message serialization.</param>
    /// <param name="isPrivate">A value indicating whether the channel is private.</param>
    public ChannelOptions(
        ClientOptions clientOptions,
        Func<string?> retrieveAccessToken,
        JsonSerializerSettings serializerSettings,
        bool isPrivate
    )
    {
        ClientOptions = clientOptions;
        SerializerSettings = serializerSettings;
        RetrieveAccessToken = retrieveAccessToken;
        IsPrivate = isPrivate;
    }
}
