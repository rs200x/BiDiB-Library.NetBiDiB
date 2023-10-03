using System.Collections.Generic;
using org.bidib.Net.NetBiDiB.Models;

namespace org.bidib.Net.NetBiDiB.Services
{
  public interface INetBiDiBParticipantsService
  {
    IEnumerable<NetBiDiBParticipant> TrustedParticipants { get; }

    void Initialize(INetBiDiBConfig config);

    void AddOrUpdate(NetBiDiBParticipant participant);
  }
}
