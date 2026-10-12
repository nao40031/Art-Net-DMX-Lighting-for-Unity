using UnityEngine;
using UnityEngine.Rendering;

namespace ArtNet.Runtime
{
    public partial class DmxFixtureComponent
    {
        [Header("Lens Halo / レンズ周辺光")]
        [Tooltip("ポストプロセスを使わず、レンズ前面に柔らかな周辺光を表示します。\n\nDisplays a soft glow in front of the lens without camera post-processing.")]
        [SerializeField] private bool enableLensHalo;

        [Tooltip("周辺光の位置と大きさの基準となるレンズのMesh Rendererです。\n\nThe lens Mesh Renderer used to position and size the halo.")]
        [SerializeField] private Renderer lensHaloSourceRenderer;

        [Tooltip("現在の描画パイプラインに対応する周辺光マテリアルです。\n\nThe halo material for the active render pipeline.")]
        [SerializeField] private Material lensHaloMaterial;

        [Tooltip("レンズ半径に対する周辺光の広がりです。\n\nHalo spread relative to the lens radius.")]
        [SerializeField, Range(1f, 2.5f)] private float lensHaloSize = 1.55f;

        private GameObject _lensHaloObject;
        private Mesh _lensHaloMesh;
        private MeshRenderer _lensHaloRenderer;

        private void CreateLensHalo()
        {
            if (!enableLensHalo || lensHaloSourceRenderer == null || lensHaloMaterial == null || _lensHaloObject != null)
                return;

            MeshFilter sourceFilter = lensHaloSourceRenderer.GetComponent<MeshFilter>();
            Mesh sourceMesh = sourceFilter != null ? sourceFilter.sharedMesh : null;
            if (sourceMesh == null)
                return;

            Bounds bounds = sourceMesh.bounds;
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            if (radius <= 0f)
                return;

            Vector3[] normals = sourceMesh.normals;
            Vector3 faceNormal = normals != null && normals.Length > 0 && normals[0].sqrMagnitude > 0.001f
                ? normals[0].normalized
                : Vector3.forward;

            _lensHaloObject = new GameObject("Lens Halo (Runtime)");
            Transform haloTransform = _lensHaloObject.transform;
            haloTransform.SetParent(lensHaloSourceRenderer.transform, false);
            haloTransform.localPosition = bounds.center + faceNormal * (radius * 0.02f);
            haloTransform.localRotation = Quaternion.FromToRotation(Vector3.forward, faceNormal);
            float diameter = 2f * radius * Mathf.Clamp(lensHaloSize, 1f, 2.5f);
            haloTransform.localScale = new Vector3(diameter, diameter, 1f);

            _lensHaloMesh = new Mesh { name = "Lens Halo Quad" };
            _lensHaloMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
            };
            _lensHaloMesh.uv = new[]
            {
                Vector2.zero, Vector2.right, Vector2.up, Vector2.one
            };
            _lensHaloMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            _lensHaloMesh.RecalculateNormals();
            _lensHaloMesh.RecalculateTangents();
            _lensHaloObject.AddComponent<MeshFilter>().sharedMesh = _lensHaloMesh;
            _lensHaloRenderer = _lensHaloObject.AddComponent<MeshRenderer>();
            _lensHaloRenderer.sharedMaterial = lensHaloMaterial;
            _lensHaloRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _lensHaloRenderer.receiveShadows = false;
            _lensHaloRenderer.lightProbeUsage = LightProbeUsage.Off;
            _lensHaloRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void ApplyLensHaloDmx(MaterialPropertyBlock properties)
        {
            if (_lensHaloRenderer != null)
                _lensHaloRenderer.SetPropertyBlock(properties);
        }

        private void ReleaseLensHalo()
        {
            if (_lensHaloObject != null)
                Destroy(_lensHaloObject);
            if (_lensHaloMesh != null)
                Destroy(_lensHaloMesh);
            _lensHaloObject = null;
            _lensHaloMesh = null;
            _lensHaloRenderer = null;
        }
    }
}
