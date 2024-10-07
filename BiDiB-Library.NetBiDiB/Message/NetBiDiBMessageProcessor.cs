using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using org.bidib.Net.Core.Enumerations;
using org.bidib.Net.Core.Models.Messages.Input;
using org.bidib.Net.Core.Models.Messages.Output;
using org.bidib.Net.Core.Utils;
using org.bidib.Net.NetBiDiB.Models;
using LocalLinkOutMessage = org.bidib.Net.Core.Models.Messages.Output.LocalLinkMessage;
using LocalLinkInMessage = org.bidib.Net.Core.Models.Messages.Input.LocalLinkMessage;
using LocalLogonInMessage = org.bidib.Net.Core.Models.Messages.Input.LocalLogonMessage;
using LocalLogonOutMessage = org.bidib.Net.Core.Models.Messages.Output.LocalLogonMessage;
using LocalLogonAckInMessage = org.bidib.Net.Core.Models.Messages.Input.LocalLogonAckMessage;
using LocalLogonAckOutMessage = org.bidib.Net.Core.Models.Messages.Output.LocalLogonAckMessage;
using ProtocolSignatureOutMessage = org.bidib.Net.Core.Models.Messages.Output.ProtocolSignatureMessage;
using ProtocolSignatureInMessage = org.bidib.Net.Core.Models.Messages.Input.ProtocolSignatureMessage;

namespace org.bidib.Net.NetBiDiB.Message;

[Export(typeof(INetBiDiBMessageProcessor))]
[PartCreationPolicy(CreationPolicy.Shared)]
[method: ImportingConstructor]
public class NetBiDiBMessageProcessor(ILoggerFactory loggerFactory) : INetBiDiBMessageProcessor
{
    private readonly ILogger<NetBiDiBMessageProcessor> logger = loggerFactory.CreateLogger<NetBiDiBMessageProcessor>();
    private IEnumerable<int> knownParticipants = [];
    private byte pairingTimeout;
    private NetBiDiBConnectionState currentState;
    private NetBiDiBConnectionState remoteState;

    private bool IsKnownParticipant => knownParticipants.Contains(CurrentParticipant?.Id.GetArrayValue() ?? 0);

    public NetBiDiBParticipant CurrentParticipant { get; private set; } = new();

    public Action<BiDiBOutputMessage> SendMessage { get; set; }

    public NetBiDiBConnectionState CurrentState
    {
        get => currentState;
        private set
        {
            if (currentState == value) { return; }

            currentState = value;
            OnConnectionStateChanged();
        }
    }

    public NetBiDiBConnectionState RemoteState
    {
        get => remoteState;
        private set
        {
            if (remoteState == value) { return; }

            remoteState = value;
            OnConnectionStateChanged();
        }
    }

    public string Emitter { get; set; } = string.Empty;

    public string Username { get; set; } = Environment.UserDomainName;

    public byte[] UniqueId { get; set; } = new byte[7];


    public byte[] Address { get; private set; }

    public void Start(IEnumerable<byte[]> trustedParticipants, byte timeout)
    {
        knownParticipants = trustedParticipants.Select(x => x.GetArrayValue());
        pairingTimeout = timeout;
        CurrentState = NetBiDiBConnectionState.SendSignature;
        SendMessage(new ProtocolSignatureOutMessage(Emitter));
    }

    public void ProcessMessage(BiDiBInputMessage message)
    {
        if (message == null) { return; }
        logger.LogDebug("{Message} {DataString}", message, message.Message.GetDataString());
        switch (message)
        {
            case ProtocolSignatureInMessage protocolSignature:
            {
                ProcessProtocolSignatureMessage(protocolSignature);
                break;
            }
            case LocalLinkInMessage linkMessage:
            {
                ProcessLinkMessage(linkMessage);
                break;
            }
            case LocalLogonInMessage logonMessage:
            {
                SendMessage(new LocalLogonAckOutMessage(logonMessage.Uid));
                CurrentState = NetBiDiBConnectionState.ConnectedControlling;
                break;
            }
            case LocalLogoffMessage:
            {
                CurrentState = NetBiDiBConnectionState.ConnectedUncontrolled;
                break;
            }
            case LocalLogonAckInMessage logonAckMessage:
            {
                Address = logonAckMessage.NodeAddress;
                CurrentState = NetBiDiBConnectionState.ConnectedControlling;
                break;
            }

            default:
            {
                logger.LogWarning("Received unknown message type '{MessageType}' at this point", message.MessageType);
                break;
            }
        }
    }

    public void RequestControl()
    {
        SendPairingStatus(LocalLinkType.STATUS_PAIRED);
    }

    public void RejectControl()
    {
        CurrentState = NetBiDiBConnectionState.WaitForStatus;
        SendMessage(new LocalLogonRejectedMessage(UniqueId));
        CurrentState = NetBiDiBConnectionState.ConnectedUncontrolled; // temporary, should be answered by node
    }

    public void Reset()
    {
        CurrentParticipant = new NetBiDiBParticipant();
        knownParticipants = Enumerable.Empty<int>();
        CurrentState = NetBiDiBConnectionState.Disconnected;
        RemoteState = NetBiDiBConnectionState.Disconnected;
        Address = null;
    }

    public event EventHandler<EventArgs> ConnectionStateChanged;

