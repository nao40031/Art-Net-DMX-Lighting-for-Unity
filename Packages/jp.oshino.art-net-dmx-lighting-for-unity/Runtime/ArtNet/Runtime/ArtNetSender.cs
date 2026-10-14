using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace ArtNet.Runtime
{
    public class ArtNetSender : MonoBehaviour
    {
        public enum CommunicationMode { Unicast, Broadcast }

        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] public int port = 6454;
        [SerializeField, Tooltip("送信方式を選びます。\n\nChoose unicast or broadcast output.")]
        private CommunicationMode communicationMode;
        [SerializeField, Tooltip("ブロードキャスト時の宛先IPです。対象ネットワークのブロードキャストアドレスを指定できます。\n\nBroadcast destination IP. You can specify a directed broadcast address for the target network.")]
        private string broadcastAddress = "255.255.255.255";
        [SerializeField, Tooltip("送信元のネットワークアダプターのIPv4アドレスです。0.0.0.0でOSに任せます。\n\nIPv4 address of the outgoing network adapter. Use 0.0.0.0 for OS selection.")]
        private string localInterfaceAddress = "0.0.0.0";
        [SerializeField, Tooltip("同じデータを127.0.0.1にも送信します。\nAlso send the same data to 127.0.0.1.")]
        private bool sendToLoopback;
        private Queue<ArtNetData> bufferStream;
        private UdpClient _client;
        private UdpClient _loopbackClient;
        public bool IsActive => this._client != null;
        public string Host => host;
        public string DestinationAddress => communicationMode == CommunicationMode.Broadcast ? broadcastAddress : host;
        public event Action<ArtNetData> OnDataSent;
        private int _port;
        private string _connectedDestination;
        private string _connectedInterface;
        private CommunicationMode _connectedMode;
        private bool _connectedSendToLoopback;

        public void Open()
        {
            if (_client != null) return;
            _port = port;
            try
            {
                string destination = DestinationAddress;
                _client = CreateClient();
                if (communicationMode == CommunicationMode.Broadcast)
                    _client.EnableBroadcast = true;
                _client.Connect(destination, port);
                if (sendToLoopback && !IsDefaultLoopbackDestination(destination))
                {
                    _loopbackClient = new UdpClient();
                    _loopbackClient.Connect(IPAddress.Loopback, port);
                }

                _connectedDestination = destination;
                _connectedInterface = localInterfaceAddress;
                _connectedMode = communicationMode;
                _connectedSendToLoopback = sendToLoopback;
            }
            catch
            {
                Close();
                throw;
            }
        }

        private void OnEnable() => Open();
        private void OnDisable() => Close();

        public void Close()
        {
            _client?.Close();
            _client = null;
            _loopbackClient?.Close();
            _loopbackClient = null;
        }

        private void Update()
        {
            if (port != _port ||
                communicationMode != _connectedMode ||
                sendToLoopback != _connectedSendToLoopback ||
                !string.Equals(DestinationAddress, _connectedDestination, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(localInterfaceAddress, _connectedInterface, StringComparison.OrdinalIgnoreCase))
            {
                Close();
                Open();
            }

            _port = port;
        }

        public void Send(int[] channels,
            ArtNetOpCode opCode = ArtNetOpCode.OpDmx,
            int sequence = 0,
            int physical = 0,
            int universe = 0,
            int protocolVersionHi = 0,
            int protocolVersionLo = 14,
            int lengthHi = 2,
            int lengthLo = 0)
        {
            if (!IsActive) return;
            var data = new ArtNetData(
                channels: channels,
                opCode: opCode,
                sequence: sequence,
                physical: physical,
                universe: universe,
                protocolVersionHi: protocolVersionHi,
                protocolVersionLo: protocolVersionLo,
                lengthHi: lengthHi,
                lengthLo: lengthLo
            );
            var sendBuf = data.ToBytes();
            _client.Send(sendBuf, sendBuf.Length);
            _loopbackClient?.Send(sendBuf, sendBuf.Length);
            OnDataSent?.Invoke(data);
        }

        private static bool IsDefaultLoopbackDestination(string value)
        {
            return string.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   (IPAddress.TryParse(value, out var address) && IPAddress.Loopback.Equals(address));
        }

        private UdpClient CreateClient()
        {
            if (string.IsNullOrEmpty(localInterfaceAddress) || localInterfaceAddress == "0.0.0.0")
                return new UdpClient();

            if (!IPAddress.TryParse(localInterfaceAddress, out var address) ||
                address.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("送信元NICにはIPv4アドレスを指定してください。\n\nThe outgoing network adapter must have an IPv4 address.");

            return new UdpClient(new IPEndPoint(address, 0));
        }
    }
}
