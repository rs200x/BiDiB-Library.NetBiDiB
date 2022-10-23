using org.bidib.netbidibc.netbidib.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using org.bidib.netbidibc.core.Services.Interfaces;
using org.bidib.netbidibc.core.Utils;
using Microsoft.Extensions.Logging;

namespace org.bidib.netbidibc.netbidib.Services
{
    public class NetBiDiBParticipantsService : INetBiDiBParticipantsService
    {
        private readonly IIoService ioService;
        private readonly IJsonService jsonService;
        private readonly ILogger<NetBiDiBParticipantsService> logger;
        private readonly List<NetBiDiBParticipant> participants;

        private readonly string defaultStoreDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".bidib", "data", "netBiDiB");
        private const string StoreFileName = "netBiDiBPairingStore.bidib";
        private string storeDirectoryPath;
        private string storeFilePath;

        public NetBiDiBParticipantsService(IIoService ioService, IJsonService jsonService, ILogger<NetBiDiBParticipantsService> logger)
        {
            this.ioService = ioService;
            this.jsonService = jsonService;
            this.logger = logger;
            participants = new List<NetBiDiBParticipant>();
        }

        public IEnumerable<NetBiDiBParticipant> TrustedParticipants => participants;

        public void Initialize(INetBiDiBConfig config)
        {
            if (config == null) { throw new ArgumentNullException(nameof(config)); }

            storeDirectoryPath = !string.IsNullOrEmpty(config.PairingStoreDirectory) ? config.PairingStoreDirectory : defaultStoreDirectoryPath;
            storeFilePath = Path.Combine(storeDirectoryPath, StoreFileName);

            LoadParticipants();
        }

        public void AddOrUpdate(NetBiDiBParticipant participant)
        {
            if (participant == null) { return; }

            var existing = participants.FirstOrDefault(x => x.Id.GetArrayValue() == participant.Id.GetArrayValue());

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
                logger.LogInformation($"{participants.Count} netBiDiB participants loaded.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"could not load participants from {storeFilePath}");
            }
        }

        private void SaveParticipants()
        {
            if (!jsonService.SaveToFile(participants, storeFilePath))
            {
                logger.LogWarning($"netBiDiB participants could not be stored to: {storeFilePath}");
            }
        }
    }
}
