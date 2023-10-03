using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using org.bidib.Net.Core;
using org.bidib.Net.Core.Controllers;
using org.bidib.Net.Core.Enumerations;
using org.bidib.Net.Core.Message;
using org.bidib.Net.Core.Models;
using org.bidib.Net.Core.Models.Messages.Output;
using org.bidib.Net.Core.Properties;
using org.bidib.Net.Core.Utils;
using org.bidib.Net.NetBiDiB.Message;
using org.bidib.Net.NetBiDiB.Models;
using org.bidib.Net.NetBiDiB.Services;

namespace org.bidib.Net.NetBiDiB.Controllers
{
    public class NetBiDiBController : SocketController<INetBiDiBConfig>, INetBiDiBController
    {
        private readonly ILogger<NetBiDiBController> logger;
        private readonly ILogger serviceLogger;
        private readonly INetBiDiBMessageProcessor messageProcessor;
        private readonly INetBiDiBParticipantsService participantsService;
        private readonly IMessageFactory messageFactory;
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

        public NetBiDiBController(
            INetBiDiBMessageProcessor messageProcessor, 
            INetBiDiBParticipantsService participantsService, 
            IMessageFactory messageFactory,  
            ILoggerFactory loggerFactory):base(loggerFactory)
        {
            this.messageProcessor = messageProcessor ?? throw new ArgumentNullException(nameof(messageProcessor));
            this.participantsService = participantsService;
            this.messageFactory = messageFactory;
            logger = loggerFactory.CreateLogger<NetBiDiBController>();
            serviceLogger = loggerFactory.CreateLogger(BiDiBConstants.LoggerContextMessage);
            messageProcessor.SendMessage = SendMessage;
            messageProcessor.ConnectionStateChanged += HandleMessageProcessorConnectionStateChanged;
        }

        public override bool MessageSecurityEnabled => false;

        public override ConnectionStateInfo ConnectionState => GetConnectionStateInfo(string.Empty);

        public override string ConnectionName => $"netBiDiB {string.Join("", instanceId.Select(x => $"{x:X2}"))} -> {base.ConnectionName}";

        public override void Initialize(INetBiDiBConfig config)
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

        private void UpdateInstanceId(INetBiDiBConfig config)
        {
            if (string.IsNullOrEmpty(config.NetBiDiBClientId)) { return; }

            try
            {
                var clientId = Enumerable.Range(0, config.NetBiDiBClientId.Length)
                    .Where(x => x % 2 == 0)
                    .Select(x => Convert.ToByte(config.NetBiDiBClientId.Substring(x, 2), 16))
                    .ToArray();

                Array.Copy(clientId, 0, instanceId, instanceId.Length - clientId.Length, clientId.Length);
            }
            catch (Exception e) when (e is ArgumentNullException or ArgumentOutOfRangeException)
            {
                logger.LogError(e, "Could not parse '{ClientId}' for custom client id", config.NetBiDiBClientId);
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
            var error = GetError(isTimeout);

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
            var message = BiDiBMessageGenerator.GenerateMessage(outputMessage);

            SendMessage(message, message.Length);
            logger.LogDebug("{Message}", outputMessage);
            serviceLogger.LogDebug("{OutputMessage} {DataString}", outputMessage, message.GetDataString());
        }

        public override void ProcessMessage(byte[] message, int messageSize)
        {
            if (message == null) { return; }

            var size = messageSize > message.Length ? message.Length : messageSize;
            var messageBytes = new byte[size];
            Array.Copy(message, 0, messageBytes, 0, size);

            if (!IsProcessorConnected)
            {
                foreach (var subMessage in messageBytes.SplitByFirst())
                {
                    var inputMessage = messageFactory.CreateInputMessage(subMessage);
                    serviceLogger.LogDebug("{InputMessage} {SubMessage}", inputMessage, subMessage.GetDataString());
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
                logger.LogDebug("State changed to request pairing, extending timeout by {PairingTimeout}", pairingTimeout);
                TimeoutTime = DateTime.Now.AddSeconds(pairingTimeout);
            }

            if (messageProcessor.CurrentState == NetBiDiBConnectionState.PairingRejected)
            {
                logger.LogDebug("Pairing was rejected, stopping timeout");
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
