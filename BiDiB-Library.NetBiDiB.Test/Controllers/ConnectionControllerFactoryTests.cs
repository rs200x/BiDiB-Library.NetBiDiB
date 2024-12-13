using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using org.bidib.Net.Core.Enumerations;
using org.bidib.Net.Core.Message;
using org.bidib.Net.NetBiDiB.Controllers;
using org.bidib.Net.NetBiDiB.Message;
using org.bidib.Net.NetBiDiB.Models;
using org.bidib.Net.NetBiDiB.Services;
using org.bidib.Net.Testing;

namespace org.bidib.Net.NetBiDiB.Test.Controllers
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

            messageProcessor = new Mock<INetBiDiBMessageProcessor>().SetupAllProperties();
            participantsService = new Mock<INetBiDiBParticipantsService>();
            var loggerFactory = new Mock<ILoggerFactory>();
            loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
            Target = new ConnectionControllerFactory(messageProcessor.Object, participantsService.Object, Mock.Of<IMessageFactory>(), loggerFactory.Object);
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
            var config = new NetBidibConfig
            {
                NetBiDiBHostAddress = "127.0.0.1",
                NetBiDiBPortNumber = 123,
                ApplicationName = "APP"
            };

            // Act
            var controller = Target.GetController(config);

            // Assert
            controller.Should().NotBeNull();
            controller.ConnectionName.Should().Be("netBiDiB 00200DFB000A14 -> 127.0.0.1:123");
            messageProcessor.Object.Emitter.Should().Be("APP");
        }
    }
}