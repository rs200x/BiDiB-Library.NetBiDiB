using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using org.bidib.Net.Core.Enumerations;
using org.bidib.Net.Core.Message;
using org.bidib.Net.Core.Models.Messages.Input;
using org.bidib.Net.NetBiDiB.Controllers;
using org.bidib.Net.NetBiDiB.Message;
using org.bidib.Net.NetBiDiB.Models;
using org.bidib.Net.NetBiDiB.Services;
using org.bidib.Net.Testing;

namespace org.bidib.Net.NetBiDiB.Test.Controllers;

[TestClass]
[TestCategory(TestCategory.UnitTest)]
public class NetBiDiBControllerTests : TestClass<NetBiDiBController>
{
    private Mock<INetBiDiBMessageProcessor> messageProcessor;
    private Mock<INetBiDiBParticipantsService> participantsService;
    private Mock<IMessageFactory> messageFactory;

    protected override void OnTestInitialize()
    {
        base.OnTestInitialize();

        messageProcessor = new Mock<INetBiDiBMessageProcessor>();
        participantsService = new Mock<INetBiDiBParticipantsService>();
        messageFactory = new Mock<IMessageFactory>();
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new Mock<ILogger>().Object);
        Target = new NetBiDiBController(messageProcessor.Object, participantsService.Object,messageFactory.Object, loggerFactory.Object);
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
        var config = new Mock<INetBiDiBConfig>();
        config.Setup(x => x.NetBiDiBClientId).Returns(string.Empty);
        config.Setup(x => x.NetBiDiBHostAddress).Returns("127.0.0.1");
        config.Setup(x => x.NetBiDiBPortNumber).Returns(62875);

        // Act
        Target.Initialize(config.Object);

        // Assert
        Target.ConnectionName.Should().Be("netBiDiB 00200DFB000A14 -> 127.0.0.1:62875");
    }

    [TestMethod]
    public void Initialize_ShouldUseNetBiDiBClientIdForInstanceId()
    {
        // Arrange
        var config = new Mock<INetBiDiBConfig>();
        config.Setup(x => x.NetBiDiBClientId).Returns("010101");
        config.Setup(x => x.NetBiDiBHostAddress).Returns("localhost");
        config.Setup(x => x.NetBiDiBPortNumber).Returns(62875);

        // Act
        Target.Initialize(config.Object);

        // Assert
        Target.ConnectionName.Should().Be("netBiDiB 00200DFB010101 -> localhost:62875");
    }

    [TestMethod]
    public void ProcessMessage_ShouldHandleWrongMessageSize()
    {
        // Arrange
        var bytes = GetBytes("FE-09-00-00-B2-00-24-01-7B-02-10-5B-FE");
        var receivedBytes = Array.Empty<byte>();
        Target.ProcessReceivedData = b => receivedBytes = b;
        messageProcessor.Setup(x => x.CurrentState).Returns(NetBiDiBConnectionState.ConnectedControlling);

        // Act
        Target.ProcessMessage(bytes, 20);

        // Assert
        receivedBytes.Should().HaveCount(13);
        receivedBytes.Should().BeEquivalentTo(bytes);
    }
    
    [TestMethod]
    public void ProcessMessage_ShouldSForwardToMessageProcessor_WhenNotConnected()
    {
        // Arrange
        var bytes = GetBytes("0A-00-00-B2-00-24-01-7B-02-10-5B");

        var inputMessage = new BiDiBInputMessage(bytes);
        messageFactory.Setup(x => x.CreateInputMessage(It.IsAny<byte[]>())).Returns(inputMessage);
        messageProcessor.Setup(x => x.CurrentState).Returns(NetBiDiBConnectionState.Disconnected);

        // Act
        Target.ProcessMessage(bytes, 11);

        // Assert
        messageProcessor.Verify(x=>x.ProcessMessage(inputMessage));
        messageFactory.Verify(x=>x.CreateInputMessage(bytes));
    }
        
    [TestMethod]
    public void RejectControl_ShouldForwardToMessageProcessor()
    {
        // Arrange

        // Act
        Target.RejectControl();

        // Assert
        messageProcessor.Verify(x=>x.RejectControl(), Times.Once);
    }
        
    [TestMethod]
    public void RequestControl_ShouldForwardToMessageProcessor()
    {
        // Arrange

        // Act
        Target.RequestControl();

        // Assert
        messageProcessor.Verify(x=>x.RequestControl(), Times.Once);
    }
    
     
    [TestMethod]
    public void Close_ShouldReset_WhenInConnectedState()
    {
        // Arrange
        messageProcessor.Setup(x => x.CurrentState).Returns(NetBiDiBConnectionState.ConnectedControlling);
        
        // Act
        Target.Close();

        // Assert
        messageProcessor.Verify(x=>x.Reset(), Times.Once);
    }
    
    [TestMethod]
    public void Close_ShouldNotReset_WhenInDisconnectedState()
    {
        // Arrange
        messageProcessor.Setup(x => x.CurrentState).Returns(NetBiDiBConnectionState.Disconnected);
        
        // Act
        Target.Close();

        // Assert
        messageProcessor.Verify(x=>x.Reset(), Times.Never);
    }
}