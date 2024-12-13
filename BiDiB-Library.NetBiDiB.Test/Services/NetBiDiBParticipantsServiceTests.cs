using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using org.bidib.Net.Core.Services.Interfaces;
using org.bidib.Net.NetBiDiB.Models;
using org.bidib.Net.NetBiDiB.Services;
using org.bidib.Net.Testing;

namespace org.bidib.Net.NetBiDiB.Test.Services
{
    [TestClass]
    [TestCategory(TestCategory.UnitTest)]
    public class NetBiDiBParticipantsServiceTests : TestClass<NetBiDiBParticipantsService>
    {
        private Mock<IIoService> ioService;
        private Mock<IJsonService> jsonService;

        protected override void OnTestInitialize()
        {
            base.OnTestInitialize();

            ioService = new Mock<IIoService>();
            jsonService = new Mock<IJsonService>();

            Target = new NetBiDiBParticipantsService(ioService.Object, jsonService.Object, NullLogger<NetBiDiBParticipantsService>.Instance);
        }

        [TestMethod]
        public void Initialize_ShouldLoadParticipants()
        {
            // Arrange
            ioService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
            ioService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
            var participant = new NetBiDiBParticipant();
            jsonService.Setup(x => x.LoadFromFile<NetBiDiBParticipant[]>(It.IsAny<string>())).Returns(new[] { participant });
            var config = new Mock<INetBiDiBConfig>().SetupAllProperties();

            // Act
            Target.Initialize(config.Object);

            // Assert
            Target.TrustedParticipants.Should().HaveCount(1);
            Target.TrustedParticipants.Should().Contain(participant);
        }

        [TestMethod]
        public void AddOrUpdate_ShouldAddAndSaveParticipant()
        {
            // Arrange
            var participant = new NetBiDiBParticipant();

            // Act
            Target.AddOrUpdate(participant);

            // Assert
            Target.TrustedParticipants.Should().HaveCount(1);
            jsonService.Verify(x => x.SaveToFile(Target.TrustedParticipants, It.IsAny<string>()));
        }

        [TestMethod]
        public void AddOrUpdate_ShouldReplaceParticipant()
        {
            // Arrange
            var participant = new NetBiDiBParticipant { Id = new byte[] { 0x01 } };
            Target.AddOrUpdate(participant);
            var participant2 = new NetBiDiBParticipant { Id = new byte[] { 0x01 } };

            // Act
            Target.AddOrUpdate(participant2);

            // Assert
            Target.TrustedParticipants.Should().HaveCount(1);
            Target.TrustedParticipants.Should().Contain(participant2);
            jsonService.Verify(x => x.SaveToFile(Target.TrustedParticipants, It.IsAny<string>()));
        }
    }
}