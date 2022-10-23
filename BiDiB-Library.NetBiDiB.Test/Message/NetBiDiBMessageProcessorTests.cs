﻿using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using org.bidib.netbidibc.netbidib.Message;
using org.bidib.netbidibc.core;
using org.bidib.netbidibc.core.Enumerations;
using org.bidib.netbidibc.core.Message;
using org.bidib.netbidibc.core.Models.Messages.Output;
using org.bidib.netbidibc.core.Test;
using LocalLinkMessage = org.bidib.netbidibc.core.Models.Messages.Input.LocalLinkMessage;
using LocalLinkOutputMessage = org.bidib.netbidibc.core.Models.Messages.Output.LocalLinkMessage;
using ProtocolSignatureOutMessage = org.bidib.netbidibc.core.Models.Messages.Output.ProtocolSignatureMessage;
using ProtocolSignatureInMessage = org.bidib.netbidibc.core.Models.Messages.Input.ProtocolSignatureMessage;
using Microsoft.Extensions.Logging.Abstractions;

namespace BiDiBLib.NetBiDiB.Test.Message
{
    [TestClass]
    [TestCategory(TestCategory.UnitTest)]
    public class NetBiDiBMessageProcessorTests : TestClass
    {
        private NetBiDiBMessageProcessor target;
        private readonly byte[] localId = { 0, 0, 0x0D, 0xFA, 0x01, 3, 1 };
        private readonly byte[] remoteId = { 10, 0, 0x0D, 0xFA, 0x01, 3, 2 };

        protected override void OnTestInitialize()
        {
            base.OnTestInitialize();

            target = new NetBiDiBMessageProcessor(NullLogger<NetBiDiBMessageProcessor>.Instance);
            BiDiBMessageGenerator.SecureMessages = false;
        }

        [TestMethod]
        public void Start_ShouldSetStateSendSignature()
        {
            // Arrange
            target.SendMessage = _ => { };

            // Act
            target.Start(Enumerable.Empty<byte[]>(), 0);

            // Assert
            target.CurrentState.Should().Be(NetBiDiBConnectionState.SendSignature);
        }

