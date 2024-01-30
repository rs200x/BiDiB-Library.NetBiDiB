using System.Globalization;
using org.bidib.Net.Core;
using org.bidib.Net.Core.Enumerations;
using org.bidib.Net.Core.Message;
using org.bidib.Net.Core.Models.Messages.Input;
using org.bidib.Net.Core.Properties;
using org.bidib.Net.Core.Services.Interfaces;
using org.bidib.Net.Core.Utils;

namespace org.bidib.Net.NetBiDiB.Message;

public class NetBiDiBMessageReceiver : IMessageReceiver
{
    private readonly IBiDiBNodesFactory nodesFactory;

    public NetBiDiBMessageReceiver(IBiDiBNodesFactory nodesFactory)
    {
        this.nodesFactory = nodesFactory;
    }

    public void ProcessMessage(BiDiBInputMessage message)
    {
        if (message == null) { return; }

        switch (message.MessageType)
        {
            case BiDiBMessage.MSG_LOCAL_LINK:
                HandleLocalLinkMessage(message as LocalLinkMessage);
                break;
            case BiDiBMessage.MSG_LOCAL_LOGON:
            {
                SetStateOnRoot(NodeState.Ok, string.Empty);
                break;
            }
            case BiDiBMessage.MSG_LOCAL_LOGOFF:
            {
                SetStateOnRoot(NodeState.Available, string.Empty);
                break;
            }
            default: return;
        }
    }

    private void HandleLocalLinkMessage(LocalLinkMessage message)
    {
        if (message == null) { return; }

        switch (message.LinkType)
        {
            case LocalLinkType.NODE_UNAVAILABLE:
                SetStateOnRoot(NodeState.Unavailable, message.Data.GetStringValue());
                return;
            case LocalLinkType.NODE_AVAILABLE:
                SetStateOnRoot(NodeState.Available, string.Empty);
                break;
            default: return;
        }
    }

    private void SetStateOnRoot(NodeState state, string data)
    {
        var rootNode = nodesFactory.GetNode(new byte[] { 0 });

        if (rootNode == null) { return; }

        rootNode.State = state;

        switch (state)
        {
            case NodeState.Available:
            {
                rootNode.StateInfo = Resources.NodeAvailableForControl;
                break;
            }
            case NodeState.Unavailable:
            {
                rootNode.StateInfo = string.Format(CultureInfo.CurrentCulture, Resources.NodeControlledByOther, data);
                break;
            }
            default:
            {
                rootNode.StateInfo = data;
                break;
            }
        }
    }
}