using Microsoft.Extensions.Logging;
using org.bidib.netbidibc.netbidib.Message;
using org.bidib.netbidibc.netbidib.Services;
using org.bidib.netbidibc.core.Controllers.Interfaces;
using org.bidib.netbidibc.core.Enumerations;
using System;

namespace org.bidib.netbidibc.netbidib.Controllers
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
