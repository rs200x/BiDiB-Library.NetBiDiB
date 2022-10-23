using Microsoft.Extensions.Logging;
using org.bidib.netbidibc.netbidib.Message;
using org.bidib.netbidibc.netbidib.Services;
using org.bidib.netbidibc.core.Controllers.Interfaces;
using org.bidib.netbidibc.core.Enumerations;
using org.bidib.netbidibc.core.Models;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Threading.Tasks;
using org.bidib.netbidibc.core.Controllers;
using org.bidib.netbidibc.core.Utils;
using org.bidib.netbidibc.core.Message;
using org.bidib.netbidibc.core.Models.Messages.Output;
using org.bidib.netbidibc.core.Properties;

namespace org.bidib.netbidibc.netbidib.Controllers
{
    internal class NetBiDiBServerController : ConnectionController<INetConfig>, INetBiDiBServerController
    {
        private readonly ILogger<NetBiDiBController> logger;
        private readonly ILogger rawLogger;
        private readonly INetBiDiBMessageProcessor messageProcessor;
        private readonly INetBiDiBParticipantsService participantsService;
        private readonly byte[] instanceId = { 0, 0, 0x0D, 0xFB, 0x00, 10, 20 };
        private byte pairingTimeout;
        private bool listening;
        private DateTime timeoutTime;
        private readonly object lockObject = new();
        private TcpListener tcpListener;
        private NetworkStream networkStream;
        private const int BufferSize = 8 * 1024;

        private bool IsProcessorConnected => messageProcessor.CurrentState is NetBiDiBConnectionState.ConnectedControlling or NetBiDiBConnectionState.ConnectedUncontrolled;

        private DateTime TimeoutTime
        {
            get
            {
                lock (lockObject)
                {
                    return timeoutTime;
                }
            }

            set
            {
                lock (lockObject)
                {
                    timeoutTime = value;
                }
            }
        }

        public NetBiDiBServerController(INetBiDiBMessageProcessor messageProcessor, INetBiDiBParticipantsService participantsService, ILoggerFactory loggerFactory)
        {
            this.messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            this.participantsService = participantsService;
            logger = loggerFactory.CreateLogger<NetBiDiBController>();
            rawLogger = loggerFactory.CreateLogger("Raw");
            messageProcessor.SendMessage = SendMessage;
            messageProcessor.ConnectionStateChanged += HandleMessageProcessorConnectionStateChanged;
        }

        public override bool MessageSecurityEnabled => false;

        public override ConnectionStateInfo ConnectionState => GetConnectionStateInfo();

        public override string ConnectionName => $"netBiDiB Server {string.Join("", instanceId.Select(x => $"{x:X2}"))}";

        public override void Initialize(INetConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                var endPoint = new IPEndPoint(IPAddress.Any, config.NetworkPortNumber);
                tcpListener = new(endPoint);
            }
            catch (Exception e) when (e is ArgumentOutOfRangeException || e is ArgumentNullException || e is ArgumentException)
            {
                logger.LogError(e, $"Problem creating tcp listener on port {config.NetworkPortNumber}");
                throw;
            }

        }

        public override Task<ConnectionStateInfo> OpenConnectionAsync()
        {
            listening = true;
            Task.Factory.StartNew(StartListening);
            return Task.FromResult(new ConnectionStateInfo(InterfaceConnectionState.FullyConnected, InterfaceConnectionType.NetBiDiBServer));
        }

        public override void Close()
        {
            listening = false;
            tcpListener?.Stop();
            networkStream?.Close();
            networkStream?.Dispose();
        }

        public void RequestControl()
        {
            throw new NotImplementedException();
        }

        public void RejectControl()
        {
            throw new NotImplementedException();
        }

        private async Task StartListening()
        {
            try
            {
                logger.LogInformation($"Start listening for clients on {tcpListener.Server.LocalEndPoint}");
                tcpListener.Start();

                while (listening)
                {
                    TcpClient client = await tcpListener.AcceptTcpClientAsync();
                    logger.LogInformation($"client connected {client.Connected}");
                    Task.Run(() => StartReceive(client.Client));
                    Task.Run(() => ObserveConnection(client.Client));
                }


            }
            catch (Exception e)
            {
                logger.LogError(e, "Error awaiting tcp connection");
            }

            logger.LogInformation("Stopp listening for clients");
        }

