using System;
using System.Linq;
using UnityEngine;

namespace Mewlist.MewNoiseGen
{
    public static class TextureGenerator
    {
        public static void Gen2D(Texture2D tex, NoiseProfile profile)
        {
            var pixels = tex.GetPixels();
            var pixelCount = pixels.Length;
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0,0,0,0);
            var min = 0f;
            var max = 1f;
            const float referenceSize = 256f;
            var tw = tex.width;
            var th = tex.height;
            
            profile.ForEach2D(new Vector2Int(tex.width, tex.height),
                (shape, x, y) =>
                {
                    var wx = x * referenceSize / tw;
                    var wy = y * referenceSize / th;
                    var wz = 1f;
                    var i = ((x % tw) + tw * (y % th)) % pixelCount;
                    if (i < 0) i = pixelCount - (-i) % pixelCount;

                    if (profile.Warp) profile.FastNoise.GradientPerturbFractal(ref wx, ref wy, ref wz);
                    var fade = shape.Density(new Vector3((float)x / tw, (float)y / th, 0));
                    var noise = profile.BaseValue + profile.FastNoise.GetNoise(wx, wy);
                    noise = fade * noise + pixels[i].r;
                    min = Mathf.Min(min, noise);
                    max = Mathf.Max(max, noise);
                    pixels[i] = new Color(noise, noise, noise, noise);
                });

            pixels = pixels.Select(x => (x - Color.white * min) / (max - min)).ToArray();
            tex.SetPixels(pixels);
            tex.Apply();
        }

        public static void Gen3D(Texture3D tex, NoiseProfile profile, Func<int, int, bool> progressionCallback)
        {
            var pixels = tex.GetPixels();
            var pixelCount = pixels.Length;
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = new Color(0,0,0,0);
            var min = 0f;
            var max = 1f;
            const float referenceSize = 256f;
            var tw = tex.width;
            var th = tex.height;
            var td = tex.depth;
            
            var valid = profile.ForEach3D(new Vector3Int(tex.width, tex.height, tex.depth),
                (shape, pos) =>
                {
                    var wx = pos.x * referenceSize / tw;
                    var wy = pos.y * referenceSize / th;
                    var wz = pos.z * referenceSize / td;
                    var ix = (pos.x % tw);
                    var iy = tw * (pos.y % th);
                    var iz = td * tw * (pos.z % td);
                    var i = (ix + iy + iz) % pixelCount;
                    if (i < 0) i = pixelCount - (-i) % pixelCount;

                    if (profile.Warp) profile.FastNoise.GradientPerturbFractal(ref wx, ref wy, ref wz);
                    var fade = shape.Density(new Vector3((float)pos.x / tw, (float)pos.y / th, (float)pos.z / td));
                    var noise = profile.BaseValue + profile.FastNoise.GetNoise(wx, wy, wz);
                    noise = fade * noise + pixels[i].r;
                    min = Mathf.Min(min, noise);
                    max = Mathf.Max(max, noise);
                    pixels[i] = new Color(noise, noise, noise, noise);
                },
                (i, total) =>
                {
                    return progressionCallback != null && progressionCallback(i, total);
                });

            if (!valid) return;
            pixels = pixels.Select(x => (x - Color.white * min) / (max - min)).ToArray();
            tex.SetPixels(pixels);
            tex.Apply();
        }
    }
}