using System;
using System.Collections.Generic;
using org.bidib.netbidibc.netbidib.Models;
using org.bidib.netbidibc.core.Enumerations;
using org.bidib.netbidibc.core.Models.Messages.Input;
using org.bidib.netbidibc.core.Models.Messages.Output;

namespace org.bidib.netbidibc.netbidib.Message
{
    /// <summary>
    /// Instance processing netBiDiB messages
    /// </summary>
    public interface INetBiDiBMessageProcessor
    {
        /// <summary>
        /// Gets or sets the local emitter name
        /// </summary>
        string Emitter { get; set; }

        /// <summary>
        /// Gets or sets the local unique id
        /// </summary>
        byte[] UniqueId { get; set; }

        /// <summary>
        /// Gets the local address when connected as node
        /// </summary>
        byte[] Address { get; }


        /// <summary>
        /// Gets the current participant
        /// </summary>
        NetBiDiBParticipant CurrentParticipant { get; }


        /// <summary>
        /// Gets the local netBiDiB connection state 
        /// </summary>
        NetBiDiBConnectionState CurrentState { get; }

        /// <summary>
        /// Gets the netBiDiB connection state of the remote client
        /// </summary>
        NetBiDiBConnectionState RemoteState { get; }

        /// <summary>
        /// Gets or sets the action for sending messages
        /// </summary>
        Action<BiDiBOutputMessage> SendMessage { get; set; }

        /// <summary>
        /// Starts the message processing
        /// </summary>
        /// <param name="trustedParticipants">UIDs of already trusted participants</param>
        /// <param name="timeout">The pairing timeout value</param>
        void Start(IEnumerable<byte[]> trustedParticipants, byte timeout);

        /// <summary>
        /// Process  the received bidib message within the netBiDiB process
        /// </summary>
        /// <param name="message"></param>
        void ProcessMessage(BiDiBInputMessage message);

        /// <summary>
        /// Request the control of the bidib system
        /// </summary>
        void RequestControl();

        /// <summary>
        /// Reject the control of the bidib system
        /// </summary>
        void RejectControl();

        /// <summary>
        /// Returns to initial state
        /// </summary>
        void Reset();

        /// <summary>
        /// Determines the interface connection state based on the current NetBiDiB state
        /// </summary>
        /// <returns></returns>
        InterfaceConnectionState GetInterfaceConnectionState();

        /// <summary>
        /// Event raised when netBiDiB connection state has changed
        /// </summary>
        event EventHandler<EventArgs> ConnectionStateChanged;
    }
}