    public InterfaceConnectionState InterfaceConnectionState
    {
        get
        {
            switch (CurrentState)
            {
                case NetBiDiBConnectionState.Disconnected:
                case NetBiDiBConnectionState.SendSignature:
                case NetBiDiBConnectionState.WaitForId:
                case NetBiDiBConnectionState.WaitForStatus:
                case NetBiDiBConnectionState.Paired:
                case NetBiDiBConnectionState.PairingRejected:
                {
                    return InterfaceConnectionState.Disconnected;
                }
                case NetBiDiBConnectionState.RequestPairing:
                case NetBiDiBConnectionState.Unpaired:
                {
                    return InterfaceConnectionState.Unpaired;
                }
                case NetBiDiBConnectionState.ConnectedControlling:
                {
                    return InterfaceConnectionState.FullyConnected;
                }
                case NetBiDiBConnectionState.ConnectedUncontrolled:
                {
                    return InterfaceConnectionState.PartiallyConnected;
                }
                default:
                    return InterfaceConnectionState.Disconnected;
            }
        }
    }

    private void OnConnectionStateChanged()
    {
        ConnectionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ProcessProtocolSignatureMessage(ProtocolSignatureInMessage protocolSignature)
    {
        if (protocolSignature.Emitter.StartsWith("BiDiB", StringComparison.CurrentCulture))
        {
            CurrentParticipant.RequestorName = protocolSignature.Emitter;
            CurrentState = NetBiDiBConnectionState.WaitForId;
            SendMessage(new LocalLinkOutMessage(LocalLinkType.DESCRIPTOR_UID, UniqueId));
            SendMessage(new LocalLinkOutMessage(LocalLinkType.DESCRIPTOR_PROD_STRING, Emitter.GetBytes()));
            SendMessage(new LocalLinkOutMessage(LocalLinkType.DESCRIPTOR_USER_STRING, Username.GetBytes()));
            SendMessage(new LocalLinkOutMessage(LocalLinkType.DESCRIPTOR_P_VERSION, new byte[] { 0, 8 }));
            SendMessage(new LocalLinkOutMessage(LocalLinkType.DESCRIPTOR_ROLE, new byte[] { 1 }));
        }
        else
        {
            CurrentState = NetBiDiBConnectionState.Disconnected;
        }
    }

    private void ProcessLinkMessage(LocalLinkInMessage linkMessage)
    {
        switch (linkMessage.LinkType)
        {
            case LocalLinkType.DESCRIPTOR_UID:
            {
                CurrentParticipant.Id = linkMessage.Data;

                if (IsKnownParticipant)
                {
                    SendPairingStatus(LocalLinkType.STATUS_PAIRED);
                    CurrentState = NetBiDiBConnectionState.Paired;
                }
                else
                {
                    SendPairingStatus(LocalLinkType.STATUS_UNPAIRED);
                    SendPairingStatus(LocalLinkType.PAIRING_REQUEST);
                }

                break;
            }
            case LocalLinkType.DESCRIPTOR_PROD_STRING:
            {
                CurrentParticipant.ProductName = linkMessage.Data.GetStringValue();
                break;
            }
            case LocalLinkType.DESCRIPTOR_P_VERSION:
            {
                CurrentParticipant.ProtocolVersion = $"{string.Join(".", linkMessage.Data.Reverse().Select(x => x.ToString(CultureInfo.CurrentCulture)))}";
                break;
            }
            case LocalLinkType.DESCRIPTOR_USER_STRING:
            {
                CurrentParticipant.UserName = linkMessage.Data.GetStringValue().Trim();
                break;
            }
            case LocalLinkType.PAIRING_REQUEST:
            {
                RemoteState = NetBiDiBConnectionState.RequestPairing;
                RequestControl();
                break;
            }
            case LocalLinkType.STATUS_PAIRED:
            {
                HandleRemotePaired();
                break;
            }
            case LocalLinkType.STATUS_UNPAIRED:
            {
                HandleRemoteUnpaired();
                break;
            }
            default:
            {
                logger.LogWarning("Received unknown local link type '{LinkType}' at this point", linkMessage.LinkType);
                break;
            }
        }
    }

    private void HandleRemotePaired()
    {
        if (CurrentState == NetBiDiBConnectionState.WaitForStatus)
        {
            CurrentState = NetBiDiBConnectionState.Paired;
        }

        if (RemoteState != NetBiDiBConnectionState.Disconnected && CurrentState == NetBiDiBConnectionState.RequestPairing)
        {
            SendPairingStatus(LocalLinkType.STATUS_PAIRED);
        }

        RemoteState = NetBiDiBConnectionState.Paired;

        if (RemoteState == NetBiDiBConnectionState.Paired && CurrentState == NetBiDiBConnectionState.Paired)
        {
            SendMessage(new LocalLogonOutMessage(UniqueId));
        }
    }

    private void HandleRemoteUnpaired()
    {
        if (RemoteState != NetBiDiBConnectionState.Disconnected && CurrentState == NetBiDiBConnectionState.RequestPairing || RemoteState == NetBiDiBConnectionState.RequestPairing)
        {
            CurrentState = NetBiDiBConnectionState.PairingRejected;
        }

        if (RemoteState is NetBiDiBConnectionState.Disconnected or NetBiDiBConnectionState.Unpaired &&
            CurrentState == NetBiDiBConnectionState.Paired)
        {
            SendPairingStatus(LocalLinkType.PAIRING_REQUEST);
        }

        RemoteState = NetBiDiBConnectionState.Unpaired;

    }

    private void SendPairingStatus(LocalLinkType status)
    {
        var parameters = new List<byte>(UniqueId);
        parameters.AddRange(CurrentParticipant.Id);

        if (status == LocalLinkType.PAIRING_REQUEST)
        {
            CurrentState = NetBiDiBConnectionState.RequestPairing;
            parameters.Add(pairingTimeout);
        }

        if (status == LocalLinkType.STATUS_PAIRED)
        {
            CurrentState = NetBiDiBConnectionState.Paired;
        }

        if (status == LocalLinkType.STATUS_UNPAIRED)
        {
            CurrentState = NetBiDiBConnectionState.Unpaired;
        }

        SendMessage(new LocalLinkOutMessage(status, parameters));
    }
}