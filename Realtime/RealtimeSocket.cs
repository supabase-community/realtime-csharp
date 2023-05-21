﻿using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Supabase.Realtime.Exceptions;
using Websocket.Client;
using Websocket.Client.Models;
using static Supabase.Realtime.Constants;

namespace Supabase.Realtime
{
    /// <summary>
    /// Socket connection handler.
    /// </summary>
    public class RealtimeSocket : IDisposable, IRealtimeSocket
    {
        /// <summary>
        /// Returns whether or not the connection is alive.
        /// </summary>
        public bool IsConnected => _connection.IsRunning;

        /// <summary>
        /// The Socket Endpoint
        /// </summary>
        private string EndpointUrl
        {
            get
            {
                var parameters = new Dictionary<string, string?>
                {
                    { "token", _options.Parameters.Token },
                    { "apikey", _options.Parameters.ApiKey },
                    { "vsn", "1.0.0" }
                };

                return string.Format($"{_endpoint}?{Utils.QueryString(parameters)}");
            }
        }

        /// <summary>
        /// Handlers for notifications of state changes.
        /// </summary>
        private readonly List<IRealtimeSocket.StateEventHandler> _socketEventHandlers = new();

        /// <summary>
        /// Handlers for notifications of message events.
        /// </summary>
        private readonly List<IRealtimeSocket.MessageEventHandler> _messageEventHandlers = new();

        /// <summary>
        /// Handlers for notifications of heartbeat events.
        /// </summary>
        private readonly List<IRealtimeSocket.HeartbeatEventHandler> _heartbeatEventHandlers = new();

        private readonly string _endpoint;
        private readonly ClientOptions _options;
        private readonly WebsocketClient _connection;

        private CancellationTokenSource? _heartbeatTokenSource;
        private CancellationTokenSource? _reconnectTokenSource;

        private bool _hasSuccessfullyConnectedOnce;
        private bool _hasPendingHeartbeat;
        private string? _pendingHeartbeatRef;

        private readonly List<Task> _buffer = new();
        private bool _isReconnecting;
        private bool _hasConnectBeenCalled;

        /// <summary>
        /// Initializes this Socket instance.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="options"></param>
        public RealtimeSocket(string endpoint, ClientOptions options)
        {
            _endpoint = $"{endpoint}/{TransportWebsocket}";
            _options = options;

            if (!options.Headers.ContainsKey("X-Client-Info"))
                options.Headers.Add("X-Client-Info", Core.Util.GetAssemblyVersion(typeof(Client)));

            _connection = new WebsocketClient(new Uri(EndpointUrl));
        }

        void IDisposable.Dispose()
        {
            _heartbeatTokenSource?.Cancel();
            _reconnectTokenSource?.Cancel();
            DisposeConnection();
        }

        /// <summary>
        /// Connects to a socket server and registers event listeners.
        /// </summary>
        public async Task Connect()
        {
            // Ignore calling connect multiple times.
            if (_connection.IsRunning || _hasConnectBeenCalled) return;

            _connection.ReconnectTimeout = TimeSpan.FromSeconds(120);
            _connection.ErrorReconnectTimeout = TimeSpan.FromSeconds(30);

            _connection.ReconnectionHappened.Subscribe(HandleSocketReconnectionHappened);
            _connection.DisconnectionHappened.Subscribe(HandleSocketDisconnectionHappened);
            _connection.MessageReceived.Subscribe(HandleSocketMessage);

            _hasConnectBeenCalled = true;

            await _connection.StartOrFail();
        }

        /// <summary>
        /// Disconnects from the socket server.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="reason"></param>
        public void Disconnect(WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure, string reason = "")
        {
            _reconnectTokenSource?.Cancel();
            _heartbeatTokenSource?.Cancel();

            _connection?.Stop(code, reason);
        }

        #region Event Listeners

        /// <summary>
        /// Adds a listener to be notified when the socket state changes.
        /// </summary>
        /// <param name="handler"></param>
        public void AddStateChangedHandler(IRealtimeSocket.StateEventHandler handler)
        {
            if (!_socketEventHandlers.Contains(handler))
                _socketEventHandlers.Add(handler);
        }

        /// <summary>
        /// Removes a specified listener from socket state changes.
        /// </summary>
        /// <param name="handler"></param>
        public void RemoveStateChangedHandler(IRealtimeSocket.StateEventHandler handler)
        {
            if (_socketEventHandlers.Contains(handler))
                _socketEventHandlers.Remove(handler);
        }

        /// <summary>
        /// Notifies all listeners that the socket state has changed.
        /// </summary>
        /// <param name="newState"></param>
        private void NotifySocketStateChange(SocketState newState)
        {
            if (!_socketEventHandlers.Any()) return;

            foreach (var handler in _socketEventHandlers.ToArray())
                handler.Invoke(this, newState);
        }

        /// <summary>
        /// Clears all of the listeners from receiving event state changes.
        /// </summary>
        public void ClearStateChangedHandlers() =>
            _socketEventHandlers.Clear();

