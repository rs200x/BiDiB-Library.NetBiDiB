using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using org.bidib.netbidibc.netbidib.Controllers;
using org.bidib.netbidibc.netbidib.Message;
using org.bidib.netbidibc.netbidib.Services;
using org.bidib.netbidibc.core.Controllers.Interfaces;
using org.bidib.netbidibc.core.Enumerations;
using org.bidib.netbidibc.core.Test;

namespace org.bidib.netbidibc.netbidib.test.Controllers
{
    [TestClass]
    [TestCategory(TestCategory.UnitTest)]
    public class ConnectionControllerFactoryTests : TestClass<ConnectionControllerFactory>
    {
        private Mock<INetBiDiBMessageProcessor> messageProcessor;
        private Mock<INetBiDiBParticipantsService> participantsService;

        protected override void OnTestInitialize()
        {
            base.OnTestInitialize();

            messageProcessor = new Mock<INetBiDiBMessageProcessor>();
            participantsService = new Mock<INetBiDiBParticipantsService>();
            var loggerFactory = new Mock<ILoggerFactory>();
            Target = new ConnectionControllerFactory(messageProcessor.Object, participantsService.Object, loggerFactory.Object);
        }

        [TestMethod]
        public void ConnectionType_ShouldBeNetBiDiB()
        {
            // Arrange

            // Act

            // Assert
            Target.ConnectionType.Should().Be(InterfaceConnectionType.NetBiDiB);
        }

        [TestMethod]
        public void GetController_ShouldCreateAndInitController()
        {
            // Arrange
            var config = new Mock<INetConfig>();
            config.Object.ApplicationName = "APP";
            config.Object.NetworkHostAddress = "127.0.0.1";
            config.Object.NetworkPortNumber = 123;

            // Act
            var controller = Target.GetController(config.Object);

            // Assert
            controller.Should().NotBeNull();
            controller.ConnectionName.Should().Be("netBiDiB 00000DFB000A14 -> 127.0.0.1:123");
            messageProcessor.Object.Emitter.Should().Be("APP");
        }
    }
}