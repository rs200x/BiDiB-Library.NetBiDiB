using Microsoft.Extensions.Logging;
using org.bidib.Net.NetBiDiB.Message;
using org.bidib.Net.NetBiDiB.Services;
using org.bidib.Net.Core.Controllers.Interfaces;
using org.bidib.Net.Core.Enumerations;

namespace org.bidib.Net.NetBiDiB.Controllers
{
    internal class ServerControllerFactory : IConnectionControllerFactory
    {
        private readonly INetBiDiBMessageProcessor messageProcessor;
        private readonly INetBiDiBParticipantsService participantsService;
        private readonly ILoggerFactory loggerFactory;
        private INetBiDiBServerController controller;

        public ServerControllerFactory(INetBiDiBMessageProcessor messageProcessor, INetBiDiBParticipantsService participantsService, ILoggerFactory loggerFactory)
        {
            this.messageProcessor = messageProcessor;
            this.participantsService = participantsService;
            this.loggerFactory = loggerFactory;
        }

        /// <inheritdoc />
        public InterfaceConnectionType ConnectionType => InterfaceConnectionType.NetBiDiBServer;

        /// <inheritdoc />
        public IConnectionController GetController(IConnectionConfig connectionConfig)
        {
            controller ??= new NetBiDiBServerController(messageProcessor, participantsService, loggerFactory);

            controller.Initialize(connectionConfig as INetConfig);
            return controller;
        }
    }
}
