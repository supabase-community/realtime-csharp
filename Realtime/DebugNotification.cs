using System;
using System.Collections.Generic;

namespace Supabase.Realtime;

public class DebugNotification
{
    private readonly List<Action<string, Exception?>> debugListeners = new();

    public void AddDebugListener(Action<string, Exception?> listener)
    {
        debugListeners.Add(listener);
    }

    public void Log(string message, Exception? e = null)
    {
        foreach (var l in debugListeners)
            l.Invoke(message, e);
    }
}