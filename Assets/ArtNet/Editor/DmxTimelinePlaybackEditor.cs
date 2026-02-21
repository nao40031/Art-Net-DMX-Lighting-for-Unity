using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    [CustomEditor(typeof(DmxTimelinePlayback))]
    [CanEditMultipleObjects]
    public class DmxTimelinePlaybackEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Quick Mode Presets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Live / Record: disable playback and use LiveOnly.\n" +
                "Timeline Playback: enable playback and force PlaybackOnly.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Live / Record"))
                    ApplyLiveRecordPreset();

                if (GUILayout.Button("Apply Timeline Playback"))
                    ApplyTimelinePlaybackPreset();
            }
        }

        private void ApplyLiveRecordPreset()
        {
            const string undoName = "Apply Live/Record Preset";
            foreach (var obj in targets)
            {
                var playback = obj as DmxTimelinePlayback;
                if (playback == null) continue;

                Undo.RecordObject(playback, undoName);

                EnsureRigReference(playback, undoName);

                playback.enablePlayback = false;
                playback.overrideRigInputMode = false;
                playback.inputModeWhileEnabled = DmxRigController.InputMode.PlaybackOnly;

                if (playback.rig != null)
                    playback.rig.inputMode = DmxRigController.InputMode.LiveOnly;

                EditorUtility.SetDirty(playback);
                if (playback.rig != null)
                    EditorUtility.SetDirty(playback.rig);
            }
        }

        private void ApplyTimelinePlaybackPreset()
        {
            const string undoName = "Apply Timeline Playback Preset";
            foreach (var obj in targets)
            {
                var playback = obj as DmxTimelinePlayback;
                if (playback == null) continue;

                Undo.RecordObject(playback, undoName);

                EnsureRigReference(playback, undoName);

                playback.enablePlayback = true;
                playback.overrideRigInputMode = true;
                playback.inputModeWhileEnabled = DmxRigController.InputMode.PlaybackOnly;

                if (playback.rig != null)
                    playback.rig.inputMode = DmxRigController.InputMode.PlaybackOnly;

                EditorUtility.SetDirty(playback);
                if (playback.rig != null)
                    EditorUtility.SetDirty(playback.rig);
            }
        }

        private static void EnsureRigReference(DmxTimelinePlayback playback, string undoName)
        {
            if (playback.rig != null)
            {
                Undo.RecordObject(playback.rig, undoName);
                return;
            }

            var rig = playback.GetComponent<DmxRigController>();
            if (rig == null) return;

            playback.rig = rig;
            Undo.RecordObject(rig, undoName);
        }
    }
}
