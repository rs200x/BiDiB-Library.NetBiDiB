using System.ComponentModel.Composition;
using Microsoft.Extensions.Logging;
using org.bidib.Net.Core.Controllers.Interfaces;
using org.bidib.Net.Core.Enumerations;
using org.bidib.Net.Core.Message;
using org.bidib.Net.NetBiDiB.Message;
using org.bidib.Net.NetBiDiB.Models;
using org.bidib.Net.NetBiDiB.Services;

namespace org.bidib.Net.NetBiDiB.Controllers
{
    [Export(typeof(IConnectionControllerFactory))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class ConnectionControllerFactory : IConnectionControllerFactory
    {
        private readonly INetBiDiBMessageProcessor messageProcessor;
        private readonly INetBiDiBParticipantsService participantsService;
        private readonly IMessageFactory messageFactory;
        private readonly ILoggerFactory loggerFactory;
        private INetBiDiBController controller;

        [ImportingConstructor]
        public ConnectionControllerFactory(
            INetBiDiBMessageProcessor messageProcessor, 
            INetBiDiBParticipantsService participantsService, 
            IMessageFactory messageFactory, 
            ILoggerFactory loggerFactory)
        {
            this.messageProcessor = messageProcessor;
            this.participantsService = participantsService;
            this.messageFactory = messageFactory;
            this.loggerFactory = loggerFactory;
        }

        /// <inheritdoc />
        public InterfaceConnectionType ConnectionType => InterfaceConnectionType.NetBiDiB;

        /// <inheritdoc />
        public IConnectionController GetController(IConnectionConfig connectionConfig)
        {
            controller ??= new NetBiDiBController(messageProcessor, participantsService, messageFactory, loggerFactory);

            var config = connectionConfig as INetBiDiBConfig;

            participantsService.Initialize(config);
            controller.Initialize(config);
            return controller;
        }
    }
}
