using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using org.bidib.netbidibc.netbidib.Message;
using org.bidib.netbidibc.netbidib.Services;
using org.bidib.netbidibc.core.Controllers;
using org.bidib.netbidibc.core.Controllers.Interfaces;
using org.bidib.netbidibc.core.Enumerations;
using org.bidib.netbidibc.core.Message;
using org.bidib.netbidibc.core.Models;
using org.bidib.netbidibc.core.Models.Messages;
using org.bidib.netbidibc.core.Models.Messages.Input;
using org.bidib.netbidibc.core.Models.Messages.Output;
using org.bidib.netbidibc.core.Properties;
using org.bidib.netbidibc.core.Utils;

namespace org.bidib.netbidibc.netbidib.Controllers
{
    public class NetBiDiBController : SocketController, INetBiDiBController
    {
        private readonly ILogger<NetBiDiBController> logger;
        private readonly ILogger serviceLogger;
        private readonly INetBiDiBMessageProcessor messageProcessor;
        private readonly INetBiDiBParticipantsService participantsService;
        private readonly byte[] instanceId = { 0, 0x20, 0x0D, 0xFB, 0x00, 10, 20 };
        private byte pairingTimeout;
        private DateTime timeoutTime;
        private readonly object lockObject = new ();

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

        public NetBiDiBController(INetBiDiBMessageProcessor messageProcessor, INetBiDiBParticipantsService participantsService, ILoggerFactory loggerFactory):base(loggerFactory)
        {
            this.messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            this.participantsService = participantsService;
            logger = loggerFactory.CreateLogger<NetBiDiBController>();
            serviceLogger = loggerFactory.CreateLogger("MS");
            messageProcessor.SendMessage = SendMessage;
            messageProcessor.ConnectionStateChanged += HandleMessageProcessorConnectionStateChanged;
        }

        public override bool MessageSecurityEnabled => false;

        public override ConnectionStateInfo ConnectionState => GetConnectionStateInfo(string.Empty);

        public override string ConnectionName => $"netBiDiB {string.Join("", instanceId.Select(x => $"{x:X2}"))} -> {base.ConnectionName}";

        public override void Initialize(INetConfig config)
        {
            if (config == null) { throw new ArgumentNullException(nameof(config)); }

            if (!IsConnected)
            {
                base.Initialize(config);
            }

            pairingTimeout = config.NetBiDiBPairingTimeout;
            UpdateInstanceId(config);

            messageProcessor.Emitter = config.ApplicationName;
            messageProcessor.UniqueId = instanceId;
        }

        private void UpdateInstanceId(INetConfig config)
        {
            if (string.IsNullOrEmpty(config.NetBiDiBClientId)) { return; }

            try
            {
                byte[] clientId = Enumerable.Range(0, config.NetBiDiBClientId.Length)
                    .Where(x => x % 2 == 0)
                    .Select(x => Convert.ToByte(config.NetBiDiBClientId.Substring(x, 2), 16))
                    .ToArray();

                Array.Copy(clientId, 0, instanceId, instanceId.Length - clientId.Length, clientId.Length);
            }
            catch (Exception e) when (e is ArgumentNullException or ArgumentOutOfRangeException)
            {
                logger.LogError(e, $"Could not parse '{config.NetBiDiBClientId}' for custom client id");
            }
        }

        public override async Task<ConnectionStateInfo> OpenConnectionAsync()
        {
            if (IsProcessorConnected)
            {
                // pairing was processed in the background in the mean time
                return ProcessCurrentState(false);
            }

            if (base.ConnectionState.InterfaceState == InterfaceConnectionState.Disconnected)
            {
                var state = await base.OpenConnectionAsync().ConfigureAwait(false);
                if (!state.IsConnected) { return state; }
            }

            TimeoutTime = DateTime.Now.AddSeconds(6);
            messageProcessor.Start(participantsService.TrustedParticipants.Select(p => p.Id), pairingTimeout);

            while (!IsProcessorConnected && DateTime.Now < TimeoutTime)
            {
                await Task.Delay(300).ConfigureAwait(false);
            }

            return ProcessCurrentState(DateTime.Now >= TimeoutTime);
        }

