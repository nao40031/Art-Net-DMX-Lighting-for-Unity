using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArtNet.Runtime
{
    // ⚠️ 重要：
    // - 既存アセット互換のため「数値を固定」しています
    // - 今後は “数値を変更しない” でください（追加は末尾に新しい番号で）
    public enum FixtureFunction
    {
        // --- intensity / color ---
        Dimmer = 0,
        Red = 1,
        Green = 2,
        Blue = 3,

        // ✅ WhiteはBlueの直後に表示したい（ただし値は互換のまま固定）
        White = 14,

        // --- pan/tilt (8bit or 16bit by coarse+fine) ---
        PanCoarse = 4,
        PanFine = 5,
        TiltCoarse = 6,
        TiltFine = 7,

        // --- optional / expand as needed ---
        Strobe = 8,
        ColorWheel = 9,
        Gobo = 10,
        Prism = 11,
        Focus = 12,
        Zoom = 13,

        // --- misc ---
        PanTiltSpeed = 15,
        Reset = 16
    }

    [Serializable]
    public class FunctionChannel
    {
        public FixtureFunction function;

        [Tooltip("DMX channel (1-512)")]
        [Range(1, 512)]
        public int channel = 1;
    }

    [Serializable]
    public class FixtureModeDefinition
    {
        [Tooltip("表示用のモード名（例: Mode 3 / Extended 16bit）")]
        public string modeName = "Mode 1";

        [Tooltip("このModeが消費するチャンネル数（確認用）")]
        [Range(1, 512)]
        public int channelCount = 1;

        [Tooltip("Function -> channel 割当")]
        public List<FunctionChannel> channels = new();
    }

    [CreateAssetMenu(menuName = "ArtNet/DMX/Fixture Definition", fileName = "FixtureDefinition")]
    public class FixtureDefinition : ScriptableObject
    {
        [Header("Identity (for humans/logs)")]
        public string manufacturer;
        public string model;
        public string revision;
        public string displayName;

        [Header("Modes")]
        public List<FixtureModeDefinition> modes = new();

        public string GetDisplayLabel()
        {
            string name = string.IsNullOrWhiteSpace(displayName)
                ? $"{manufacturer} {model}".Trim()
                : displayName.Trim();

            if (!string.IsNullOrWhiteSpace(revision))
                name += $" (Rev {revision.Trim()})";

            return name;
        }
    }
}
