﻿using System;
using System.Net;

namespace Engine.Session
{
    /// <summary>Possible connection states for the client.</summary>
    public enum ClientState
    {
        Unconnected,
        Connecting,
        Connected
    }

    /// <summary>Interface for client side representations of a session.</summary>
    public interface IClientSession : ISession
    {
        /// <summary>Called when we receive information about an open game. Only possibly called after Search() was called.</summary>
        event EventHandler<GameInfoReceivedEventArgs> GameInfoReceived;

        /// <summary>Called when we successfully joined a server (i.e. Join() was called and the server accepted us).</summary>
        event EventHandler<JoinResponseEventArgs> JoinResponse;

        /// <summary>Connection to the server shutting down.</summary>
        event EventHandler<EventArgs> Disconnecting;

        /// <summary>Connection to the server was lost.</summary>
        event EventHandler<EventArgs> Disconnected;

        /// <summary>Current state of this session.</summary>
        ClientState ConnectionState { get; }

        /// <summary>Reference to the data struct with info about the local player.</summary>
        /// <remarks>
        ///     Shortcut for <c>session.GetPlayer(session.LocalPlayerNumber)</c>.
        /// </remarks>
        Player LocalPlayer { get; }

        /// <summary>Send a ping into the local network, looking for open games.</summary>
        void Search();

        /// <summary>Join a game on the given host.</summary>
        /// <param name="remote">the remote host that runs the session.</param>
        /// <param name="playerName">the name with which to register.</param>
        /// <param name="data">additional data to be associated with our player.</param>
        void Join(IPEndPoint remote, string playerName, object data);

        /// <summary>Join a local game.</summary>
        /// <param name="server">the local server to join.</param>
        /// <param name="playerName">the name with which to register.</param>
        /// <param name="data">additional data to be associated with our player.</param>
        void Join(IServerSession server, string playerName, object data);

        /// <summary>Leave the session.</summary>
        void Leave();
    }
}