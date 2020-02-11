﻿// <copyright file="LogService.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.AdminPanelBlazor.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.SignalR.Client;
    using MUnique.Log4Net.CoreSignalR;

    /// <summary>
    /// Service which connects to the log hub.
    /// </summary>
    public class LogService
    {
        private readonly HubConnection connection;

        private long idOfLastMessage;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogService"/> class.
        /// </summary>
        public LogService()
        {
            this.connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:1234/signalr/hubs/logHub")
                .Build();

            this.connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await this.Connect();
            };

            this.connection.On<string, LogEventData, long>("OnLoggedEvent", this.OnLoggedEvent);
            this.connection.On<string[], LogEntry[]>("Initialize", this.OnInitialize);

            Task EnsureConnected()
            {
                if (this.connection.State == HubConnectionState.Disconnected)
                {
                    return this.Connect();
                }

                return Task.CompletedTask;
            }

            this.Initialization = EnsureConnected();
        }

        /// <summary>
        /// Gets the known loggers.
        /// </summary>
        public ICollection<string> Loggers { get; private set; }

        /// <summary>
        /// Gets the captured entries.
        /// </summary>
        public LinkedList<LogEventData> Entries { get; private set; }

        /// <summary>
        /// Occurs when a log event was received.
        /// </summary>
        public EventHandler<LogEntryReceivedEventArgs> LogEventReceived;

        /// <summary>
        /// Occurs when the connection state to the hub changed.
        /// </summary>
        public EventCallback<bool>? IsConnectedChanged;

        /// <summary>
        /// Gets a value indicating whether this instance is connected to the log hub.
        /// </summary>
        public bool IsConnected => this.connection.State == HubConnectionState.Connected;

        /// <summary>
        /// Gets the initialization task.
        /// </summary>
        public Task Initialization { get; }

        /// <summary>
        /// Gets or sets the maximum size of <see cref="Entries"/>.
        /// </summary>
        public int MaximumEntries { get; set; } = 500;

        private void OnInitialize(string[] loggers, LogEntry[] cachedEntries)
        {
            this.Loggers = loggers.ToList();
            this.Entries = new LinkedList<LogEventData>(cachedEntries.Select(entry => entry.LoggingEvent));
        }

        private async Task Connect()
        {
            await this.connection.StartAsync();
            await this.connection.InvokeAsync("SubscribeToGroupWithMessageOffset", "MyGroup", this.idOfLastMessage);
            var isConnectedChanged = this.IsConnectedChanged;
            if (isConnectedChanged != null)
            {
                await isConnectedChanged.Value.InvokeAsync(this.IsConnected);
            }
        }

        private void OnLoggedEvent(string formattedEvent, LogEventData entry, long id)
        {
            this.idOfLastMessage = id;
            this.Entries.AddLast(entry);
            while (this.Entries.Count > this.MaximumEntries)
            {
                this.Entries.RemoveFirst();
            }

            this.LogEventReceived?.Invoke(this, new LogEntryReceivedEventArgs(entry));
        }
    }
}
