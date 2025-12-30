using System.Text;
using UnityEngine;

namespace ArtNet.Runtime
{
    /// <summary>
    /// DMXの任意チャンネルをInspectorに表示するデバッグ用Subscriber
    /// + 任意で一定間隔の要約ログ（洪水対策付き）
    /// </summary>
    public class DmxDebugMonitor : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("購読対象の ArtNetReceiver（未設定なら同一GameObjectから自動取得）")]
        public ArtNetReceiver receiver;

        [Header("Monitor Channels (1-512)")]
        [Range(1, 512)] public int monitorCh1 = 1;
        [Range(1, 512)] public int monitorCh2 = 2;
        [Range(1, 512)] public int monitorCh3 = 3;
        [Range(1, 512)] public int monitorCh4 = 4;
        [Range(1, 512)] public int monitorCh5 = 5;

        [Header("Debug Values (ReadOnly)")]
        [SerializeField, Range(0, 255)] private int ch1;
        [SerializeField, Range(0, 255)] private int ch2;
        [SerializeField, Range(0, 255)] private int ch3;
        [SerializeField, Range(0, 255)] private int ch4;
        [SerializeField, Range(0, 255)] private int ch5;

        [Header("Optional Filter")]
        [Tooltip("特定Universeのみ処理したい場合に設定。-1 でフィルタ無効")]
        public int onlyUniverse = -1;

        [Header("Periodic Logging (Flood Protection)")]
        [Tooltip("一定間隔で受信データを要約ログ表示します（ログ洪水対策あり）")]
        [SerializeField] private bool enablePeriodicLog = false;

        [Tooltip("ログを出す間隔（秒）。例：1.0 で1秒ごと")]
        [SerializeField, Min(0.1f)] private float logIntervalSec = 1.0f;

        [Tooltip("受信が無い間隔はログしない（無駄ログ抑制）")]
        [SerializeField] private bool logOnlyWhenDataArrived = true;

        [Tooltip("ログの先頭にヘッダー（Universe/Length等）を含める")]
        [SerializeField] private bool includeHeaderInLog = true;

        private float _nextLogTime;
        private bool _hasNewDataSinceLastLog;
        private ArtNetData _lastData;

        private void OnEnable()
        {
            if (receiver == null) receiver = GetComponent<ArtNetReceiver>();
            if (receiver != null) receiver.OnDataReceived += Handle;

            _nextLogTime = Time.unscaledTime + logIntervalSec;
            _hasNewDataSinceLastLog = false;
        }

        private void OnDisable()
        {
            if (receiver != null) receiver.OnDataReceived -= Handle;
        }

        private void Update()
        {
            if (!enablePeriodicLog) return;
            if (Time.unscaledTime < _nextLogTime) return;

            if (logOnlyWhenDataArrived && !_hasNewDataSinceLastLog)
            {
                _nextLogTime = Time.unscaledTime + logIntervalSec;
                return;
            }

            Debug.Log(BuildSummaryLog(_lastData));

            _hasNewDataSinceLastLog = false;
            _nextLogTime = Time.unscaledTime + logIntervalSec;
        }

        private void Handle(ArtNetData data)
        {
            if (onlyUniverse >= 0 && data.Universe != onlyUniverse) return;

            ch1 = GetChannelInt(data, monitorCh1);
            ch2 = GetChannelInt(data, monitorCh2);
            ch3 = GetChannelInt(data, monitorCh3);
            ch4 = GetChannelInt(data, monitorCh4);
            ch5 = GetChannelInt(data, monitorCh5);

            _lastData = data;
            _hasNewDataSinceLastLog = true;
        }

        private string BuildSummaryLog(ArtNetData data)
        {
            var sb = new StringBuilder(256);

            sb.Append("[DmxDebugMonitor] ");

            if (includeHeaderInLog)
            {
                int length = ((data.LengthHi & 0xFF) << 8) | (data.LengthLo & 0xFF);
                sb.Append($"Universe={data.Universe}, Length={length}, Seq={data.Sequence}, Phys={data.Physical} | ");
            }

            sb.Append($"ch{monitorCh1}={ch1}, ");
            sb.Append($"ch{monitorCh2}={ch2}, ");
            sb.Append($"ch{monitorCh3}={ch3}, ");
            sb.Append($"ch{monitorCh4}={ch4}, ");
            sb.Append($"ch{monitorCh5}={ch5}");

            return sb.ToString();
        }

        private static int GetChannelInt(ArtNetData data, int channel1to512)
        {
            if (channel1to512 < 1 || channel1to512 > 512) return 0;

            int idx = channel1to512 - 1;
            if (data.Channels == null || data.Channels.Length <= idx) return 0;

            int v = data.Channels[idx];
            if (v < 0) v = 0;
            if (v > 255) v = 255;
            return v;
        }
    }
}
