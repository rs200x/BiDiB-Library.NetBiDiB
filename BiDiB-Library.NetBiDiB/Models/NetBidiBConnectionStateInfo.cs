using org.bidib.Net.Core.Enumerations;
using org.bidib.Net.Core.Models;
using org.bidib.Net.Core.Models.BiDiB.Extensions;
using org.bidib.Net.Core.Utils;

namespace org.bidib.Net.NetBiDiB.Models
{
    public class NetBiDiBConnectionStateInfo : ConnectionStateInfo
    {
        public NetBiDiBConnectionStateInfo(
            InterfaceConnectionState interfaceState, 
            NetBiDiBConnectionState localState, 
            NetBiDiBConnectionState remoteState, 
            byte[] remoteId, string remoteName, string remoteAddress, byte[] localAddress,  string error, int timeout) 
            : base(interfaceState, InterfaceConnectionType.NetBiDiB, error)
        {
           
            RemoteId = remoteId;
            RemoteName = remoteName;
            Timeout = timeout;
            LocalState = localState;
            LocalAddress = localAddress;
            RemoteState = remoteState;
            RemoteAddress = remoteAddress;
        }


        public byte[] RemoteId { get; }

        public string RemoteIdString => RemoteId?.GetDataString();

        public string RemoteName { get; }

        public string RemoteAddress { get; }

        public int Timeout { get; }

        public byte[] LocalAddress { get; }

        public string LocalAddressString => NodeExtensions.GetFullAddressString(LocalAddress);

        public NetBiDiBConnectionState LocalState { get; }

        public NetBiDiBConnectionState RemoteState { get; }

        public bool HasGuestFunctions => RemoteId is { Length: > 0 } && (RemoteId[0] & 32) == 32;
    }
}