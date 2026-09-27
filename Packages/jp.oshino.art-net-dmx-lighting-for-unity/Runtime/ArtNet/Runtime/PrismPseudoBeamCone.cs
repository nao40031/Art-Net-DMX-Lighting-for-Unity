using UnityEngine;

namespace ArtNet.Runtime
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("ArtNet/Pseudo Beam Cone")]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class PrismPseudoBeamCone : MonoBehaviour
    {
        private const string GeneratedMeshName = "PrismPseudoBeamCone_Generated";

        [Header("Shape")]
        [SerializeField, Min(0.01f)] private float length = 10f;
        [SerializeField, Min(0f)] private float startRadius = 0.05f;
        [SerializeField, Min(0.001f)] private float endRadius = 1.5f;
        [SerializeField, Range(3, 128)] private int segments = 32;
        [SerializeField] private bool capStart;
        [SerializeField] private bool capEnd;

        [Header("Generation")]
        [SerializeField] private bool generateOnAwake = true;
        [SerializeField] private bool rebuildInEditMode = true;
        [SerializeField] private bool assignMaterialOnRebuild = true;
        [SerializeField] private Material material;

        private Mesh _generatedMesh;
        private bool _rebuildQueued;

        public float Length
        {
            get => length;
            set
            {
                length = Mathf.Max(0.01f, value);
                RebuildBeamMesh();
            }
        }

        public float StartRadius
        {
            get => startRadius;
            set
            {
                startRadius = Mathf.Max(0f, value);
                RebuildBeamMesh();
            }
        }

        public float EndRadius
        {
            get => endRadius;
            set
            {
                endRadius = Mathf.Max(0.001f, value);
                RebuildBeamMesh();
            }
        }

        public int Segments
        {
            get => segments;
            set
            {
                segments = Mathf.Clamp(value, 3, 128);
                RebuildBeamMesh();
            }
        }

        public MeshRenderer MeshRenderer => GetComponent<MeshRenderer>();

        public void SetRuntimeShape(float runtimeLength, float runtimeStartRadius, float runtimeEndRadius)
        {
            length = Mathf.Max(0.01f, runtimeLength);
            startRadius = Mathf.Max(0f, runtimeStartRadius);
            endRadius = Mathf.Max(0.001f, runtimeEndRadius);

            if (!UpdateExistingMeshGeometry())
                RebuildBeamMesh();
        }

        public void SetRuntimeEndRadius(float runtimeEndRadius)
        {
            SetRuntimeShape(length, startRadius, runtimeEndRadius);
        }

        private void Reset()
        {
            length = 10f;
            startRadius = 0.05f;
            endRadius = 1.5f;
            segments = 32;
            capStart = false;
            capEnd = false;
            generateOnAwake = true;
            rebuildInEditMode = true;
            assignMaterialOnRebuild = true;
            RebuildBeamMesh();
        }

        private void Awake()
        {
            if (generateOnAwake)
                RebuildBeamMesh();
        }

        private void OnEnable()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == null && generateOnAwake)
                RebuildBeamMesh();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                return;

            DisposeGeneratedMesh();
        }

        private void OnDestroy()
        {
            DisposeGeneratedMesh();
        }

        private void OnValidate()
        {
            length = Mathf.Max(0.01f, length);
            startRadius = Mathf.Max(0f, startRadius);
            endRadius = Mathf.Max(0.001f, endRadius);
            segments = Mathf.Clamp(segments, 3, 128);

            if (!Application.isPlaying && rebuildInEditMode)
                QueueEditModeRebuild();
        }

        private void Update()
        {
            if (!_rebuildQueued)
                return;

            _rebuildQueued = false;
            RebuildBeamMesh();
        }

        private void QueueEditModeRebuild()
        {
            _rebuildQueued = true;
        }

        [ContextMenu("Rebuild Beam Mesh")]
        public void RebuildBeamMesh()
        {
            var filter = GetComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();
            if (filter == null || renderer == null)
                return;

            DisposeGeneratedMesh();

            _generatedMesh = BuildConeMesh();
            _generatedMesh.hideFlags = HideFlags.DontSave;
            filter.sharedMesh = _generatedMesh;

            if (assignMaterialOnRebuild && material != null)
                renderer.sharedMaterial = material;
        }

        private Mesh BuildConeMesh()
        {
            int sideVertexCount = (segments + 1) * 2;
            int capVertexCount = (capStart ? segments + 2 : 0) + (capEnd ? segments + 2 : 0);
            int vertexCount = sideVertexCount + capVertexCount;
            int sideIndexCount = segments * 6;
            int capIndexCount = (capStart ? segments * 3 : 0) + (capEnd ? segments * 3 : 0);

            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var triangles = new int[sideIndexCount + capIndexCount];

            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float angle = u * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);

                int startIndex = i * 2;
                int endIndex = startIndex + 1;
                vertices[startIndex] = new Vector3(x * startRadius, y * startRadius, 0f);
                vertices[endIndex] = new Vector3(x * endRadius, y * endRadius, length);

                Vector3 normal = new Vector3(x, y, 0f).normalized;
                normals[startIndex] = normal;
                normals[endIndex] = normal;
                uvs[startIndex] = new Vector2(u, 0f);
                uvs[endIndex] = new Vector2(u, 1f);
            }

            int tri = 0;
            for (int i = 0; i < segments; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;

                triangles[tri++] = a;
                triangles[tri++] = b;
                triangles[tri++] = c;
                triangles[tri++] = c;
                triangles[tri++] = b;
                triangles[tri++] = d;
            }

            int vertex = sideVertexCount;
            if (capStart)
            {
                int center = vertex++;
                vertices[center] = Vector3.zero;
                normals[center] = Vector3.back;
                uvs[center] = new Vector2(0.5f, 0.5f);

                int ringStart = vertex;
                for (int i = 0; i <= segments; i++)
                {
                    float u = (float)i / segments;
                    float angle = u * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle);
                    float y = Mathf.Sin(angle);
                    vertices[vertex] = new Vector3(x * startRadius, y * startRadius, 0f);
                    normals[vertex] = Vector3.back;
                    uvs[vertex] = new Vector2(x * 0.5f + 0.5f, y * 0.5f + 0.5f);
                    vertex++;
                }

                for (int i = 0; i < segments; i++)
                {
                    triangles[tri++] = center;
                    triangles[tri++] = ringStart + i + 1;
                    triangles[tri++] = ringStart + i;
                }
            }

            if (capEnd)
            {
                int center = vertex++;
                vertices[center] = new Vector3(0f, 0f, length);
                normals[center] = Vector3.forward;
                uvs[center] = new Vector2(0.5f, 0.5f);

                int ringStart = vertex;
                for (int i = 0; i <= segments; i++)
                {
                    float u = (float)i / segments;
                    float angle = u * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle);
                    float y = Mathf.Sin(angle);
                    vertices[vertex] = new Vector3(x * endRadius, y * endRadius, length);
                    normals[vertex] = Vector3.forward;
                    uvs[vertex] = new Vector2(x * 0.5f + 0.5f, y * 0.5f + 0.5f);
                    vertex++;
                }

                for (int i = 0; i < segments; i++)
                {
                    triangles[tri++] = center;
                    triangles[tri++] = ringStart + i;
                    triangles[tri++] = ringStart + i + 1;
                }
            }

            var mesh = new Mesh
            {
                name = GeneratedMeshName
            };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private bool UpdateExistingMeshGeometry()
        {
            var filter = GetComponent<MeshFilter>();
            var mesh = _generatedMesh != null ? _generatedMesh : filter != null ? filter.sharedMesh : null;
            if (mesh == null)
                return false;

            int sideVertexCount = (segments + 1) * 2;
            int capVertexCount = (capStart ? segments + 2 : 0) + (capEnd ? segments + 2 : 0);
            int vertexCount = sideVertexCount + capVertexCount;
            if (mesh.vertexCount != vertexCount)
                return false;

            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (normals == null || normals.Length != vertices.Length)
                normals = new Vector3[vertices.Length];

            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float angle = u * Mathf.PI * 2f;
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);

                int startIndex = i * 2;
                int endIndex = startIndex + 1;
                vertices[startIndex] = new Vector3(x * startRadius, y * startRadius, 0f);
                vertices[endIndex] = new Vector3(x * endRadius, y * endRadius, length);

                Vector3 normal = new Vector3(x, y, 0f).normalized;
                normals[startIndex] = normal;
                normals[endIndex] = normal;
            }

            int vertex = sideVertexCount;
            if (capStart)
            {
                int center = vertex++;
                vertices[center] = Vector3.zero;
                normals[center] = Vector3.back;

                for (int i = 0; i <= segments; i++)
                {
                    float u = (float)i / segments;
                    float angle = u * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle);
                    float y = Mathf.Sin(angle);
                    vertices[vertex] = new Vector3(x * startRadius, y * startRadius, 0f);
                    normals[vertex] = Vector3.back;
                    vertex++;
                }
            }

            if (capEnd)
            {
                int center = vertex++;
                vertices[center] = new Vector3(0f, 0f, length);
                normals[center] = Vector3.forward;

                for (int i = 0; i <= segments; i++)
                {
                    float u = (float)i / segments;
                    float angle = u * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle);
                    float y = Mathf.Sin(angle);
                    vertices[vertex] = new Vector3(x * endRadius, y * endRadius, length);
                    normals[vertex] = Vector3.forward;
                    vertex++;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            _generatedMesh = mesh;
            if (filter != null && filter.sharedMesh == null)
                filter.sharedMesh = mesh;
            return true;
        }

        private void DisposeGeneratedMesh()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == _generatedMesh)
                filter.sharedMesh = null;

            if (_generatedMesh == null)
                return;

            if (Application.isPlaying)
                Destroy(_generatedMesh);
            else
                DestroyImmediate(_generatedMesh);

            _generatedMesh = null;
        }
    }
}