        private NetBiDiBConnectionStateInfo ProcessCurrentState(bool isTimeout)
        {
            string error = GetError(isTimeout);

            if (IsProcessorConnected)
            {
                participantsService.AddOrUpdate(messageProcessor.CurrentParticipant);
            }

            if (!IsProcessorConnected)
            {
                messageProcessor.Reset();
            }

            return GetConnectionStateInfo(error);
        }

        private string GetError(bool isTimeout)
        {
            string error = null;
            if (messageProcessor.CurrentState == NetBiDiBConnectionState.PairingRejected)
            {
                error = Resources.PairingRejectedByRemote;
            }

            if (isTimeout && messageProcessor.CurrentState == NetBiDiBConnectionState.RequestPairing || messageProcessor.RemoteState == NetBiDiBConnectionState.RequestPairing)
            {
                error = Resources.PairingProcessAborted;
            }

            if (!IsProcessorConnected && isTimeout && string.IsNullOrEmpty(error))
            {
                error = Resources.ConnectionProcessTimedOut;
                Close();
            }

            return error;
        }

        private void SendMessage(BiDiBOutputMessage outputMessage)
        {
            byte[] message = BiDiBMessageGenerator.GenerateMessage(outputMessage);

            
            SendMessage(message, message.Length);
            logger.LogDebug(outputMessage.ToString());
            serviceLogger.LogDebug($"{outputMessage} {message.GetDataString()}");
        }

        public override void ProcessMessage(byte[] message, int messageSize)
        {
            if (message == null) { return; }

            var size = messageSize > message.Length ? message.Length : messageSize;
            byte[] messageBytes = new byte[size];
            Array.Copy(message, 0, messageBytes, 0, size);

            if (!IsProcessorConnected)
            {
                foreach (byte[] subMessage in messageBytes.SplitByFirst())
                {
                    BiDiBInputMessage inputMessage = MessageFactory.CreateInputMessage(subMessage);
                    serviceLogger.LogDebug($"{inputMessage} {subMessage.GetDataString()}");
                    messageProcessor.ProcessMessage(inputMessage);
                }

                return;
            }

            base.ProcessMessage(messageBytes, size);
        }

        public override void RejectControl()
        {
            messageProcessor.RejectControl();
        }

        public override void RequestControl()
        {
            messageProcessor.RequestControl();
        }

        public override void Close()
        {
            base.Close();
            TimeoutTime = DateTime.Now.AddSeconds(0);

            if (IsProcessorConnected)
            {
                messageProcessor.Reset();
            }
        }

        private void HandleMessageProcessorConnectionStateChanged(object sender, EventArgs e)
        {
            OnConnectionStateChanged();

            if (messageProcessor.CurrentState == NetBiDiBConnectionState.RequestPairing)
            {
                logger.LogDebug($"State changed to request pairing, extending timeout by {pairingTimeout}");
                TimeoutTime = DateTime.Now.AddSeconds(pairingTimeout);
            }

            if (messageProcessor.CurrentState == NetBiDiBConnectionState.PairingRejected)
            {
                logger.LogDebug($"Pairing was rejected, stopping timeout");
                TimeoutTime = DateTime.Now.AddSeconds(0);
            }
        }

        private NetBiDiBConnectionStateInfo GetConnectionStateInfo(string error)
        {
            return new NetBiDiBConnectionStateInfo(
                messageProcessor.GetInterfaceConnectionState(),                 
                messageProcessor.CurrentState,
                messageProcessor.RemoteState,
                messageProcessor.CurrentParticipant.Id, 
                messageProcessor.CurrentParticipant.ProductName, 
                ConnectionName,
                messageProcessor.Address,
                error, 
                pairingTimeout);
        }
    }
}
