using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using NLog;

namespace RconPlugin
{
    public delegate string RconCommandHandlerDel(string command);

    public class RconServer : IDisposable
    {
        private Socket _listener;
        private List<RconClient> _clients = new List<RconClient>();
        private byte[] _pwHash;
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public RconCommandHandlerDel CommandHandler { get; set; }

        public RconServer(EndPoint endpoint)
        {
            _listener = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(endpoint);
            Log.Info($"RCON server bound to {endpoint}");
        }

        public void SetPassword(string password)
        {
            _pwHash = Md5Util.HashString(password);
        }

        public void SetPassword(byte[] hash)
        {
            _pwHash = hash;
        }

        public void Start()
        {
            _listener.Listen(100);
            _listener.BeginAccept(DoEndAccept, _listener);
        }

        private void DoEndAccept(IAsyncResult ar)
        {
            try
            {
                var listener = (Socket)ar.AsyncState;
                var remoteSocket = listener.EndAccept(ar);
                var client = new RconClient(remoteSocket);
                Log.Info($"Accepted client {remoteSocket.RemoteEndPoint}");
                _clients.Add(client);
                client.MessageReceived += Client_MessageReceived;
                client.ConnectionClosed += x => _clients.Remove(x);
                client.StartListening();

                listener.BeginAccept(DoEndAccept, listener);
            }
            catch
            {
                // Dies when the socket is disposed, but we don't care.
            }
        }

        private void Client_MessageReceived(RconClient sender, RconPacket packet)
        {
            switch (packet.Type)
            {
                case PacketType.SERVERDATA_AUTH:
                    Authenticate(sender, packet);
                    break;
                case PacketType.SERVERDATA_EXECCOMMAND:
                    if (sender.IsAuthed)
                        HandleCommand(sender, packet);
                    else
                        sender.SendPacket(new RconPacket(packet.Id, PacketType.SERVERDATA_RESPONSE_VALUE, "Not authenticated"));
                    break;
                default:
                    Log.Error($"Recieved an unsupported packet type {packet.Type}. Id: {packet.Id}");
                    sender.SendPacket(new RconPacket(packet.Id, PacketType.SERVERDATA_RESPONSE_VALUE, "Invalid packet type"));
                    break;
            }
        }

        private void HandleCommand(RconClient sender, RconPacket packet)
        {
            var message = CommandHandler?.Invoke(packet.Body) ?? "No command handler defined on host.";

            var response = new RconPacket
            {
                Id = packet.Id,
                Type = PacketType.SERVERDATA_RESPONSE_VALUE,
                Body = message
            };

            sender.SendPacket(response);
        }

        private void Authenticate(RconClient sender, RconPacket packet)
        {
            var hash = Md5Util.HashString(packet.Body);
            if (_pwHash != null && hash.SequenceEqual(_pwHash))
            {
                Log.Info($"{sender.RemoteEndPoint}: Authorized");
		// Necessary to send an empty RESPONSE_VALUE before sending the AUTHRESPONSE, otherwise most RCON clients don´t realize AUTH has been successfully.
		// See https://developer.valvesoftware.com/wiki/Source_RCON_Protocol#SERVERDATA_AUTH_RESPONSE
		// >>  When the server receives an auth request, it will respond with an empty SERVERDATA_RESPONSE_VALUE, followed immediately by a SERVERDATA_AUTH_RESPONSE indicating whether authentication succeeded or failed.
                sender.SendPacket(new RconPacket(packet.Id, PacketType.SERVERDATA_RESPONSE_VALUE, string.Empty));
                sender.SendPacket(new RconPacket(packet.Id, PacketType.SERVERDATA_AUTHRESPONSE, string.Empty));
                sender.IsAuthed = true;
            }
            else
            {
                Log.Warn($"{sender.RemoteEndPoint}: Incorrect password attempt");
                sender.SendPacket(new RconPacket(packet.Id, PacketType.SERVERDATA_RESPONSE_VALUE, string.Empty)); // same here by definition although most clients realized the wrong AUTH info
                sender.SendPacket(new RconPacket(-1, PacketType.SERVERDATA_AUTHRESPONSE, string.Empty));
                sender.IsAuthed = false;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var client in _clients)
            {
                client.Dispose();
            }

            _listener.Close();
        }
    }
}
