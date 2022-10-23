using Newtonsoft.Json;
using System;
using System.Linq;

namespace org.bidib.netbidibc.netbidib.Models
{
    [Serializable]
    public class NetBiDiBParticipant
    {
        private byte[] id;
        private string uid;

        [JsonProperty("productName")]
        public string ProductName { get; set; }

        [JsonProperty("requestorName")]
        public string RequestorName { get; set; }

        [JsonProperty("userName")]
        public string UserName { get; set; }

        [JsonProperty("uid")]
        public string Uid
        {
            get
            {
                if (string.IsNullOrEmpty(uid) && id != null )
                {
                    uid = $"0x{string.Join("", id.Select(b => $"{b:X2}"))}";
                }
                return uid;
            }
            set => uid = value;
        }

        [JsonProperty("lastSeen")]
        public DateTime LastSeen { get; set; }

        [JsonProperty("protocolVersion")]
        public string ProtocolVersion { get; set; }

        [JsonIgnore]
        public byte[] Id
        {
            get
            {
                if (id == null && !string.IsNullOrEmpty(uid))
                {
                    id = GetId();
                }

                return id;
            }
            set => id = value;
        }

        public override string ToString() => $"{ProductName} - {Uid}";

        private byte[] GetId()
        {
            var hexIs = Uid.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase) ? uid.Substring(2) : Uid;
            byte[] hexBytes = Enumerable.Range(0, hexIs.Length)
                .Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(hexIs.Substring(x, 2), 16))
                .ToArray();
            byte[] uniqueIdBytes = new byte[7];
            Array.Copy(hexBytes, 0, uniqueIdBytes, 0, 7);
            return uniqueIdBytes;
        }
    }
}
