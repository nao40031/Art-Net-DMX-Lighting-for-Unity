/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    /// <summary>
    /// Materializes editable copies of the package's fixture content in Assets without
    /// changing the package installation source or user-created assets.
    /// </summary>
    internal sealed class EditableAssetLibraryWindow : EditorWindow
    {
        private const string PackageRoot = "Packages/jp.oshino.art-net-dmx-lighting-for-unity";
        private const string RuntimeRoot = PackageRoot + "/Runtime";
        private const string TargetRoot = "Assets/Art-Net DMX Lighting/Editable Assets";
        private const string ManifestPath = TargetRoot + "/EditableAssetLibraryManifest.asset";
        private const string StagingRoot = TargetRoot + "/.ArtNetStaging";
        private const string MenuPath = "Art-Net/Assets/Editable Asset Library...";

        private readonly List<Candidate> _candidates = new();
        private readonly HashSet<string> _selectedPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _editableDependencies = new(StringComparer.OrdinalIgnoreCase);
        private Vector2 _scrollPosition;
        private bool _includeDefinitions = true;
        private bool _includeUrpPrefabs = true;
        private bool _includeUrpVlbPrefabs = true;
        private bool _includeHdrpPrefabs = true;
        private bool _includeHdrpVlbPrefabs = true;
        private OverwriteMode _overwriteMode = OverwriteMode.SkipExisting;
        private EditableAssetLibraryManifest _manifest;

        private enum OverwriteMode
        {
            SkipExisting,
            ChooseAssets,
            ReplaceAllManaged
        }

        private enum CandidateStatus
        {
            New,
            UpToDate,
            PackageChanged,
            UserEdited,
            PackageAndUserChanged,
            MissingTarget,
            ProtectedConflict
        }

        private sealed class Candidate
        {
            public string sourcePath;
            public string targetPath;
            public string category;
            public CandidateStatus status;
        }

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<EditableAssetLibraryWindow>(true, "Art-Net Editable Asset Library", true);
            window.minSize = new Vector2(680f, 520f);
            window.RefreshCandidates();
            window.Show();
        }

        private void OnEnable() => RefreshCandidates();

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.LabelField("Art-Net Editable Asset Library", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "配布パッケージの定義・Prefab・必要な依存アセットをAssets配下へ複製します。作成済みの編集用アセットだけを管理するため、ユーザーが独自に作成または複製したアセットは上書き・削除しません。\n\n" +
                "Copies package definitions, prefabs, and required editable dependencies into Assets. Only assets created by this tool are managed, so user-created or duplicated assets are never overwritten or deleted.",
                MessageType.Info);

            DrawSourceOptions();
            EditorGUILayout.Space(8f);
            DrawOverwriteOptions();
            EditorGUILayout.Space(8f);
            DrawCandidateSummary();
            EditorGUILayout.Space(8f);
            DrawActions();
            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceOptions()
        {
            EditorGUILayout.LabelField("Export Contents", EditorStyles.boldLabel);
            _includeDefinitions = EditorGUILayout.ToggleLeft("Fixture / Gobo / Prism Definitions", _includeDefinitions);
            _includeUrpPrefabs = EditorGUILayout.ToggleLeft("URP Prefabs", _includeUrpPrefabs);
            _includeUrpVlbPrefabs = EditorGUILayout.ToggleLeft("URP VLB Prefabs", _includeUrpVlbPrefabs);
            _includeHdrpPrefabs = EditorGUILayout.ToggleLeft("HDRP Prefabs", _includeHdrpPrefabs);
            _includeHdrpVlbPrefabs = EditorGUILayout.ToggleLeft("HDRP VLB Prefabs", _includeHdrpVlbPrefabs);
            EditorGUILayout.HelpBox(
                "Prefabを選ぶと、参照されているDefinition、テクスチャ、マテリアルなども編集用コピーに含めます。スクリプト、Shader、3Dモデルはパッケージ参照のままです。\n\n" +
                "Selecting prefabs also includes referenced definitions, textures, and materials. Scripts, shaders, and 3D models remain package references.",
                MessageType.None);
        }

        private void DrawOverwriteOptions()
        {
            EditorGUILayout.LabelField("Update Mode", EditorStyles.boldLabel);
            _overwriteMode = (OverwriteMode)EditorGUILayout.EnumPopup("Mode", _overwriteMode);
            EditorGUILayout.HelpBox(GetOverwriteModeDescription(), MessageType.None);
        }

        private string GetOverwriteModeDescription()
        {
            return _overwriteMode switch
            {
                OverwriteMode.ChooseAssets =>
                    "一覧でチェックしたアセットだけを更新します。\n\nOnly checked assets in the list will be updated.",
                OverwriteMode.ReplaceAllManaged =>
                    "現在選択している展開対象のうち、このツールが管理しているアセットをすべて更新します。未管理のユーザーアセットは対象外です。\n\nUpdates every tool-managed asset in the currently selected export contents. Unmanaged user assets are excluded.",
                _ =>
                    "既存の編集用アセットを維持し、未作成のアセットだけを追加します。\n\nKeeps existing editable assets and creates only missing assets."
            };
        }

        private void DrawCandidateSummary()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Asset List", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"{_candidates.Count} assets under {TargetRoot}");

            if (_candidates.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "展開対象を選択してRefreshを押してください。\n\nSelect export contents and click Refresh.",
                    MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            foreach (var candidate in _candidates)
            {
                EditorGUILayout.BeginHorizontal();
                if (_overwriteMode == OverwriteMode.ChooseAssets)
                {
                    bool selected = _selectedPaths.Contains(candidate.sourcePath);
                    bool next = EditorGUILayout.Toggle(selected, GUILayout.Width(18f));
                    if (next) _selectedPaths.Add(candidate.sourcePath);
                    else _selectedPaths.Remove(candidate.sourcePath);
                }
                else
                {
                    GUILayout.Space(22f);
                }

                EditorGUILayout.LabelField(candidate.category, GUILayout.Width(92f));
                EditorGUILayout.LabelField(GetStatusLabel(candidate.status), GUILayout.Width(140f));
                EditorGUILayout.LabelField(candidate.targetPath, EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("再検索\nRefresh", GUILayout.Height(36f)))
                RefreshCandidates();

            bool canApply = _candidates.Count > 0 && GetApplicableCandidates().Count > 0;
            using (new EditorGUI.DisabledScope(!canApply))
            {
                if (GUILayout.Button("編集用アセットを展開・更新\nExport / Update Editable Assets", GUILayout.Height(36f)))
                    Apply();
            }
            EditorGUILayout.EndHorizontal();

            int protectedCount = _candidates.Count(x => x.status == CandidateStatus.ProtectedConflict);
            if (protectedCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{protectedCount}件は管理記録のない同名アセットが出力先にあるため保護されています。移動または名前変更後に再実行してください。\n\n{protectedCount} asset(s) have unmanaged files at the target path and are protected. Move or rename them before retrying.",
                    MessageType.Warning);
            }
        }

        private void RefreshCandidates()
        {
            _manifest = LoadOrCreateManifest(false);
            _candidates.Clear();
            _editableDependencies.Clear();
            var sourcePaths = DiscoverSourcePaths();
            foreach (var sourcePath in sourcePaths)
            {
                string targetPath = GetTargetPath(sourcePath);
                var candidate = new Candidate
                {
                    sourcePath = sourcePath,
                    targetPath = targetPath,
                    category = GetCategory(sourcePath),
                    status = GetStatus(sourcePath, targetPath)
                };
                _candidates.Add(candidate);
                _editableDependencies[sourcePath] = new HashSet<string>(
                    AssetDatabase.GetDependencies(sourcePath, true)
                        .Where(path => !string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase))
                        .Where(IsEditablePackageAsset),
                    StringComparer.OrdinalIgnoreCase);
                if (candidate.status == CandidateStatus.New)
                    _selectedPaths.Add(sourcePath);
            }

            _candidates.Sort((left, right) => string.Compare(left.targetPath, right.targetPath, StringComparison.OrdinalIgnoreCase));
            Repaint();
        }

        private List<string> DiscoverSourcePaths()
        {
            var seeds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (_includeDefinitions)
            {
                AddAssetsOfType(seeds, typeof(FixtureDefinition), RuntimeRoot + "/ArtNet/FixtureDefinition");
                AddAssetsOfType(seeds, typeof(GoboWheelDefinition), RuntimeRoot + "/ArtNet/GoboWheelDefinition");
                AddAssetsOfType(seeds, typeof(PrismDefinition), RuntimeRoot + "/ArtNet/Prism Definition");
            }

            foreach (string prefabPath in FindPrefabPaths())
            {
                string normalized = Normalize(prefabPath);
                bool isVlb = normalized.IndexOf("/VLB/", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isUrp = normalized.IndexOf("/URP/", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isHdrp = normalized.IndexOf("/HDRP/", StringComparison.OrdinalIgnoreCase) >= 0;
                if ((isUrp && ((isVlb && _includeUrpVlbPrefabs) || (!isVlb && _includeUrpPrefabs))) ||
                    (isHdrp && ((isVlb && _includeHdrpVlbPrefabs) || (!isVlb && _includeHdrpPrefabs))))
                    seeds.Add(prefabPath);
            }

            var assets = new HashSet<string>(seeds, StringComparer.OrdinalIgnoreCase);
            foreach (string seed in seeds)
            {
                foreach (string dependency in AssetDatabase.GetDependencies(seed, true))
                {
                    if (IsEditablePackageAsset(dependency))
                        assets.Add(dependency);
                }
            }
            return assets.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void AddAssetsOfType(ISet<string> paths, Type type, string folder)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{type.Name}", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsEditablePackageAsset(path)) paths.Add(path);
            }
        }

        private static IEnumerable<string> FindPrefabPaths()
        {
            const string lightAssetRoot = RuntimeRoot + "/LightAsset";
            return AssetDatabase.FindAssets("t:Prefab", new[] { lightAssetRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsEditablePackageAsset);
        }

        private static bool IsEditablePackageAsset(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Normalize(path).StartsWith(RuntimeRoot + "/", StringComparison.OrdinalIgnoreCase))
                return false;

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension is ".cs" or ".asmdef" or ".shader" or ".hlsl" or ".cginc" or ".dll" or ".fbx" or ".obj")
                return false;

            return AssetDatabase.LoadMainAssetAtPath(path) is not MonoScript;
        }

        private static string GetTargetPath(string sourcePath)
        {
            string normalized = Normalize(sourcePath);
            string relative = normalized.Substring(PackageRoot.Length).TrimStart('/');
            return TargetRoot + "/" + relative;
        }

        private static string GetCategory(string sourcePath)
        {
            string path = Normalize(sourcePath);
            if (path.IndexOf("/FixtureDefinition/", StringComparison.OrdinalIgnoreCase) >= 0) return "Fixture Definition";
            if (path.IndexOf("/GoboWheelDefinition/", StringComparison.OrdinalIgnoreCase) >= 0) return "Gobo Definition";
            if (path.IndexOf("/Prism Definition/", StringComparison.OrdinalIgnoreCase) >= 0) return "Prism Definition";
            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return "Prefab";
            return "Dependency";
        }

        private CandidateStatus GetStatus(string sourcePath, string targetPath)
        {
            var record = _manifest != null ? _manifest.Find(sourcePath) : null;
            bool targetExists = File.Exists(ToFullPath(targetPath));
            if (record == null)
                return targetExists ? CandidateStatus.ProtectedConflict : CandidateStatus.New;
            if (!targetExists)
                return CandidateStatus.MissingTarget;
            if (!string.Equals(AssetDatabase.AssetPathToGUID(targetPath), record.targetGuid, StringComparison.OrdinalIgnoreCase))
                return CandidateStatus.ProtectedConflict;

            bool sourceChanged = !string.Equals(ComputeHash(sourcePath), record.sourceHash, StringComparison.Ordinal);
            bool targetChanged = !string.Equals(ComputeHash(targetPath), record.targetHash, StringComparison.Ordinal);
            if (sourceChanged && targetChanged) return CandidateStatus.PackageAndUserChanged;
            if (sourceChanged) return CandidateStatus.PackageChanged;
            if (targetChanged) return CandidateStatus.UserEdited;
            return CandidateStatus.UpToDate;
        }

        private static string GetStatusLabel(CandidateStatus status)
        {
            return status switch
            {
                CandidateStatus.New => "新規 / New",
                CandidateStatus.UpToDate => "最新 / Up to date",
                CandidateStatus.PackageChanged => "配布更新 / Package changed",
                CandidateStatus.UserEdited => "ユーザー編集済み / User edited",
                CandidateStatus.PackageAndUserChanged => "双方更新 / Both changed",
                CandidateStatus.MissingTarget => "コピー先なし / Missing target",
                _ => "保護中 / Protected"
            };
        }

        private List<Candidate> GetApplicableCandidates()
        {
            var explicitlySelected = _candidates.Where(candidate =>
            {
                if (candidate.status == CandidateStatus.ProtectedConflict)
                    return false;
                return _overwriteMode switch
                {
                    OverwriteMode.ChooseAssets => _selectedPaths.Contains(candidate.sourcePath),
                    OverwriteMode.ReplaceAllManaged => true,
                    _ => candidate.status is CandidateStatus.New or CandidateStatus.MissingTarget
                };
            }).ToList();

            var bySourcePath = _candidates.ToDictionary(candidate => candidate.sourcePath, StringComparer.OrdinalIgnoreCase);
            var applicable = new HashSet<Candidate>();
            foreach (var root in explicitlySelected)
            {
                var closure = GetEditableDependencyClosure(root.sourcePath, bySourcePath);
                // A copied prefab must not silently retain an unmanaged package reference.
                // If a required target path is protected, leave this root untouched until the
                // conflict is resolved by the user.
                if (closure.Any(candidate => candidate.status == CandidateStatus.ProtectedConflict))
                    continue;

                foreach (var candidate in closure)
                {
                    bool shouldUpdate = _overwriteMode == OverwriteMode.ReplaceAllManaged ||
                                        ReferenceEquals(candidate, root) ||
                                        candidate.status is CandidateStatus.New or CandidateStatus.MissingTarget;
                    if (shouldUpdate)
                        applicable.Add(candidate);
                }
            }
            return applicable.OrderBy(candidate => candidate.targetPath, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private IReadOnlyCollection<Candidate> GetEditableDependencyClosure(string sourcePath, IReadOnlyDictionary<string, Candidate> bySourcePath)
        {
            var closure = new HashSet<Candidate>();
            var pending = new Queue<string>();
            pending.Enqueue(sourcePath);
            while (pending.Count > 0)
            {
                string current = pending.Dequeue();
                if (!bySourcePath.TryGetValue(current, out var candidate) || !closure.Add(candidate))
                    continue;
                if (!_editableDependencies.TryGetValue(current, out var dependencies))
                    continue;
                foreach (string dependency in dependencies)
                    pending.Enqueue(dependency);
            }
            return closure;
        }

        private void Apply()
        {
            var candidates = GetApplicableCandidates();
            if (candidates.Count == 0) return;

            string message = $"対象: {candidates.Count}件\n更新方式: {GetStatusLabelForMode()}\n出力先: {TargetRoot}\n\n管理対象の既存アセットだけが上書き候補です。未管理の独自アセットは変更しません。\n\n" +
                             $"Assets: {candidates.Count}\nMode: {GetStatusLabelForMode()}\nTarget: {TargetRoot}\n\nOnly existing managed assets can be overwritten. Unmanaged custom assets will not be changed.";
            if (!EditorUtility.DisplayDialog("Art-Net Editable Asset Library", message, "実行 / Apply", "キャンセル / Cancel"))
                return;

            try
            {
                _manifest = LoadOrCreateManifest(true);
                EnsureFolder(TargetRoot);
                var errors = new List<string>();
                CopyCandidates(candidates, errors);
                RefreshCandidates();
                AssetDatabase.SaveAssets();
                string result = errors.Count == 0
                    ? $"{candidates.Count}件の編集用アセットを更新しました。\n\nUpdated {candidates.Count} editable asset(s)."
                    : $"完了しましたが、{errors.Count}件で問題が発生しました。\n\nCompleted with {errors.Count} issue(s).\n\n" + string.Join("\n", errors.Take(8));
                EditorUtility.DisplayDialog("Art-Net Editable Asset Library", result, "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("Art-Net Editable Asset Library", $"編集用アセットの更新に失敗しました。\n\nEditable asset update failed.\n\n{exception.Message}", "OK");
                RefreshCandidates();
            }
        }

        private string GetStatusLabelForMode()
        {
            return _overwriteMode switch
            {
                OverwriteMode.ChooseAssets => "一部上書き / Choose assets",
                OverwriteMode.ReplaceAllManaged => "すべて上書き / Replace all managed",
                _ => "上書きしない / Skip existing"
            };
        }

        private void CopyCandidates(IReadOnlyList<Candidate> candidates, ICollection<string> errors)
        {
            var sourceToTargetGuid = BuildKnownGuidMap();
            var createdOrUpdated = new List<Candidate>();
            var overwrite = new List<Candidate>();

            foreach (var candidate in candidates)
            {
                var record = _manifest.Find(candidate.sourcePath);
                bool targetExists = File.Exists(ToFullPath(candidate.targetPath));
                if (targetExists && record != null)
                {
                    overwrite.Add(candidate);
                    continue;
                }

                EnsureFolder(Path.GetDirectoryName(candidate.targetPath)?.Replace('\\', '/'));
                if (!AssetDatabase.CopyAsset(candidate.sourcePath, candidate.targetPath))
                {
                    errors.Add($"Copy failed: {candidate.sourcePath}");
                    continue;
                }
                createdOrUpdated.Add(candidate);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var candidate in candidates)
            {
                string targetGuid = AssetDatabase.AssetPathToGUID(candidate.targetPath);
                string sourceGuid = AssetDatabase.AssetPathToGUID(candidate.sourcePath);
                if (!string.IsNullOrEmpty(sourceGuid) && !string.IsNullOrEmpty(targetGuid))
                    sourceToTargetGuid[sourceGuid] = targetGuid;
            }

            if (overwrite.Count > 0)
            {
                EnsureFolder(StagingRoot);
                for (int index = 0; index < overwrite.Count; index++)
                {
                    Candidate candidate = overwrite[index];
                    string stagingPath = StagingRoot + "/" + index + "_" + Path.GetFileName(candidate.targetPath);
                    if (File.Exists(ToFullPath(stagingPath)) || File.Exists(ToFullPath(stagingPath) + ".meta"))
                        AssetDatabase.DeleteAsset(stagingPath);
                    if (!AssetDatabase.CopyAsset(candidate.sourcePath, stagingPath))
                    {
                        errors.Add($"Stage copy failed: {candidate.sourcePath}");
                        continue;
                    }

                    AssetDatabase.ImportAsset(stagingPath, ImportAssetOptions.ForceSynchronousImport);
                    RemapTextReferences(stagingPath, sourceToTargetGuid);
                    try
                    {
                        // Replace only the asset content. The existing .meta remains in place
                        // so the target GUID, and therefore scene/prefab references, are stable.
                        File.Copy(ToFullPath(stagingPath), ToFullPath(candidate.targetPath), true);
                        if (!File.Exists(ToFullPath(candidate.targetPath)))
                            throw new IOException("The replacement target was not created.");
                        AssetDatabase.DeleteAsset(stagingPath);
                        createdOrUpdated.Add(candidate);
                    }
                    catch (Exception exception)
                    {
                        errors.Add($"Replace failed: {candidate.targetPath} ({exception.Message})");
                        AssetDatabase.DeleteAsset(stagingPath);
                    }
                }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            foreach (var candidate in createdOrUpdated.Distinct())
            {
                string targetGuid = AssetDatabase.AssetPathToGUID(candidate.targetPath);
                UpdateTargetMeta(candidate.sourcePath, candidate.targetPath, targetGuid);
                RemapTextReferences(candidate.targetPath, sourceToTargetGuid);
                AssetDatabase.ImportAsset(candidate.targetPath, ImportAssetOptions.ForceSynchronousImport);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var candidate in createdOrUpdated.Distinct())
            {
                string targetGuid = AssetDatabase.AssetPathToGUID(candidate.targetPath);
                if (string.IsNullOrEmpty(targetGuid))
                {
                    errors.Add($"Import failed: {candidate.targetPath}");
                    continue;
                }
                _manifest.Upsert(candidate.sourcePath, candidate.targetPath, AssetDatabase.AssetPathToGUID(candidate.sourcePath), targetGuid,
                    ComputeHash(candidate.sourcePath), ComputeHash(candidate.targetPath));
            }
            AssetDatabase.SaveAssets();
        }

        private Dictionary<string, string> BuildKnownGuidMap()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (_manifest == null) return map;
            foreach (var record in _manifest.records)
            {
                if (!string.IsNullOrEmpty(record.sourceGuid) && !string.IsNullOrEmpty(record.targetGuid) &&
                    File.Exists(ToFullPath(record.targetPath)) &&
                    string.Equals(AssetDatabase.AssetPathToGUID(record.targetPath), record.targetGuid, StringComparison.OrdinalIgnoreCase))
                    map[record.sourceGuid] = record.targetGuid;
            }
            return map;
        }

        private static void RemapTextReferences(string assetPath, IReadOnlyDictionary<string, string> sourceToTargetGuid)
        {
            string extension = Path.GetExtension(assetPath).ToLowerInvariant();
            if (extension is not ".prefab" and not ".asset" and not ".mat" and not ".controller" and not ".overridecontroller")
                return;

            string fullPath = ToFullPath(assetPath);
            if (!File.Exists(fullPath)) return;
            string original = File.ReadAllText(fullPath);
            string replaced = original;
            foreach (var pair in sourceToTargetGuid)
                replaced = replaced.Replace("guid: " + pair.Key, "guid: " + pair.Value);
            if (!string.Equals(original, replaced, StringComparison.Ordinal))
                File.WriteAllText(fullPath, replaced);
        }

        private static void UpdateTargetMeta(string sourcePath, string targetPath, string targetGuid)
        {
            if (string.IsNullOrEmpty(targetGuid))
                throw new InvalidOperationException($"No GUID was assigned to {targetPath}.");

            string sourceMetaPath = ToFullPath(sourcePath) + ".meta";
            string targetMetaPath = ToFullPath(targetPath) + ".meta";
            if (!File.Exists(sourceMetaPath) || !File.Exists(targetMetaPath))
                return;

            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            string sourceMeta = File.ReadAllText(sourceMetaPath);
            string updatedMeta = sourceMeta.Replace("guid: " + sourceGuid, "guid: " + targetGuid);
            File.WriteAllText(targetMetaPath, updatedMeta);
        }

        private static EditableAssetLibraryManifest LoadOrCreateManifest(bool create)
        {
            var manifest = AssetDatabase.LoadAssetAtPath<EditableAssetLibraryManifest>(ManifestPath);
            if (manifest != null || !create) return manifest;
            EnsureFolder(TargetRoot);
            manifest = CreateInstance<EditableAssetLibraryManifest>();
            AssetDatabase.CreateAsset(manifest, ManifestPath);
            AssetDatabase.SaveAssets();
            return manifest;
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path)) return;
            string normalized = Normalize(path);
            string[] segments = normalized.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segments[i]);
                current = next;
            }
        }

        private static string ComputeHash(string assetPath)
        {
            string fullPath = ToFullPath(assetPath);
            if (!File.Exists(fullPath)) return string.Empty;
            using var sha = SHA256.Create();
            using var stream = new MemoryStream();
            using (var assetStream = File.OpenRead(fullPath)) assetStream.CopyTo(stream);
            string metaPath = fullPath + ".meta";
            if (File.Exists(metaPath))
            {
                byte[] separator = { 0 };
                stream.Write(separator, 0, separator.Length);
                using var metaStream = File.OpenRead(metaPath);
                metaStream.CopyTo(stream);
            }
            stream.Position = 0;
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static string Normalize(string path) => path?.Replace('\\', '/');
        private static string ToFullPath(string assetPath) => Path.GetFullPath(assetPath);
    }

    internal sealed class EditableAssetLibraryManifest : ScriptableObject
    {
        [Serializable]
        internal sealed class Record
        {
            public string sourcePath;
            public string targetPath;
            public string sourceGuid;
            public string targetGuid;
            public string sourceHash;
            public string targetHash;
        }

        [SerializeField] internal List<Record> records = new();

        internal Record Find(string sourcePath)
        {
            return records.FirstOrDefault(record => string.Equals(record.sourcePath, sourcePath, StringComparison.OrdinalIgnoreCase));
        }

        internal void Upsert(string sourcePath, string targetPath, string sourceGuid, string targetGuid, string sourceHash, string targetHash)
        {
            var record = Find(sourcePath);
            if (record == null)
            {
                record = new Record();
                records.Add(record);
            }
            record.sourcePath = sourcePath;
            record.targetPath = targetPath;
            record.sourceGuid = sourceGuid;
            record.targetGuid = targetGuid;
            record.sourceHash = sourceHash;
            record.targetHash = targetHash;
            EditorUtility.SetDirty(this);
        }
    }
}
