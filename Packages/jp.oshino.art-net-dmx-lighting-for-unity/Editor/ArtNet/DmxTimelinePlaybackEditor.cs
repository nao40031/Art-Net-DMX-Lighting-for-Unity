/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using ArtNet.Runtime;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
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
            EditorGUILayout.LabelField("Universe Source Discovery", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Scans active ArtNetChannels components under Universe Root and replaces Sources only when every Universe number is valid and unique.",
                MessageType.Info);

            if (GUILayout.Button("Auto Discover in Children"))
                DiscoverSourcesInChildren();

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

        private void DiscoverSourcesInChildren()
        {
            const string undoName = "Auto Discover Timeline Universe Sources";
            var messages = new List<string>();
            bool allSucceeded = true;

            foreach (var obj in targets)
            {
                var playback = obj as DmxTimelinePlayback;
                if (playback == null) continue;

                Undo.RecordObject(playback, undoName);
                var result = playback.DiscoverSourcesInChildren();
                allSucceeded &= result.succeeded;

                if (result.succeeded)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(playback))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(playback);

                    EditorUtility.SetDirty(playback);
                    var scene = playback.gameObject.scene;
                    if (scene.IsValid() && scene.isLoaded && !Application.isPlaying)
                        EditorSceneManager.MarkSceneDirty(scene);
                }

                messages.Add($"{playback.gameObject.name}\n{result.BuildMessage()}");
            }

            serializedObject.Update();
            EditorUtility.DisplayDialog(
                allSucceeded ? "ArtNet Universe Sources Registered" : "ArtNet Universe Source Discovery",
                string.Join("\n\n", messages),
                "OK");
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
