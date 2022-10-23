using Microsoft.Extensions.Logging;
using org.bidib.netbidibc.netbidib.Message;
using org.bidib.netbidibc.netbidib.Models;
using org.bidib.netbidibc.netbidib.Services;
using org.bidib.netbidibc.core.Controllers.Interfaces;
using org.bidib.netbidibc.core.Enumerations;

namespace org.bidib.netbidibc.netbidib.Controllers
{

  public class ConnectionControllerFactory : IConnectionControllerFactory
  {
    private readonly INetBiDiBMessageProcessor messageProcessor;
    private readonly INetBiDiBParticipantsService participantsService;
    private readonly ILoggerFactory loggerFactory;
    private INetBiDiBController controller;

    public ConnectionControllerFactory(INetBiDiBMessageProcessor messageProcessor, INetBiDiBParticipantsService participantsService, ILoggerFactory loggerFactory)
    {
      this.messageProcessor = messageProcessor;
      this.participantsService = participantsService;
      this.loggerFactory = loggerFactory;
    }

    /// <inheritdoc />
    public InterfaceConnectionType ConnectionType => InterfaceConnectionType.NetBiDiB;

    /// <inheritdoc />
    public IConnectionController GetController(IConnectionConfig connectionConfig)
    {
      controller ??= new NetBiDiBController(messageProcessor, participantsService, loggerFactory);

      var config = connectionConfig as INetBiDiBConfig;

      participantsService.Initialize(config);
      controller.Initialize(config);
      return controller;
    }
  }
}
