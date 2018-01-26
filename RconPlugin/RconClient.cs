using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NLog.Fluent;

namespace RconPlugin
{
    public delegate void MessageReceivedDel(RconClient sender, RconPacket message);

    public delegate void ConnectionClosedDel(RconClient sender);

    public class RconClient : IDisposable
    {
        private static Logger Log = LogManager.GetCurrentClassLogger();

        public EndPoint RemoteEndPoint { get; }

        private Socket _socket;
        public bool IsAuthed { get; set; }
        private bool _isSending;

        public event ConnectionClosedDel ConnectionClosed;

        public RconClient(Socket remoteSocket)
        {
            _socket = remoteSocket;
            RemoteEndPoint = _socket.RemoteEndPoint;
        }

        public void StartListening()
        {
            BeginReceivePacket(_socket);
        }

        #region Receive

        public event MessageReceivedDel MessageReceived;

        private void BeginReceivePacket(Socket client)
        {
            try
            {
                var sizeBytes = new byte[4];
                client.BeginReceive(sizeBytes, 0, 4, SocketFlags.None, ContinueReceievePacket, new Tuple<Socket, byte[]>(client, sizeBytes));
            }
            catch (Exception e)
            {
                OnConnectionException(e);
            }
        }

        private void ContinueReceievePacket(IAsyncResult ar)
        {
            var receiveData = (Tuple<Socket, byte[]>)ar.AsyncState;
            var client = receiveData.Item1;
            var sizeBytes = receiveData.Item2;

            try
            {
                var size = BitConverter.ToInt32(sizeBytes, 0);
                Log.Debug($"Incoming message: {size} bytes");

                client.EndReceive(ar);

                var messageBytes = new byte[size + 4];
                Array.Copy(sizeBytes, messageBytes, sizeBytes.Length);
                client.BeginReceive(messageBytes, 4, size, SocketFlags.None, EndReceivePacket, new Tuple<Socket, byte[]>(client, messageBytes));
            }
            catch (Exception e)
            {
                OnConnectionException(e);
            }
        }

        private void EndReceivePacket(IAsyncResult ar)
        {
            var receiveData = (Tuple<Socket, byte[]>)ar.AsyncState;
            var client = receiveData.Item1;
            var rconMessageBytes = receiveData.Item2;

            try
            {
                client.EndReceive(ar);

                Log.Debug($"Message: {string.Join("", rconMessageBytes.Select(x => x.ToString("x2")))}");

                var rconMessage = RconPacket.FromBytes(rconMessageBytes, 0, rconMessageBytes.Length);
                Log.Debug($"Got packet: {rconMessage.Size} bytes\n  Type: {rconMessage.Type:G}\n  Id: {rconMessage.Id}\n  Body: {rconMessage.Body}");
                BeginReceivePacket(client);

                MessageReceived?.Invoke(this, rconMessage);
            }
            catch (Exception e)
            {
                OnConnectionException(e);
            }
        }

        private void OnConnectionException(Exception e)
        {
            Log.Error($"{RemoteEndPoint}: Connection failed.");
            ConnectionClosed?.Invoke(this);
        }

        #endregion

        #region Send

        private readonly ConcurrentQueue<RconPacket> _outboundQueue = new ConcurrentQueue<RconPacket>();

        public void SendPacket(RconPacket packet)
        {
            _outboundQueue.Enqueue(packet);
            //TODO: split if necessary
            //_outboundQueue.Enqueue(new RconPacket(packet.Id, PacketType.SERVERDATA_RESPONSE_VALUE, string.Empty));

            if (!_isSending)
                FlushQueueAsync().ConfigureAwait(false);
        }

        private async Task FlushQueueAsync()
        {
            _isSending = true;
            await Task.Run(() =>
                {
                    try
                    {

                        var mre = new ManualResetEvent(false);
                        while (_outboundQueue.Count > 0)
                        {
                            if (!_outboundQueue.TryDequeue(out RconPacket rconMessage))
                                continue;

                            var bytes = rconMessage.GetBytes();
                            Log.Debug($"Sending packet: {rconMessage.Size} bytes\n  Type: {rconMessage.Type:G}\n  Id: {rconMessage.Id}\n  Body: {rconMessage.Body}");
                            _socket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, EndSend, mre);
                            mre.WaitOne(TimeSpan.FromSeconds(5));
                            mre.Reset();
                        }

                        _isSending = false;
                    }
                    catch (Exception e)
                    {
                        OnConnectionException(e);
                    }
                });

            void EndSend(IAsyncResult ar)
            {
                try
                {
                    _socket.EndSend(ar);
                }
                catch (Exception e)
                {
                    OnConnectionException(e);
                }
                ((ManualResetEvent)ar.AsyncState).Set();
            }
        }

        #endregion

        /// <inheritdoc />
        public void Dispose()
        {
            _socket.Close();
        }
    }
}
