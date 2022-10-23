using System.Globalization;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using org.bidib.netbidibc.netbidib.Message;
using org.bidib.netbidibc.core;
using org.bidib.netbidibc.core.Enumerations;
using org.bidib.netbidibc.core.Message;
using org.bidib.netbidibc.core.Models;
using org.bidib.netbidibc.core.Models.Messages.Input;
using org.bidib.netbidibc.core.Properties;
using org.bidib.netbidibc.core.Services.Interfaces;
using org.bidib.netbidibc.core.Test;

namespace org.bidib.netbidibc.netbidib.test.Message
{

    [TestClass]
    [TestCategory(TestCategory.UnitTest)]
    public class NetBiDiBMessageReceiverTests : TestClass<NetBiDiBMessageReceiver>
    {
        private Mock<IBiDiBNodesFactory> nodesFactory;
        private BiDiBNode node;

        protected override void OnTestInitialize()
        {
            base.OnTestInitialize();

            node = new BiDiBNode { Address = new byte[] { 0 } };
            nodesFactory = new Mock<IBiDiBNodesFactory>();

            Target = new NetBiDiBMessageReceiver(nodesFactory.Object);
        }

        [TestMethod]
        public void ProcessMessage_ShouldNotProcess_WhenMessageNull()
        {
            // Arrange

            // Act
            Target.ProcessMessage(null);

            // Assert
            nodesFactory.Verify(x => x.GetNode(It.IsAny<byte[]>()), Times.Never);
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetOKState_WhenLogon()
        {
            // Arrange
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LOGON);
            var message = new BiDiBInputMessage(messageBytes);
            nodesFactory.Setup(x => x.GetNode(It.IsAny<byte[]>())).Returns(node);

            // Act
            Target.ProcessMessage(message);

            // Assert
            node.State.Should().Be(NodeState.Ok);
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetAvailableState_WhenLogoff()
        {
            // Arrange
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LOGOFF);
            var message = new BiDiBInputMessage(messageBytes);
            nodesFactory.Setup(x => x.GetNode(It.IsAny<byte[]>())).Returns(node);

            // Act
            Target.ProcessMessage(message);

            // Assert
            node.State.Should().Be(NodeState.Available);
        }

        [TestMethod]
        public void HandleLocalLinkMessage_ShouldNotProcess_WhenMessageNotLocalLinkType()
        {
            // Arrange
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK);
            var message = new BiDiBInputMessage(messageBytes);

            // Act
            Target.ProcessMessage(message);

            // Assert
            nodesFactory.Verify(x => x.GetNode(It.IsAny<byte[]>()), Times.Never);
        }

        [TestMethod]
        public void HandleLocalLinkMessage_ShouldSetUnavailableState()
        {
            // Arrange
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0,
                0x81, 0x41, 0x42, 0x43);
            var message = new LocalLinkMessage(messageBytes);
            nodesFactory.Setup(x => x.GetNode(It.IsAny<byte[]>())).Returns(node);

            // Act
            Target.ProcessMessage(message);

            // Assert
            node.State.Should().Be(NodeState.Unavailable);
            node.StateInfo.Should()
                .Be(string.Format(CultureInfo.CurrentCulture, Resources.NodeControlledByOther, "ABC"));
        }

        [TestMethod]
        public void HandleLocalLinkMessage_ShouldSetAvailableState()
        {
            // Arrange
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0,
                0x82, 0, 0, 0, 0, 0, 0, 1);
            var message = new LocalLinkMessage(messageBytes);
            nodesFactory.Setup(x => x.GetNode(It.IsAny<byte[]>())).Returns(node);

            // Act
            Target.ProcessMessage(message);

            // Assert
            node.State.Should().Be(NodeState.Available);
            node.StateInfo.Should().Be(Resources.NodeAvailableForControl);
        }
    }
}