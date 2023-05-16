namespace Supabase.Realtime.Exceptions;

public class FailureHint
{
    public enum Reason
    {
        Unknown,
        PushTimeout,
        ChannelNotOpen,
        JoinFailure
    }
    
    //public static Reason DetectReason(Socket gte) {}
}