        private async Task ObserveConnection(Socket senderSocket)
        {
            while (senderSocket != null && senderSocket.Connected)
            {
                await Task.Delay(200);
            }

            logger.LogInformation("Socket has been closed");
        }

        private void StartReceive(Socket senderSocket)
        {
            var clientData = new byte[BufferSize];

            if (senderSocket.Poll(0, SelectMode.SelectRead) && senderSocket.Available == 0)
            {
                logger.LogWarning($"Socket '{ConnectionName}' seems to be closed");
                Close();

                OnConnectionClosed();
                return;
            }

            try
            {
                senderSocket.BeginReceive(clientData, 0, clientData.Length, SocketFlags.None, HandleDataReceived, (senderSocket, clientData));
            }
            catch (SocketException se)
            {
                logger.LogError(se, Resources.Error_SocketData);
            }
        }

        private void HandleDataReceived(IAsyncResult result)
        {
            try
            {
                // this is what had been passed into BeginReceive as the second parameter:

                if (!(result.AsyncState is (Socket socket, byte[] clientData)))
                {
                    logger.LogError(Resources.Error_NoSocket);
                    return;
                }

                // get the actual message and fill out the source:
                int messageSize = socket.EndReceive(result);
                // do what you'd like with `message` here:
                logger.LogDebug($"Socket data received {BitConverter.ToString(clientData, 0, messageSize)}");

                if (messageSize > 0)
                {
                    ProcessMessage(clientData, messageSize);
                }

                // schedule the next receive operation once reading is done:
                StartReceive(socket);
            }
            catch (ObjectDisposedException)
            {
                logger.LogDebug(Resources.Error_SocketDisposed);
            }

            catch (SocketException se)
            {
                logger.LogError(se, Resources.Error_SocketData);
            }
        }


        private void SendMessage(BiDiBOutputMessage outputMessage)
        {
            byte[] message = BiDiBMessageGenerator.GenerateMessage(outputMessage);

            SendMessage(message, message.Length);
            logger.LogDebug(outputMessage.ToString());
            //ServiceLogger.Debug($"{outputMessage} {message.GetDataString()}");
        }

        public override bool SendMessage(byte[] messageBytes, int byteCount)
        {
            if (!networkStream.Socket.Connected)
            {
                logger.LogError($"Cannot send message - Socket to '{ConnectionName}' is NOT connected or open!");
                return false;
            }

            try
            {
                rawLogger.LogInformation($">>> {messageBytes.GetDataString()}");
                logger.LogInformation($">>> {messageBytes.GetDataString()}");
                networkStream.Write(messageBytes, 0, byteCount);
            }
            catch (Exception e) when (e is SocketException)
            {
                logger.LogError(e, $"Sending data to '{ConnectionName}' failed");
            }

            return true;
        }



        private void HandleMessageProcessorConnectionStateChanged(object sender, EventArgs e)
        {
            OnConnectionStateChanged();

            if (messageProcessor.CurrentState == NetBiDiBConnectionState.RequestPairing)
            {
                TimeoutTime = DateTime.Now.AddSeconds(pairingTimeout);
            }

            if (messageProcessor.CurrentState == NetBiDiBConnectionState.PairingRejected)
            {
                TimeoutTime = DateTime.Now.AddSeconds(0);
            }
        }

        private ConnectionStateInfo GetConnectionStateInfo()
        {
            return new NetBiDiBConnectionStateInfo(messageProcessor.GetInterfaceConnectionState(), messageProcessor.CurrentState, messageProcessor.RemoteState,
                messageProcessor.CurrentParticipant.Id, messageProcessor.CurrentParticipant.ProductName, string.Empty, null, string.Empty, pairingTimeout);
        }

        protected virtual byte[] DecodeMessage(byte[] message, int byteCount)
        {
            byte[] messageData = new byte[byteCount];
            Array.Copy(message, 0, messageData, 0, byteCount);
            return messageData;
        }

        public virtual void ProcessMessage(byte[] message, int messageSize)
        {
            byte[] messageData = DecodeMessage(message, messageSize);
            rawLogger.LogInformation($"<<< {messageData.GetDataString()}");
            logger.LogInformation($"<<< {messageData.GetDataString()}");
            ProcessReceivedData?.Invoke(messageData);
        }
    }
}
