using System.Collections.Generic;
using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    /// <summary>Reference mappings only; never replaces an existing fixture asset.</summary>
    public static class IrisExamples
    {
        [MenuItem("Tools/ArtNet/Iris/Create Reference Definitions")]
        public static void Create()
        {
            const string folder = "Assets/ArtNetIrisExamples";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets", "ArtNetIrisExamples");
            var profile = ScriptableObject.CreateInstance<IrisProfile>();
            AssetDatabase.CreateAsset(profile, AssetDatabase.GenerateUniqueAssetPath(folder + "/IrisProfile.asset"));
            var generic = ScriptableObject.CreateInstance<FixtureDefinition>();
            generic.displayName = "Generic Iris - isolated test channels";
            generic.irisProfile = profile;
            generic.modes.Add(new FixtureModeDefinition { modeName = "8-bit: CH1 Iris", channelCount = 1,
                elements = new List<FixtureChannelElement> { new FixtureChannelElement { attribute = FixtureAttribute.Iris } } });
            generic.modes.Add(new FixtureModeDefinition { modeName = "16-bit: CH1 coarse / CH2 fine", channelCount = 2,
                elements = new List<FixtureChannelElement> {
                    new FixtureChannelElement { attribute = FixtureAttribute.Iris, byteRole = FixtureByteRole.Coarse },
                    new FixtureChannelElement { attribute = FixtureAttribute.Iris, byteRole = FixtureByteRole.Fine } } });
            AssetDatabase.CreateAsset(generic, AssetDatabase.GenerateUniqueAssetPath(folder + "/GenericIris.asset"));
            var mac = ScriptableObject.CreateInstance<FixtureDefinition>();
            mac.displayName = "MAC Ultra Performance - Iris channels ONLY";
            mac.irisProfile = profile;
            mac.modes.Add(CreateMacMode(false));
            mac.modes.Add(CreateMacMode(true));
            AssetDatabase.CreateAsset(mac, AssetDatabase.GenerateUniqueAssetPath(folder + "/MacUltraIrisReference.asset"));
            AssetDatabase.SaveAssets();
            Selection.activeObject = mac;
            Debug.Log("Iris reference assets created. MAC definitions contain only Iris, not a complete fixture. Copy the Iris elements into your fixture. Minimum diameter, Hz and waveform are tunable approximations, not measured MAC specifications.");
        }

        internal static FixtureModeDefinition CreateMacMode(bool extended)
        {
            var mode = new FixtureModeDefinition { modeName = extended ? "Extended Iris reference" : "Basic Iris reference", channelCount = extended ? 25 : 24 };
            for (int i = 0; i < mode.channelCount; i++) mode.elements.Add(new FixtureChannelElement());
            mode.elements[23] = new FixtureChannelElement { attribute = FixtureAttribute.Iris,
                byteRole = extended ? FixtureByteRole.Coarse : FixtureByteRole.Single,
                ranges = new List<FixtureChannelRange> {
                    Range("Open to minimum", 0, extended ? 51400 : 200, FixtureRangeType.Indexed, true),
                    Range("Pulse fast to slow", extended ? 51401 : 201, extended ? 57825 : 225, FixtureRangeType.Pulse, true),
                    Range("Hold", extended ? 57826 : 226, extended ? 59110 : 230, FixtureRangeType.IrisHold, false),
                    Range("Reverse pulse slow to fast", extended ? 59111 : 231, extended ? 65535 : 255, FixtureRangeType.IrisPulseReverse, false) } };
            if (extended) mode.elements[24] = new FixtureChannelElement { attribute = FixtureAttribute.Iris, byteRole = FixtureByteRole.Fine };
            return mode;
        }

        private static FixtureChannelRange Range(string name, int min, int max, FixtureRangeType type, bool inverted)
            => new FixtureChannelRange { name = name, dmxMin = min, dmxMax = max, type = type,
                mappingContext = NormalizedMappingContext.Iris,
                mappingPreset = inverted ? NormalizedMappingPreset.Inverted : NormalizedMappingPreset.Normal };
    }
}
