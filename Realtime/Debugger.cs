using System;
using System.Collections.Generic;
using Supabase.Realtime.Exceptions;
using Supabase.Realtime.Interfaces;

namespace Supabase.Realtime;

/// <summary>
/// A Singleton used for debug notifications
/// </summary>
internal class Debugger : IRealtimeDebugger
{
    private static Debugger? _instance { get; set; }

    /// <summary>
    /// Returns the Singleton Instance.
    /// </summary>
    public static Debugger Instance => _instance ??= new Debugger();

    private Debugger()
    {
    }

    private readonly List<IRealtimeDebugger.DebugEventHandler> _debugListeners = new();

    /// <summary>
    /// Adds a debug listener
    /// </summary>
    /// <param name="handler"></param>
    public void AddDebugHandler(IRealtimeDebugger.DebugEventHandler handler)
    {
        if (!_debugListeners.Contains(handler))
            _debugListeners.Add(handler);
    }

    /// <summary>
    /// Removes a debug handler.
    /// </summary>
    /// <param name="handler"></param>
    public void RemoveDebugHandler(IRealtimeDebugger.DebugEventHandler handler)
    {
        if (_debugListeners.Contains(handler))
            _debugListeners.Remove(handler);
    }

    /// <summary>
    /// Clears debug handlers.
    /// </summary>
    public void ClearDebugHandlers() =>
        _debugListeners.Clear();

    /// <summary>
    /// Notifies debug listeners.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    public void Log(object sender, string message, Exception? exception = null)
    {
        foreach (var l in _debugListeners.ToArray())
            l.Invoke(sender, message, exception);
    }
}