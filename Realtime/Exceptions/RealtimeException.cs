using System;

namespace Supabase.Realtime.Exceptions;

/// <summary>
/// An Exception thrown within <see cref="Realtime"/>
/// </summary>
public class RealtimeException: Exception
{
    /// <inheritdoc />
    public RealtimeException(string? message) : base(message) { }

    /// <inheritdoc />
    public RealtimeException(string? message, Exception? innerException) : base(message, innerException) { }
    
    /// <summary>
    /// A specific reason for this exception, as provided by this library.
    /// </summary>
    public FailureHint.Reason Reason { get; internal set; }
}