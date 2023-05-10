using System;

namespace Supabase.Realtime.Exceptions;

public class RealtimeException: Exception
{
    
    public RealtimeException(string? message) : base(message) { }
    public RealtimeException(string? message, Exception? innerException) : base(message, innerException) { }

    public string? Content { get; internal set; }
    
    public void AddReason()
    {
        // Reason = FailureHint.DetectReason(this);
        //Debug.WriteLine(Content);
    }
    
    public FailureHint.Reason Reason { get; internal set; }
}