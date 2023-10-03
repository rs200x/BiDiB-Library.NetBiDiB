using org.bidib.Net.Core.Controllers.Interfaces;

namespace org.bidib.Net.NetBiDiB.Models;

public interface INetBiDiBConfig : INetConfig
{
    string NetBiDiBClientId { get; set; }

    byte NetBiDiBPairingTimeout { get; set; }

    string NetBiDiBPairingStoreDirectory { get; set; }
}