        /// <summary>
        /// Adds a listener to be notified when a message is received.
        /// </summary>
        /// <param name="handler"></param>
        public void AddMessageReceivedHandler(IRealtimeSocket.MessageEventHandler handler)
        {
            if (_messageEventHandlers.Contains(handler))
                return;

            _messageEventHandlers.Add(handler);
        }

        /// <summary>
        /// Removes a specified listener from messages received.
        /// </summary>
        /// <param name="handler"></param>
        public void RemoveMessageReceivedHandler(IRealtimeSocket.MessageEventHandler handler)
        {
            if (!_messageEventHandlers.Contains(handler))
                return;

            _messageEventHandlers.Remove(handler);
        }

        /// <summary>
        /// Notifies all listeners that the socket has received a message
        /// </summary>
        /// <param name="heartbeat"></param>
        private void NotifyMessageReceived(SocketResponse heartbeat)
        {
            foreach (var handler in _messageEventHandlers.ToArray())
                handler.Invoke(this, heartbeat);
        }

        /// <summary>
        /// Clears all of the listeners from receiving event state changes.
        /// </summary>
        public void ClearMessageReceivedHandlers() =>
            _messageEventHandlers.Clear();

        /// <summary>
        /// Adds a listener to be notified when a message is received.
        /// </summary>
        /// <param name="handler"></param>
        public void AddHeartbeatHandler(IRealtimeSocket.HeartbeatEventHandler handler)
        {
            if (!_heartbeatEventHandlers.Contains(handler))
                _heartbeatEventHandlers.Add(handler);
        }

        /// <summary>
        /// Removes a specified listener from messages received.
        /// </summary>
        /// <param name="handler"></param>
        public void RemoveHeartbeatHandler(IRealtimeSocket.HeartbeatEventHandler handler)
        {
            if (_heartbeatEventHandlers.Contains(handler))
                _heartbeatEventHandlers.Remove(handler);
        }

        /// <summary>
        /// Notifies all listeners that the socket has received a heartbeat
        /// </summary>
        /// <param name="heartbeat"></param>
        private void NotifyHeartbeatReceived(SocketResponse heartbeat)
        {
            foreach (var handler in _heartbeatEventHandlers.ToArray())
                handler.Invoke(this, heartbeat);
        }

        /// <summary>
        /// Clears all of the listeners from receiving event state changes.
        /// </summary>
        public void ClearHeartbeatHandlers() =>
            _heartbeatEventHandlers.Clear();
        
        #endregion
        
        /// <summary>
        /// Pushes formatted data to the socket server.
        ///
        /// If the connection is not alive, the data will be placed into a buffer to be sent when reconnected.
        /// </summary>
        /// <param name="data"></param>
        public void Push(SocketRequest data)
        {
            _options.Logger("push", $"{data.Topic} {data.Event} ({data.Ref})", data.Payload);

            var task = new Task(() => _options.Encode!(data, encoded => _connection.Send(encoded)));

            if (_connection.IsRunning)
                task.Start();
            else
                _buffer.Add(task);
        }

        /// <summary>
        /// Returns the latency (in millis) of roundtrip time from socket to server and back.
        /// </summary>
        /// <returns></returns>
        public Task<double> GetLatency()
        {
            var tsc = new TaskCompletionSource<double>();
            var start = DateTime.Now;
            var pingRef = Guid.NewGuid().ToString();

            // ReSharper disable once ConvertToLocalFunction
            IRealtimeSocket.MessageEventHandler? messageHandler = null;
            messageHandler = (_, messageResponse) =>
            {
                if (messageResponse.Ref != pingRef) return;

                RemoveMessageReceivedHandler(messageHandler!);
                tsc.SetResult((DateTime.Now - start).TotalMilliseconds);
            };
            AddMessageReceivedHandler(messageHandler);

            Push(new SocketRequest { Topic = "phoenix", Event = "heartbeat", Ref = pingRef });

            return tsc.Task;
        }

        /// <summary>
        /// Maintains a heartbeat connection with the socket server to prevent disconnection.
        /// </summary>
        private void SendHeartbeat()
        {
            if (!_connection.IsRunning) return;

            if (_hasPendingHeartbeat)
            {
                _hasPendingHeartbeat = false;
                _options.Logger("transport", "heartbeat timeout. Attempting to re-establish connection.", null);
                _connection.Stop(WebSocketCloseStatus.NormalClosure, "heartbeat timeout");
                return;
            }

            _pendingHeartbeatRef = MakeMsgRef();

            Push(new SocketRequest
            {
                Topic = "phoenix", Event = "heartbeat", Ref = _pendingHeartbeatRef,
                Payload = new Dictionary<string, string>()
            });
        }

        /// <summary>
        /// Called when the socket opens, registers the heartbeat thread and cancels the reconnection timer.
        /// </summary>
        private void HandleSocketOpened()
        {
            _hasSuccessfullyConnectedOnce = true;

            // Was a reconnection attempt
            if (_isReconnecting)
                NotifySocketStateChange(SocketState.Reconnect);

            // Reset flag for reconnections
            _isReconnecting = false;

            _options.Logger("transport", $"connected to ${EndpointUrl}", null);

            _reconnectTokenSource?.Cancel();
            _heartbeatTokenSource?.Cancel();

            _hasPendingHeartbeat = false;
            _heartbeatTokenSource = new CancellationTokenSource();
            Task.Run(EmitHeartbeat, _heartbeatTokenSource.Token);

            // Send any pending `Push` messages that were queued while socket was disconnected.
            FlushBuffer();

            NotifySocketStateChange(SocketState.Open);
        }

