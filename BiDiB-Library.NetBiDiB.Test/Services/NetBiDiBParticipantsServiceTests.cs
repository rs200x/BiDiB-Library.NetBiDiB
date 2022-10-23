using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using org.bidib.netbidibc.netbidib.Models;
using org.bidib.netbidibc.netbidib.Services;
using org.bidib.netbidibc.core.Services.Interfaces;
using org.bidib.netbidibc.core.Test;

namespace org.bidib.netbidibc.netbidib.test.Services
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

            Target = new NetBiDiBParticipantsService(ioService.Object, jsonService.Object);
        }

        [TestMethod]
        public void Ctor_ShouldLoadParticipants()
        {
            // Arrange
            ioService.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(true);
            ioService.Setup(x => x.FileExists(It.IsAny<string>())).Returns(true);
            var participant = new NetBiDiBParticipant();
            jsonService.Setup(x => x.LoadFromFile<NetBiDiBParticipant[]>(It.IsAny<string>())).Returns(new[] { participant });

            // Act
            Target = new NetBiDiBParticipantsService(ioService.Object, jsonService.Object);

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