        [TestMethod]
        public void Start_ShouldSendProtocolSignature()
        {
            // Arrange
            target.Emitter = "Test";
            ProtocolSignatureOutMessage outMessage = null;
            target.SendMessage = message => outMessage = message as ProtocolSignatureOutMessage;

            // Act
            target.Start(Enumerable.Empty<byte[]>(), 0);

            // Assert
            outMessage.Should().NotBeNull();
            outMessage.Emitter.Should().Be("Test");
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetStateDisconnected_WhenEmitterIsNotBidib()
        {
            // Arrange
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new ProtocolSignatureOutMessage("Test"));

            // Act
            target.ProcessMessage(new ProtocolSignatureInMessage(messageBytes));

            // Assert
            target.CurrentState.Should().Be(NetBiDiBConnectionState.Disconnected);
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetStateWaitForId_WhenEmitterIsBidib()
        {
            // Arrange
            target.SendMessage = _ => { };
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new ProtocolSignatureOutMessage("BiDiB"));

            // Act
            target.ProcessMessage(new ProtocolSignatureInMessage(messageBytes));

            // Assert
            target.CurrentState.Should().Be(NetBiDiBConnectionState.WaitForId);
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetStateUnpaired_WhenUnknownDescriptorId()
        {
            // Arrange
            target.SendMessage = _ => { };
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, 0xFF, 0, 0, 0, 0, 0, 0, 1);
            target.Start(new List<byte[]>(), 0);
            List<LocalLinkOutputMessage> receivedMessages = new List<LocalLinkOutputMessage>();
            target.SendMessage = message => { receivedMessages.Add(message as LocalLinkOutputMessage); };

            // Act
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            // Assert
            target.CurrentState.Should().Be(NetBiDiBConnectionState.RequestPairing);
            receivedMessages.Should().HaveCount(2);
            receivedMessages[0].LinkType.Should().Be(LocalLinkType.STATUS_UNPAIRED);
            receivedMessages[1].LinkType.Should().Be(LocalLinkType.PAIRING_REQUEST);
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetStatePaired_WhenKnownDescriptorId()
        {
            // Arrange
            LocalLinkOutputMessage receivedMessage = null;
            target.SendMessage = message => { receivedMessage = message as LocalLinkOutputMessage; };
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, 0xFF, 0, 0, 0, 0, 0, 0, 1);
            target.Start(new List<byte[]> { new byte[] { 0, 0, 0, 0, 0, 0, 1 } }, 0);

            // Act
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            // Assert
            target.CurrentState.Should().Be(NetBiDiBConnectionState.Paired);
            receivedMessage.Should().NotBeNull();
            receivedMessage.LinkType.Should().Be(LocalLinkType.STATUS_PAIRED);
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetRemoteName()
        {
            // Arrange
            target.SendMessage = _ => { };
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, 0, 0x03, 0x31, 0x41, 0x51);
            target.Start(new List<byte[]> { new byte[] { 0, 0, 0, 0, 0, 0, 1 } }, 0);

            // Act
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            // Assert
            target.CurrentParticipant.ProductName.Should().Be("1AQ");
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetProtocolVersion()
        {
            // Arrange
            target.SendMessage = _ => { };
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, (byte)LocalLinkType.DESCRIPTOR_P_VERSION, 0x08, 0x00);
            target.Start(new List<byte[]> { new byte[] { 0, 0, 0, 0, 0, 0, 1 } }, 0);

            // Act
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            // Assert
            target.CurrentParticipant.ProtocolVersion.Should().Be("0.8");
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetUserName()
        {
            // Arrange
            target.SendMessage = message => { };
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, (byte)LocalLinkType.DESCRIPTOR_USER_STRING, 0x03, 0x41, 0x42, 0x43);
            target.Start(new List<byte[]> { new byte[] { 0, 0, 0, 0, 0, 0, 1 } }, 0);

            // Act
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            // Assert
            target.CurrentParticipant.UserName.Should().Be("ABC");
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetRemotePairing()
        {
            // Arrange
            LocalLinkOutputMessage receivedMessage = null;
            target.SendMessage = message => { receivedMessage = message as LocalLinkOutputMessage; };

            target.Start(new List<byte[]> { remoteId }, 0);
            var parameters = new List<byte> { (byte)LocalLinkType.DESCRIPTOR_UID };
            parameters.AddRange(remoteId);
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, parameters.ToArray());
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            parameters = new List<byte> { (byte)LocalLinkType.PAIRING_REQUEST };
            parameters.AddRange(localId);
            parameters.AddRange(remoteId);
            parameters.Add(30);
            messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, parameters.ToArray());

            // Act
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            // Assert
            target.RemoteState.Should().Be(NetBiDiBConnectionState.RequestPairing);
            target.CurrentState.Should().Be(NetBiDiBConnectionState.Paired);
            receivedMessage.Should().NotBeNull();
            receivedMessage.LinkType.Should().Be(LocalLinkType.STATUS_PAIRED);
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetLocalPairing_WhenDisconnectedPaired()
        {
            // Arrange
            LocalLinkOutputMessage receivedMessage = null;
            target.SendMessage = message => { receivedMessage = message as LocalLinkOutputMessage; };

            target.Start(new List<byte[]> { remoteId }, 1);
            var parameters = new List<byte> { (byte)LocalLinkType.DESCRIPTOR_UID };
            parameters.AddRange(remoteId);
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, parameters.ToArray());
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            parameters = new List<byte> { (byte)LocalLinkType.STATUS_UNPAIRED };
            parameters.AddRange(localId);
            parameters.AddRange(remoteId);
            messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, parameters.ToArray());

