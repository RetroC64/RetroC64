// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace RetroC64.Vice.Monitor.Responses;

/// <summary>
/// Base class for all monitor responses.
/// </summary>
public abstract class MonitorResponse(MonitorResponseType responseType)
{
    /// <summary>
    /// Gets the response type.
    /// </summary>
    public MonitorResponseType ResponseType { get; } = responseType;

    /// <summary>
    /// Gets the error kind for this response.
    /// </summary>
    public MonitorErrorKind Error { get; protected set; }

    /// <summary>
    /// Gets the request identifier associated with this response.
    /// </summary>
    public MonitorRequestId RequestId { get; private set; }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append($"ResponseType: {ResponseType}, Error: {Error}, RequestId: {RequestId}");
        AppendMembers(builder);
        return builder.ToString();
    }

    protected virtual void AppendMembers(StringBuilder builder)
    {
        // No-op for base
    }
    /// <summary>
    /// Deserializes the response body from the specified span.
    /// </summary>
    /// <param name="body">The response body.</param>
    public abstract void Deserialize(ReadOnlySpan<byte> body);

    // Factory to create and deserialize the correct response type
    internal static MonitorResponse Create(MonitorResponseType responseType, MonitorErrorKind error, MonitorRequestId requestId, ReadOnlySpan<byte> body)
    {
        MonitorResponse response = responseType switch
        {
            MonitorResponseType.CheckpointInfo => new CheckpointResponse(),
            MonitorResponseType.RegisterInfo => new RegisterResponse(),
            MonitorResponseType.Jam => new JamResponse(),
            MonitorResponseType.Stopped => new StoppedResponse(),
            MonitorResponseType.Resumed => new ResumedResponse(),
            MonitorResponseType.ResourceGet => new ResourceGetResponse(),
            MonitorResponseType.BanksAvailable => new BanksAvailableResponse(),
            MonitorResponseType.RegistersAvailable => new RegistersAvailableResponse(),
            MonitorResponseType.DisplayGet => new DisplayGetResponse(),
            MonitorResponseType.ViceInfo => new ViceInfoResponse(),
            MonitorResponseType.PaletteGet => new PaletteGetResponse(),
            _ => new GenericResponse(responseType)
        };

        response.RequestId = requestId;
        response.Error = error;
        if (error == MonitorErrorKind.None)
        {
            response.Deserialize(body);
        }
        return response;
    }

    protected static string ReadString(ReadOnlySpan<byte> body)
    {
        var length = body[0];
        return System.Text.Encoding.ASCII.GetString(body.Slice(1, length));
    }
}