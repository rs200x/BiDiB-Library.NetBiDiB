using org.bidib.netbidibc.core.Enumerations;

namespace org.bidib.netbidibc.netbidib.Models;

public class NetBidibConfig : INetBiDiBConfig
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NetworkHostAddress { get; set; }
    public int NetworkPortNumber { get; set; }
    public string ApplicationName { get; set; }
    public string NetBiDiBClientId { get; set; }
    public byte NetBiDiBPairingTimeout { get; set; }
    public InterfaceConnectionType ConnectionType { get; set; } = InterfaceConnectionType.NetBiDiB;
    public string PairingStoreDirectory { get; set; }
}
