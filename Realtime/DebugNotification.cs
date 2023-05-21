using System;
using System.Collections.Generic;

namespace Supabase.Realtime;

/// <summary>
/// 
/// </summary>
public class DebugNotification
{
    private readonly List<Action<string, Exception?>> _debugListeners = new();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="listener"></param>
    public void AddDebugListener(Action<string, Exception?> listener)
    {
        if (!_debugListeners.Contains(listener))
            _debugListeners.Add(listener);
    }

    /// <summary>
    /// Notifies debug listeners.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="e"></param>
    public void Log(string message, Exception? e = null)
    {
        foreach (var l in _debugListeners)
            l.Invoke(message, e);
    }
}