            // Act
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            // Assert
            target.RemoteState.Should().Be(NetBiDiBConnectionState.Unpaired);
            target.CurrentState.Should().Be(NetBiDiBConnectionState.RequestPairing);
            receivedMessage.Should().NotBeNull();
            receivedMessage.LinkType.Should().Be(LocalLinkType.PAIRING_REQUEST);
            receivedMessage.Timeout.Should().Be(1);
        }

        [TestMethod]
        public void ProcessMessage_ShouldSetPaired_WhenDisconnectedPairingRequest()
        {
            // Arrange
            List<LocalLinkOutputMessage> receivedMessages = new List<LocalLinkOutputMessage>();
            target.SendMessage = message => { receivedMessages.Add(message as LocalLinkOutputMessage); };

            target.Start(new List<byte[]> { remoteId }, 1);
            var parameters = new List<byte> { (byte)LocalLinkType.DESCRIPTOR_UID };
            parameters.AddRange(remoteId);
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, parameters.ToArray());
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            parameters = new List<byte> { (byte)LocalLinkType.STATUS_UNPAIRED };
            parameters.AddRange(localId);
            parameters.AddRange(remoteId);
            messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, parameters.ToArray());
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            parameters = new List<byte> { (byte)LocalLinkType.STATUS_PAIRED };
            parameters.AddRange(localId);
            parameters.AddRange(remoteId);
            messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, parameters.ToArray());

            // Act
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            // Assert
            target.RemoteState.Should().Be(NetBiDiBConnectionState.Paired);
            target.CurrentState.Should().Be(NetBiDiBConnectionState.Paired);
            receivedMessages.Should().HaveCount(4);
            receivedMessages[1].LinkType.Should().Be(LocalLinkType.STATUS_PAIRED);
            receivedMessages[2].LinkType.Should().Be(LocalLinkType.PAIRING_REQUEST);
            receivedMessages[3].LinkType.Should().Be(LocalLinkType.STATUS_PAIRED);
        }

        [TestMethod]
        public void RejectControl_ShouldSetConnectedUncontrolled()
        {
            // Arrange
            LocalLogonRejectedMessage receivedMessage = null;
            target.SendMessage = message => { receivedMessage = message as LocalLogonRejectedMessage; };
            target.Start(new List<byte[]> { remoteId }, 1);

            // Act
            target.RejectControl();

            // Assert
            target.CurrentState.Should().Be(NetBiDiBConnectionState.ConnectedUncontrolled);
            receivedMessage.Should().NotBe(null);
        }

        [TestMethod]
        public void RejectControl_ShouldSetPaired()
        {
            // Arrange
            LocalLinkOutputMessage receivedMessage = null;
            target.SendMessage = message => { receivedMessage = message as LocalLinkOutputMessage; };
            target.Start(new List<byte[]>(), 1);

            var parameters = new List<byte> { (byte)LocalLinkType.DESCRIPTOR_UID };
            parameters.AddRange(remoteId);
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, parameters.ToArray());
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            // Act
            target.RequestControl();

            // Assert
            target.CurrentState.Should().Be(NetBiDiBConnectionState.Paired);
            receivedMessage.Should().NotBe(null);
        }


        [TestMethod]
        public void Reset_ShouldResetStates()
        {
            // Arrange
            target.SendMessage = message => { };
            target.Start(new List<byte[]>(), 1);

            var parameters = new List<byte> { (byte)LocalLinkType.DESCRIPTOR_UID };
            parameters.AddRange(remoteId);
            var messageBytes = BiDiBMessageGenerator.GenerateMessage(new byte[] { 0 }, BiDiBMessage.MSG_LOCAL_LINK, 0, parameters.ToArray());
            target.ProcessMessage(new LocalLinkMessage(messageBytes));

            // Act
            target.Reset();

            // Assert
            target.CurrentState.Should().Be(NetBiDiBConnectionState.Disconnected);
            target.RemoteState.Should().Be(NetBiDiBConnectionState.Disconnected);
        }
    }
}
