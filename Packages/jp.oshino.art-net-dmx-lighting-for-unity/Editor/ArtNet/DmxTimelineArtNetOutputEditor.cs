using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    [CustomEditor(typeof(DmxTimelineArtNetOutput))]
    public sealed class DmxTimelineArtNetOutputEditor : UnityEditor.Editor
    {
        private static readonly string[] RigChoices = { "Assigned", "All Active" };
        private static readonly string[] UniverseChoices = { "Specified", "All" };
        private SerializedProperty _enableOutput;
        private SerializedProperty _sender;
        private SerializedProperty _sendInEditMode;
        private SerializedProperty _director;
        private SerializedProperty _rigSelection;
        private SerializedProperty _rigs;
        private SerializedProperty _universeSelection;
        private SerializedProperty _universes;
        private SerializedProperty _maxSendRate;
        private SerializedProperty _resendInterval;

        private void OnEnable()
        {
            _enableOutput = serializedObject.FindProperty("enableOutput");
            _sender = serializedObject.FindProperty("sender");
            _sendInEditMode = serializedObject.FindProperty("sendInEditMode");
            _director = serializedObject.FindProperty("director");
            _rigSelection = serializedObject.FindProperty("rigSelection");
            _rigs = serializedObject.FindProperty("rigs");
            _universeSelection = serializedObject.FindProperty("universeSelection");
            _universes = serializedObject.FindProperty("universes");
            _maxSendRate = serializedObject.FindProperty("maxSendRate");
            _resendInterval = serializedObject.FindProperty("resendInterval");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Timeline → Art-Net", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_enableOutput, new GUIContent("Enable Output"));
            EditorGUILayout.PropertyField(_sender, new GUIContent("Sender"));
            EditorGUILayout.PropertyField(_sendInEditMode, new GUIContent("Send In Edit Mode"));
            EditorGUILayout.PropertyField(_director, new GUIContent("Playable Director (Optional)"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Rig Selection", EditorStyles.boldLabel);
            _rigSelection.enumValueIndex = EditorGUILayout.Popup("Selection", _rigSelection.enumValueIndex, RigChoices);
            if ((DmxTimelineArtNetOutput.RigSelection)_rigSelection.enumValueIndex == DmxTimelineArtNetOutput.RigSelection.Assigned)
                EditorGUILayout.PropertyField(_rigs, new GUIContent("Rigs"), true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Universe", EditorStyles.boldLabel);
            _universeSelection.enumValueIndex = EditorGUILayout.Popup("Selection", _universeSelection.enumValueIndex, UniverseChoices);
            if ((DmxTimelineArtNetOutput.UniverseSelection)_universeSelection.enumValueIndex == DmxTimelineArtNetOutput.UniverseSelection.Specified)
                EditorGUILayout.PropertyField(_universes, new GUIContent("Specified Universes"), true);
            EditorGUILayout.PropertyField(_maxSendRate, new GUIContent("Max Send Rate"));
            EditorGUILayout.PropertyField(_resendInterval, new GUIContent("Resend Interval (sec)"));

            bool changed = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            var output = (DmxTimelineArtNetOutput)target;
            if (changed) output.RefreshRigs();
            EditorGUILayout.Space();
            if (_sendInEditMode.boolValue)
                EditorGUILayout.HelpBox("Art-Net is transmitted in Edit Mode. Check connected fixtures before enabling output.", MessageType.Warning);
            EditorGUILayout.HelpBox("For duplicate Universes, the first rig wins. All Active uses Instance ID order. Live Input and External Injection are not forwarded.", MessageType.Info);

            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active Rigs", output.ActiveRigCount.ToString());
            EditorGUILayout.LabelField("Packets Sent", output.PacketsSent.ToString());
            EditorGUILayout.LabelField("Last Universe", output.LastUniverse < 0 ? "—" : output.LastUniverse.ToString());
            if (!string.IsNullOrEmpty(output.LastError))
                EditorGUILayout.HelpBox(output.LastError, MessageType.Warning);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Rigs")) output.RefreshRigs();
                using (new EditorGUI.DisabledGroupScope(!output.OutputEnabled || (!Application.isPlaying && !_sendInEditMode.boolValue)))
                {
                    if (GUILayout.Button("Send Current Frame Once")) output.SendCurrentFrameOnce();
                }
            }
        }
    }
}
