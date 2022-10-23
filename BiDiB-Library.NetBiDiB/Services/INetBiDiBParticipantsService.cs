using org.bidib.netbidibc.netbidib.Models;
using System.Collections.Generic;

namespace org.bidib.netbidibc.netbidib.Services
{
  public interface INetBiDiBParticipantsService
  {
    IEnumerable<NetBiDiBParticipant> TrustedParticipants { get; }

    void Initialize(INetBiDiBConfig config);

    void AddOrUpdate(NetBiDiBParticipant participant);
  }
}
