/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ArtNet.Runtime
{
    public class ArtNetReceiver : MonoBehaviour, IDisposable
    {
        [Header("ArtNet Settings")]
        [Tooltip("自分のPCのIP / 受信するIP。0.0.0.0 なら全インターフェースで待ち受け")]
        public string host = "0.0.0.0";

        [Tooltip("Art-Net UDP Port (通常 6454)")]
        public int port = 6454;

        public event Action<ArtNetData> OnDataReceived;

        public bool IsActive => _client != null;

        [Header("Logging (Flood Protection)")]
        [Tooltip("無効パケット（短い/Art-Net以外/長すぎる/Parse失敗）を一定間隔でまとめてログ表示します")]
        [SerializeField] private bool logInvalidPackets = true;

        [Tooltip("ログをまとめて出す間隔（秒）。短いほどログは増えます")]
        [SerializeField, Min(0.1f)] private float invalidPacketLogIntervalSec = 1.0f;

        [Tooltip("ReceiveLoop側（SocketException等）のエラーを一定間隔でまとめてログ表示します")]
        [SerializeField] private bool logReceiveLoopErrors = true;

        [Tooltip("ReceiveLoopエラーのログ間隔（秒）")]
        [SerializeField, Min(0.1f)] private float receiveErrorLogIntervalSec = 2.0f;

        private UdpClient _client;
        private CancellationTokenSource _cts;
        private int _port;
        private ConcurrentQueue<byte[]> _bufferStream;

        // ---- invalid packet counters (main thread: Update) ----
        private int _dropTooShort;
        private int _dropNonArtNet;
        private int _dropTooLong;
        private int _parseOrDispatchFailed;

        private float _nextInvalidLogTime;
        private int _lastInvalidLen;
        private string _lastInvalidReason;

        // ---- ReceiveLoop error aggregation (bg thread -> main thread) ----
        private int _socketErrorCount;
        private int _otherReceiveErrorCount;
        private volatile string _lastReceiveErrorMessage;
        private float _nextReceiveErrorLogTime;

        public void Open()
        {
            if (_client != null) return;

            _port = port;
            _bufferStream = new ConcurrentQueue<byte[]>();

            // counters reset
            _dropTooShort = 0;
            _dropNonArtNet = 0;
            _dropTooLong = 0;
            _parseOrDispatchFailed = 0;
            _lastInvalidLen = 0;
            _lastInvalidReason = null;

            _socketErrorCount = 0;
            _otherReceiveErrorCount = 0;
            _lastReceiveErrorMessage = null;

            _nextInvalidLogTime = Time.unscaledTime + invalidPacketLogIntervalSec;
            _nextReceiveErrorLogTime = Time.unscaledTime + receiveErrorLogIntervalSec;

            var ip = (host == "0.0.0.0") ? IPAddress.Any : IPAddress.Parse(host);
            var endPoint = new IPEndPoint(ip, port);

            _client = new UdpClient(endPoint);
            _cts = new CancellationTokenSource();

            Task.Run(() => ReceiveLoop(_cts.Token), _cts.Token);
        }

        public void Close() => Dispose();

        public void Dispose()
        {
            if (_client == null) return;

            try { _cts?.Cancel(); } catch { }
            try { _client?.Close(); } catch { }
            try { _client?.Dispose(); } catch { }

            _client = null;
            _cts = null;
        }

        private void OnEnable() => Open();
        private void OnDisable() => Close();
        private void OnApplicationQuit() => Close();

        private void Update()
        {
            // ポート変更検知：一旦閉じて開き直す
            if (port != _port)
            {
                Close();
                Open();
            }

            // ReceiveLoopのエラーをまとめてログ（ログ洪水対策）
            FlushReceiveLoopErrorsIfNeeded();

            // パケット処理
            while (_bufferStream != null && _bufferStream.TryDequeue(out var packet))
            {
                if (packet == null)
                {
                    CountInvalid("packet is null", 0);
                    continue;
                }

                // Art-Netとして最低限見るための長さ（ヘッダ + オペコード等）
                if (packet.Length < 18)
                {
                    CountInvalid("too short (<18)", packet.Length, kind: InvalidKind.TooShort);
                    continue;
                }

                // Art-Net ID "Art-Net\0" 判定
                if (!ArtNetData.IsArtNet(packet))
                {
                    CountInvalid("non Art-Net header", packet.Length, kind: InvalidKind.NonArtNet);
                    continue;
                }

                // この実装のArtNetData(byte[])は 530 バイト配列に BlockCopy(buf.Length) するため、
                // buf.Length > 530 だと例外になり得る（今の ArtNetData.cs の仕様に合わせて先に弾く）
                if (packet.Length > 530)
                {
                    CountInvalid("too long (>530) for current parser", packet.Length, kind: InvalidKind.TooLong);
                    continue;
                }

                try
                {
                    var data = new ArtNetData(packet); // 解析は ArtNetData 側
                    OnDataReceived?.Invoke(data);      // 受信側は通知のみ
                }
                catch (Exception e)
                {
                    _parseOrDispatchFailed++;
                    _lastInvalidReason = $"parse/dispatch failed: {e.GetType().Name}";
                    _lastInvalidLen = packet.Length;
                }
            }

            // 無効パケットをまとめてログ（ログ洪水対策）
            FlushInvalidPacketLogIfNeeded();

            _port = port;
        }

        private enum InvalidKind { Other, TooShort, NonArtNet, TooLong }

        private void CountInvalid(string reason, int len, InvalidKind kind = InvalidKind.Other)
        {
            switch (kind)
            {
                case InvalidKind.TooShort: _dropTooShort++; break;
                case InvalidKind.NonArtNet: _dropNonArtNet++; break;
                case InvalidKind.TooLong: _dropTooLong++; break;
            }

            _lastInvalidReason = reason;
            _lastInvalidLen = len;
        }

        private void FlushInvalidPacketLogIfNeeded()
        {
            if (!logInvalidPackets) return;
            if (Time.unscaledTime < _nextInvalidLogTime) return;

            int totalInvalid = _dropTooShort + _dropNonArtNet + _dropTooLong + _parseOrDispatchFailed;
            if (totalInvalid > 0)
            {
                Debug.LogWarning(
                    $"[ArtNetReceiver] Invalid packets ignored (summary / last {invalidPacketLogIntervalSec:0.0}s)\n" +
                    $"- too short(<18): {_dropTooShort}\n" +
                    $"- non Art-Net header: {_dropNonArtNet}\n" +
                    $"- too long(>530): {_dropTooLong}\n" +
                    $"- parse/dispatch failed: {_parseOrDispatchFailed}\n" +
                    $"- last: reason='{_lastInvalidReason}', len={_lastInvalidLen}"
                );

                // reset for next interval
                _dropTooShort = 0;
                _dropNonArtNet = 0;
                _dropTooLong = 0;
                _parseOrDispatchFailed = 0;
                _lastInvalidReason = null;
                _lastInvalidLen = 0;
            }

            _nextInvalidLogTime = Time.unscaledTime + invalidPacketLogIntervalSec;
        }

        private void FlushReceiveLoopErrorsIfNeeded()
        {
            if (!logReceiveLoopErrors) return;
            if (Time.unscaledTime < _nextReceiveErrorLogTime) return;

            // Interlocked で安全にリセット＆取得
            int socket = Interlocked.Exchange(ref _socketErrorCount, 0);
            int other = Interlocked.Exchange(ref _otherReceiveErrorCount, 0);

            if ((socket + other) > 0)
            {
                var last = _lastReceiveErrorMessage;
                Debug.LogError(
                    $"[ArtNetReceiver] ReceiveLoop errors (summary / last {receiveErrorLogIntervalSec:0.0}s)\n" +
                    $"- SocketException: {socket}\n" +
                    $"- Other exception: {other}\n" +
                    $"- last: {last}"
                );
            }

            _nextReceiveErrorLogTime = Time.unscaledTime + receiveErrorLogIntervalSec;
        }

        private async Task ReceiveLoop(CancellationToken token)
        {
            if (_client == null) return;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var result = await _client.ReceiveAsync();
                    _bufferStream?.Enqueue(result.Buffer);
                }
                catch (ObjectDisposedException)
                {
                    // Close/Disposeで起きる想定の例外。静かに終了。
                    return;
                }
                catch (SocketException se)
                {
                    if (token.IsCancellationRequested) return;

                    Interlocked.Increment(ref _socketErrorCount);
                    _lastReceiveErrorMessage = $"{se.GetType().Name}: {se.Message}";
                    // 継続（瞬間的なネットワーク/ポート状況の変化などを想定）
                }
                catch (Exception e)
                {
                    if (token.IsCancellationRequested) return;

                    Interlocked.Increment(ref _otherReceiveErrorCount);
                    _lastReceiveErrorMessage = $"{e.GetType().Name}: {e.Message}";
                }
            }
        }
    }
}
