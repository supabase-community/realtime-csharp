using Newtonsoft.Json;
using Postgrest.Models;
using Supabase.Realtime.Converters;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.Socket;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Websocket.Client;
using static Supabase.Realtime.Socket.SocketStateChangedEventArgs;

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
		public bool IsConnected => connection.IsRunning;

		/// <summary>
		/// Invoked when the socket state changes.
		/// </summary>
		public event EventHandler<SocketStateChangedEventArgs>? StateChanged;

		/// <summary>
		/// Invoked when a message has been recieved and decoded.
		/// </summary>
		public event EventHandler<SocketResponseEventArgs>? OnMessage;

		public event EventHandler<SocketResponseEventArgs>? OnHeartbeat;

		private string endpoint;
		private ClientOptions options;
		private WebsocketClient connection;

		private Task? heartbeatTask;
		private CancellationTokenSource? heartbeatTokenSource;

		private bool hasPendingHeartbeat = false;
		private string? pendingHeartbeatRef = null;

		private Task? reconnectTask;
		private CancellationTokenSource? reconnectTokenSource;

		private List<Task> buffer = new List<Task>();
		private bool isReconnecting = false;
		private bool hasConnectBeenCalled = false;

		private JsonSerializerSettings serializerSettings;

		private string endpointUrl
		{
			get
			{
				var parameters = new Dictionary<string, string?> {
					{ "token", options.Parameters.Token },
					{ "apikey", options.Parameters.ApiKey }
				};

				return string.Format($"{endpoint}?{Utils.QueryString(parameters)}");
			}
		}

		/// <summary>
		/// Initializes this Socket instance.
		/// </summary>
		/// <param name="endpoint"></param>
		/// <param name="options"></param>
		public RealtimeSocket(string endpoint, ClientOptions options, JsonSerializerSettings serializerSettings)
		{
			this.serializerSettings = serializerSettings;
			this.endpoint = $"{endpoint}/{Constants.TRANSPORT_WEBSOCKET}";
			this.options = options;

			if (!options.Headers.ContainsKey("X-Client-Info"))
			{
				options.Headers.Add("X-Client-Info", Core.Util.GetAssemblyVersion(typeof(Client)));
			}

			connection = new WebsocketClient(new Uri(endpointUrl));
		}

		void IDisposable.Dispose()
		{
			DisposeConnection();
		}

		/// <summary>
		/// Dispose of the web socket connection.
		/// </summary>
		private async void DisposeConnection()
		{
			if (connection == null) return;

			await connection.Stop(WebSocketCloseStatus.NormalClosure, string.Empty);
			connection.Dispose();
		}

		/// <summary>
		/// Connects to a socket server and registers event listeners.
		/// </summary>
		public async Task Connect()
		{
			if (connection.IsRunning || hasConnectBeenCalled) return;

			connection.ReconnectTimeout = TimeSpan.FromSeconds(120);
			connection.ErrorReconnectTimeout = TimeSpan.FromSeconds(30);

			connection.ReconnectionHappened.Subscribe(reconnectionInfo =>
			{
				if (reconnectionInfo.Type != ReconnectionType.Initial)
					isReconnecting = true;

				OnConnectionOpened(this, new EventArgs { });
			});

			connection.DisconnectionHappened.Subscribe(disconnectionInfo =>
			{
				if (disconnectionInfo.Exception != null)
				{
					OnConnectionError(this, disconnectionInfo);
				}
				else
				{
					OnConnectionClosed(this, disconnectionInfo);
				}
			});

			connection.MessageReceived.Subscribe(msg =>
			{
				OnConnectionMessage(this, msg);
			});

			hasConnectBeenCalled = true;

			await connection.Start();
		}

		/// <summary>
		/// Disconnects from the socket server.
		/// </summary>
		/// <param name="code"></param>
		/// <param name="reason"></param>
		public void Disconnect(WebSocketCloseStatus code = WebSocketCloseStatus.NormalClosure, string reason = "") => connection?.Stop(code, reason);

		/// <summary>
		/// Pushes formatted data to the socket server.
		///
		/// If the connection is not alive, the data will be placed into a buffer to be sent when reconnected.
		/// </summary>
		/// <param name="data"></param>
		public void Push(SocketRequest data)
		{
			options.Logger("push", $"{data.Topic} {data.Event} ({data.Ref})", data.Payload);

			var task = new Task(() => options.Encode!(data, encoded => connection.Send(encoded)));

			if (connection.IsRunning)
				task.Start();
			else
				buffer.Add(task);
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

			EventHandler<SocketResponseEventArgs>? handler = null;

			handler = (sender, args) =>
			{
				if (args.Response.Ref == pingRef)
				{
					OnMessage -= handler;
					tsc.SetResult((DateTime.Now - start).TotalMilliseconds);
				}
			};

			OnMessage += handler;

			Push(new SocketRequest { Topic = "phoenix", Event = "heartbeat", Ref = pingRef });

			return tsc.Task;
		}

		/// <summary>
		/// Maintains a heartbeat connection with the socket server to prevent disconnection.
		/// </summary>
		private void SendHeartbeat()
		{
			if (!connection.IsRunning) return;

			if (hasPendingHeartbeat)
			{
				hasPendingHeartbeat = false;
				options.Logger("transport", "heartbeat timeout. Attempting to re-establish connection.", null);
				connection.Stop(WebSocketCloseStatus.NormalClosure, "heartbeat timeout");
				return;
			}

			pendingHeartbeatRef = MakeMsgRef();

			Push(new SocketRequest { Topic = "phoenix", Event = "heartbeat", Ref = pendingHeartbeatRef.ToString() });
		}

		/// <summary>
		/// Called when the socket opens, registers the heartbeat thread and cancels the reconnection timer.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void OnConnectionOpened(object sender, EventArgs args)
		{
			// Was a reconnection attempt
			if (isReconnecting == true)
				StateChanged?.Invoke(sender, new SocketStateChangedEventArgs(ConnectionState.Reconnected, args));

			// Reset flag for reconnections
			isReconnecting = false;

			options.Logger("transport", $"connected to ${endpointUrl}", null);

			if (reconnectTokenSource != null)
				reconnectTokenSource.Cancel();

			if (heartbeatTokenSource != null)
				heartbeatTokenSource.Cancel();

			hasPendingHeartbeat = false;
			heartbeatTokenSource = new CancellationTokenSource();
			heartbeatTask = Task.Run(async () =>
			{
				while (!heartbeatTokenSource.IsCancellationRequested)
				{
					SendHeartbeat();
					await Task.Delay(options.HeartbeatInterval, heartbeatTokenSource.Token);
				}
			}, heartbeatTokenSource.Token);

			// Send any pending `Push` messages that were queued while socket was disconnected.
			FlushBuffer();

			StateChanged?.Invoke(sender, new SocketStateChangedEventArgs(ConnectionState.Open, args));
		}

		/// <summary>
		/// Parses a recieved socket message into a non-generic type.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void OnConnectionMessage(object sender, ResponseMessage args)
		{
			options.Decode!(args.Text, decoded =>
			{
				if (decoded == null) return;

				options.Logger("receive", $"{args.Text} {decoded?.Topic} {decoded?.Event} ({decoded?.Ref})", null);

				// Send Separate heartbeat event
				if (decoded!.Ref == pendingHeartbeatRef)
					OnHeartbeat?.Invoke(sender, new SocketResponseEventArgs(decoded));

				decoded!.Json = args.Text;

				OnMessage?.Invoke(sender, new SocketResponseEventArgs(decoded));
			});

			StateChanged?.Invoke(sender, new SocketStateChangedEventArgs(ConnectionState.Message, new EventArgs()));
		}

		private void OnConnectionError(object sender, DisconnectionInfo args)
		{
			AttemptReconnection();

			StateChanged?.Invoke(sender, new SocketStateChangedEventArgs(ConnectionState.Error, new EventArgs()));
		}

		/// <summary>
		/// Begins the reconnection thread with a progressively increasing interval.
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="args"></param>
		private void OnConnectionClosed(object sender, DisconnectionInfo args)
		{
			options.Logger("transport", "close", args);

			AttemptReconnection();

			StateChanged?.Invoke(sender, new SocketStateChangedEventArgs(ConnectionState.Close, new EventArgs()));
		}

		private void AttemptReconnection()
		{
			// Make sure that the connection closed handler doesn't get called repeatedly.
			if (isReconnecting) return;

			if (reconnectTokenSource != null)
				reconnectTokenSource.Cancel();

			reconnectTokenSource = new CancellationTokenSource();
			reconnectTask = Task.Run(async () =>
			{
				isReconnecting = true;

				var tries = 1;
				while (!reconnectTokenSource.IsCancellationRequested)
				{
					// Delay reconnection for a set interval, by default it increases the
					// time between executions.
					var delay = options.ReconnectAfterInterval(tries++);
					options.Logger("transport", "reconnection:attempt", $"Tries: {tries}, Delay: {delay.Seconds}s, Started: {DateTime.Now.ToShortTimeString()}");

					await connection.Stop(WebSocketCloseStatus.EndpointUnavailable, "Closed");

					await Task.Delay(delay, reconnectTokenSource.Token);

					await Connect();
				}
			}, reconnectTokenSource.Token);
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
		/// Flushes `Push` requests added while a socket was disconnected.
		/// </summary>
		private void FlushBuffer()
		{
			if (connection.IsRunning)
			{
				foreach (var item in buffer)
					item.Start();

				buffer.Clear();
			}
		}
	}
}
