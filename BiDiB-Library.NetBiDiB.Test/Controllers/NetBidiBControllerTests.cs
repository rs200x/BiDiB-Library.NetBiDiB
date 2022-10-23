using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using org.bidib.netbidibc.netbidib.Controllers;
using org.bidib.netbidibc.netbidib.Message;
using org.bidib.netbidibc.netbidib.Services;
using org.bidib.netbidibc.core.Controllers.Interfaces;
using org.bidib.netbidibc.core.Message;
using org.bidib.netbidibc.core.Test;

namespace org.bidib.netbidibc.netbidib.test.Controllers
{
    [TestClass]
    [TestCategory(TestCategory.UnitTest)]
    public class NetBiDiBControllerTests : TestClass<NetBiDiBController>
    {
        private Mock<INetBiDiBMessageProcessor> messageProcessor;
        private Mock<INetBiDiBParticipantsService> participantsService;

        protected override void OnTestInitialize()
        {
            base.OnTestInitialize();

            messageProcessor = new Mock<INetBiDiBMessageProcessor>();
            participantsService = new Mock<INetBiDiBParticipantsService>();
            var loggerFactory = new Mock<ILoggerFactory>();
            Target = new NetBiDiBController(messageProcessor.Object, participantsService.Object, loggerFactory.Object);
        }

        protected override void OnTestCleanup()
        {
            base.OnTestCleanup();

            BiDiBMessageGenerator.SecureMessages = true;
        }

        [TestMethod]
        public void Initialize_ShouldThrow_WhenConfigNull()
        {
            // Arrange

            // Act
            var action = new Action(() => Target.Initialize(null));

            // Assert
            action.Should().Throw<ArgumentNullException>().And.ParamName.Should().Be("config");
        }

        [TestMethod]
        public void Initialize_ShouldUseDefaultId_WhenNetBiDiBClientIdNotSet()
        {
            // Arrange
            var config = new Mock<INetConfig>();
            config.Setup(x => x.NetBiDiBClientId).Returns(string.Empty);
            config.Setup(x => x.NetworkHostAddress).Returns("127.0.0.1");
            config.Setup(x => x.NetworkPortNumber).Returns(62875);

            // Act
            Target.Initialize(config.Object);

            // Assert
            Target.ConnectionName.Should().Be("netBiDiB 00000DFB000A14 -> 127.0.0.1:62875");
        }

        [TestMethod]
        public void Initialize_ShouldUseNetBiDiBClientIdForInstanceId()
        {
            // Arrange
            var config = new Mock<INetConfig>();
            config.Setup(x => x.NetBiDiBClientId).Returns("010101");
            config.Setup(x => x.NetworkHostAddress).Returns("localhost");
            config.Setup(x => x.NetworkPortNumber).Returns(62875);

            // Act
            Target.Initialize(config.Object);

            // Assert
            Target.ConnectionName.Should().Be("netBiDiB 00000DFB010101 -> localhost:62875");
        }

        [TestMethod]
        public void ProcessMessage_ShouldHandleWrongMessageSize()
        {
            // Arrange
            var bytes = GetBytes("FE-09-00-00-B2-00-24-01-7B-02-10-5B-FE");
            var receivedBytes = new byte[0];
            Target.ProcessReceivedData = b => receivedBytes = b;

            // Act
            Target.ProcessMessage(bytes, 20);

            // Assert
            receivedBytes.Should().HaveCount(13);
            receivedBytes.Should().BeEquivalentTo(bytes);
        }
    }
}
