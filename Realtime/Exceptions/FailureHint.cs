namespace Supabase.Realtime.Exceptions;

/// <summary>
/// A failure hint
/// </summary>
public static class FailureHint
{
    /// <summary>
    /// Reasons for a failure
    /// </summary>
    public enum Reason
    {
        /// <summary>
        /// Catchall for any kind of failure that is presently untyped.
        /// </summary>
        Unknown,
        /// <summary>
        /// A push timeout
        /// </summary>
        PushTimeout,
        /// <summary>
        /// Channel is not open
        /// </summary>
        ChannelNotOpen,
        /// <summary>
        /// Channel cannot be joined
        /// </summary>
        ChannelJoinFailure
    }
}