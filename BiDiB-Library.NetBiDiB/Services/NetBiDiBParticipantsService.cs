using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using org.bidib.Net.Core.Services.Interfaces;
using org.bidib.Net.Core.Utils;
using org.bidib.Net.NetBiDiB.Models;

namespace org.bidib.Net.NetBiDiB.Services
{
    [Export(typeof(INetBiDiBParticipantsService))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    public class NetBiDiBParticipantsService : INetBiDiBParticipantsService
    {
        private readonly IIoService ioService;
        private readonly IJsonService jsonService;
        private readonly ILogger<NetBiDiBParticipantsService> logger;
        private readonly List<NetBiDiBParticipant> participants;

        private readonly string defaultStoreDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bidib", "data", "netBiDiB");
        private const string DefaultStoreFileName = "netBiDiBPairingStore-{0}.bidib";
        private string storeDirectoryPath;
        private string storeFilePath;

        [ImportingConstructor]
        public NetBiDiBParticipantsService(IIoService ioService, IJsonService jsonService, ILoggerFactory loggerFactory)
        {
            this.ioService = ioService;
            this.jsonService = jsonService;
            logger = loggerFactory.CreateLogger<NetBiDiBParticipantsService>();
            participants = new List<NetBiDiBParticipant>();
        }

        public IEnumerable<NetBiDiBParticipant> TrustedParticipants => participants;

        public void Initialize(INetBiDiBConfig config)
        {
            if (config == null) { throw new ArgumentNullException(nameof(config)); }

            storeDirectoryPath = !string.IsNullOrEmpty(config.NetBiDiBPairingStoreDirectory) ? config.NetBiDiBPairingStoreDirectory : defaultStoreDirectoryPath;
            var storeFileName = string.Format(CultureInfo.CurrentUICulture, DefaultStoreFileName, config.NetBiDiBClientId);
            storeFilePath = ioService.GetPath(storeDirectoryPath, storeFileName);

            LoadParticipants();
        }

        public void AddOrUpdate(NetBiDiBParticipant participant)
        {
            if (participant == null) { return; }

            var existing = participants.Find(x => x.Id.GetArrayValue() == participant.Id.GetArrayValue());

            if (existing != null)
            {
                _ = participants.Remove(existing);
            }

            participant.LastSeen = DateTime.Now;
            participants.Add(participant);
            SaveParticipants();
        }

        private void LoadParticipants()
        {
            participants.Clear();

            if (!ioService.DirectoryExists(storeDirectoryPath))
            {
                ioService.CreateDirectory(storeDirectoryPath);
            }

            if (!ioService.FileExists(storeFilePath)) { return; }

            try
            {
                var items = jsonService.LoadFromFile<NetBiDiBParticipant[]>(storeFilePath);
                participants.AddRange(items.Where(x => x != null));
                logger.LogInformation("{Participants} netBiDiB participants loaded.", participants.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not load participants from {StoreFilePath}", storeDirectoryPath);
            }
        }

        private void SaveParticipants()
        {
            if (!jsonService.SaveToFile(participants, storeFilePath))
            {
                logger.LogWarning("netBiDiB participants could not be stored to: {StoreFilePath}", storeFilePath);
            }
        }
    }
}
