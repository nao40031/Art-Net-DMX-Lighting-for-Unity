using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

namespace ArtNet.Runtime
{
    public class ArtNetSender : MonoBehaviour
    {
        [SerializeField] private string host = "127.0.0.1";
        [SerializeField] public int port = 6454;
        private Queue<ArtNetData> bufferStream;
        private UdpClient _client;
        public bool IsActive => this._client != null;
        private int _port;

        public void Open()
        {
            if (_client != null) return;
            _port = port;
            _client = new UdpClient();
            _client.Connect(host, port);
        }

        private void OnEnable() => Open();
        private void OnDisable() => Close();

        public void Close()
        {
            if (_client == null) return;
            _client.Close();
            _client = null;
        }

        private void Update()
        {
            if (port != _port)
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
            var sendBuf = new ArtNetData(
                channels: channels,
                opCode: opCode,
                sequence: sequence,
                physical: physical,
                universe: universe,
                protocolVersionHi: protocolVersionHi,
                protocolVersionLo: protocolVersionLo,
                lengthHi: lengthHi,
                lengthLo: lengthLo
            ).ToBytes();
            _client.Send(sendBuf, sendBuf.Length);
        }
    }
}