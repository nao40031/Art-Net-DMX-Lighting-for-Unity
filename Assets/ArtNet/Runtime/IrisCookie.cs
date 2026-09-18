using UnityEngine;

namespace ArtNet.Runtime
{
    /// <summary>One cached output per light, preserving that light's existing cookie.</summary>
    internal sealed class IrisCookie : System.IDisposable
    {
        private Material material;
        private RenderTexture output;
        private Texture lastSource;
        private uint lastUpdate;
        private Vector4 lastShape;
        private bool valid;
        public Texture Source { get; private set; }
        public Texture Output => output;

        public Texture Apply(Texture source, Vector4 shape, int resolution)
        {
            if (source == output) source = Source;
            Source = source;
            if (shape.x >= 0.99999f) return source;
            if (material == null)
            {
                var shader = Resources.Load<Shader>("ArtNet/IrisCookie");
                if (shader == null || !shader.isSupported) return source;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            int size = Mathf.Clamp(Mathf.NextPowerOfTwo(resolution), 64, 1024);
            if (output == null || output.width != size)
            {
                ReleaseOutput();
                output = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { name = "ArtNet Iris Cookie", hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, useMipMap = false };
                output.Create();
                valid = false;
            }
            uint update = source != null ? source.updateCount : 0;
            if (!valid || lastSource != source || lastUpdate != update || lastShape != shape)
            {
                material.SetVector("_IrisShape", shape);
                var previous = RenderTexture.active;
                try { Graphics.Blit(source != null ? source : Texture2D.whiteTexture, output, material); }
                finally { RenderTexture.active = previous; }
                output.IncrementUpdateCount();
                lastSource = source; lastUpdate = update; lastShape = shape; valid = true;
            }
            return output;
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }
        private void ReleaseOutput() { if (output != null) { output.Release(); Release(output); output = null; } }
        public void Dispose() { ReleaseOutput(); Release(material); material = null; valid = false; }
    }
}
