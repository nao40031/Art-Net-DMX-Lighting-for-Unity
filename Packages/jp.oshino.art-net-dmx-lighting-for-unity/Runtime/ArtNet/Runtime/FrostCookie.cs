using UnityEngine;

namespace ArtNet.Runtime
{
    /// <summary>One cached diffused output per light, preserving that light's existing cookie.</summary>
    internal sealed class FrostCookie : System.IDisposable
    {
        private Material material;
        private RenderTexture output;
        private Texture lastSource;
        private uint lastUpdate;
        private float lastBlur = -1f;
        private bool valid;
        public Texture Source { get; private set; }
        public Texture Output => output;

        public Texture Apply(Texture source, float blur, int resolution)
        {
            if (source == output) source = Source;
            Source = source;
            blur = Mathf.Max(0f, blur);
            if (source == null || blur <= 0.000001f) return source;

            if (material == null)
            {
                var shader = Resources.Load<Shader>("ArtNet/FrostCookie");
                if (shader == null || !shader.isSupported) return source;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            int size = Mathf.Clamp(Mathf.NextPowerOfTwo(resolution), 64, 1024);
            if (output == null || output.width != size)
            {
                ReleaseOutput();
                output = new RenderTexture(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    name = "ArtNet Frost Cookie",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    useMipMap = false
                };
                output.Create();
                valid = false;
            }

            uint update = source.updateCount;
            if (!valid || lastSource != source || lastUpdate != update || !Mathf.Approximately(lastBlur, blur))
            {
                material.SetFloat("_Blur", blur);
                var previous = RenderTexture.active;
                try { Graphics.Blit(source, output, material); }
                finally { RenderTexture.active = previous; }
                output.IncrementUpdateCount();
                lastSource = source;
                lastUpdate = update;
                lastBlur = blur;
                valid = true;
            }
            return output;
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
        }

        private void ReleaseOutput()
        {
            if (output == null) return;
            output.Release();
            Release(output);
            output = null;
        }

        public void Dispose()
        {
            ReleaseOutput();
            Release(material);
            material = null;
            valid = false;
        }
    }
}
