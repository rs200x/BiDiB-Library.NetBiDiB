using org.bidib.netbidibc.core.Controllers.Interfaces;

namespace org.bidib.netbidibc.netbidib.Models;

public interface INetBiDiBConfig : INetConfig
{
    string PairingStoreDirectory { get; set; }
}