        private async Task EmitHeartbeat()
        {
            while (_heartbeatTokenSource is { IsCancellationRequested: false })
            {
                SendHeartbeat();
                await Task.Delay(_options.HeartbeatInterval, _heartbeatTokenSource.Token);
            }
        }

        #region Socket Event Handlers
        
        /// <summary>
        /// The socket has reconnected (or connected)
        /// </summary>
        /// <param name="reconnectionInfo"></param>
        private void HandleSocketReconnectionHappened(ReconnectionInfo reconnectionInfo)
        {
            if (reconnectionInfo.Type != ReconnectionType.Initial) _isReconnecting = true;
            HandleSocketOpened();
        }

        /// <summary>
        /// The socket has disconnected, called either through a socket closing or erroring.
        /// </summary>
        /// <param name="disconnectionInfo"></param>
        private void HandleSocketDisconnectionHappened(DisconnectionInfo disconnectionInfo)
        {
            if (disconnectionInfo.Exception != null)
                HandleSocketError(disconnectionInfo);
            else
                HandleSocketClosed(disconnectionInfo);
        }

        /// <summary>
        /// Parses a received socket message into a non-generic type.
        /// </summary>
        /// <param name="args"></param>
        private void HandleSocketMessage(ResponseMessage args)
        {
            _options.Decode!(args.Text, decoded =>
            {
                _options.Logger("receive", args.Text, null);

                // Send Separate heartbeat event
                if (decoded!.Ref == _pendingHeartbeatRef)
                {
                    NotifyHeartbeatReceived(decoded);
                    return;
                }

                decoded!.Json = args.Text;
                NotifyMessageReceived(decoded);
            });
        }

        /// <summary>
        /// Handles socket errors, attempts reconnection if the error occurs after a successful connection has been
        /// established at least once.
        /// </summary>
        /// <param name="disconnectionInfo"></param>
        /// <exception cref="Exception"></exception>
        private void HandleSocketError(DisconnectionInfo? disconnectionInfo = null)
        {
            if (!_hasSuccessfullyConnectedOnce && disconnectionInfo is { Exception: not RealtimeException })
                throw disconnectionInfo.Exception;

            AttemptReconnection();
        }

        /// <summary>
        /// Begins the reconnection thread with a progressively increasing interval.
        /// </summary>
        private void HandleSocketClosed(DisconnectionInfo? disconnectionInfo = null)
        {
            _options.Logger("transport", "close", disconnectionInfo);

            if (disconnectionInfo?.Type != DisconnectionType.ByUser)
                AttemptReconnection();
        }
        
        #endregion

        /// <summary>
        /// Handles reconnecting.
        /// </summary>
        private void AttemptReconnection()
        {
            // Make sure that the connection closed handler doesn't get called repeatedly.
            if (_isReconnecting) return;

            var tries = 1;
            _reconnectTokenSource?.Cancel();
            _reconnectTokenSource = new CancellationTokenSource();

            async Task Reconnect()
            {
                _isReconnecting = true;

                while (!_reconnectTokenSource.IsCancellationRequested)
                {
                    // Delay reconnection for a set interval, by default it increases the
                    // time between executions.
                    var delay = _options.ReconnectAfterInterval(tries++);
                    _options.Logger("transport", "reconnection:attempt",
                        $"Tries: {tries}, Delay: {delay.Seconds}s, Started: {DateTime.Now.ToShortTimeString()}");

                    await _connection.Stop(WebSocketCloseStatus.EndpointUnavailable, "Closed");

                    await Task.Delay(delay, _reconnectTokenSource.Token);

                    await Connect();
                }
            }

            Task.Run(Reconnect, _reconnectTokenSource.Token);
        }

        /// <summary>
        /// Generates an incrementing identifier for message references - this reference is used
        /// to coordinate requests with their responses.
        /// </summary>
        /// <returns></returns>
        public string MakeMsgRef() => Guid.NewGuid().ToString();

        /// <summary>
        /// Returns the expected reply event name based off a generated message ref.
        /// </summary>
        /// <param name="msgRef"></param>
        /// <returns></returns>
        public string ReplyEventName(string msgRef) => $"chan_reply_{msgRef}";

        /// <summary>
        /// Dispose of the web socket connection.
        /// </summary>
        private async void DisposeConnection()
        {
            await _connection.Stop(WebSocketCloseStatus.NormalClosure, string.Empty);
            _connection.Dispose();
        }

        /// <summary>
        /// Flushes `Push` requests added while a socket was disconnected.
        /// </summary>
        private void FlushBuffer()
        {
            if (_connection.IsRunning)
            {
                foreach (var item in _buffer)
                    item.Start();

                _buffer.Clear();
            }
        }
    }
}