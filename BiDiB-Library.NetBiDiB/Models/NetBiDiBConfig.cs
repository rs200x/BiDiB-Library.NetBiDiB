﻿using org.bidib.Net.Core.Enumerations;

namespace org.bidib.Net.NetBiDiB.Models;

public class NetBidibConfig : INetBiDiBConfig
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string NetworkHostAddress { get; set; }
    public int NetworkPortNumber { get; set; }
    public string ApplicationName { get; set; }
    public string NetBiDiBClientId { get; set; }
    public byte NetBiDiBPairingTimeout { get; set; }
    public string NetBiDiBPairingStoreDirectory { get; set; }
    public InterfaceConnectionType ConnectionType { get; set; } = InterfaceConnectionType.NetBiDiB;
    public ConnectionStrategyType ConnectionStrategyType { get; set; } = ConnectionStrategyType.Default;
}
