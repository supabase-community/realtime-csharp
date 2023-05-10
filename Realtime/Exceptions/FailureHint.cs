namespace Supabase.Realtime.Exceptions;

public class FailureHint
{
    public enum Reason
    {
        Unknown,
        PushTimeout
    }
    
    //public static Reason DetectReason(Socket gte) {}
}