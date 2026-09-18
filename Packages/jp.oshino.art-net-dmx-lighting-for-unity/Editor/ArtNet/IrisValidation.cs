using System;
using ArtNet.Runtime;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor
{
    /// <summary>Runs without a test-framework dependency. Also callable with -executeMethod.</summary>
    public static class IrisValidation
    {
        private static int checks;
        [MenuItem("Tools/ArtNet/Iris/Run Validation")]
        public static void Run()
        {
            checks = 0;
            var profile = ScriptableObject.CreateInstance<IrisProfile>();
            try
            {
                var controller = new IrisController();
                var generic = new FixtureChannelElement { attribute = FixtureAttribute.Iris };
                Input(controller, profile, generic, 0, 255); Equal(controller.Aperture, 0, "generic closed");
                Input(controller, profile, generic, 255, 255); Equal(controller.Aperture, 1, "generic open");
                Input(controller, profile, generic, 32768, 65535); Equal(controller.Aperture, 32768f / 65535, "16-bit value");
                Input(controller, profile, generic, 32769, 65535); Equal(controller.Aperture, 32769f / 65535, "fine-byte precision");
                profile.invertUnmappedPosition = true;
                Input(controller, profile, generic, 0, 255); Equal(controller.Aperture, 1, "inverted generic");
                profile.invertUnmappedPosition = false;
                foreach (bool extended in new[] { false, true })
                {
                    var element = IrisExamples.CreateMacMode(extended).elements[23];
                    int max = extended ? 65535 : 255;
                    Input(controller, profile, element, 0, max); Equal(controller.Aperture, 1, "MAC open");
                    Input(controller, profile, element, extended ? 51400 : 200, max); Equal(controller.Aperture, 0, "MAC minimum");
                    Equal(controller.Diameter(profile), profile.minimumDiameter, "minimum diameter");
                    Input(controller, profile, element, extended ? 51401 : 201, max);
                    Check(controller.Mode == IrisController.Motion.Pulse, "pulse lower boundary");
                    Input(controller, profile, element, extended ? 57825 : 225, max);
                    Check(controller.Mode == IrisController.Motion.Pulse, "pulse upper boundary");
                    controller.Tick(0.4f, profile);
                    float held = controller.Aperture;
                    Input(controller, profile, element, extended ? 57826 : 226, max);
                    controller.Tick(3, profile); Equal(controller.Aperture, held, "hold lower boundary");
                    Input(controller, profile, element, extended ? 59110 : 230, max);
                    Check(controller.Mode == IrisController.Motion.Hold, "hold upper boundary");
                    Input(controller, profile, element, extended ? 59111 : 231, max);
                    Check(controller.Mode == IrisController.Motion.ReversePulse, "reverse lower boundary");
                    Input(controller, profile, element, max, max);
                    Check(controller.Mode == IrisController.Motion.ReversePulse, "reverse upper boundary");
                }
                controller.Reset(); Equal(controller.Aperture, 1, "reset opens"); Check(!controller.Active, "reset inactive");
                profile.closingSeconds = 2;
                controller.SetInput(profile, true, 0, 255, generic, false, 0, 255, null, false, 0, false);
                controller.Tick(0.5f, profile); Equal(controller.Aperture, 0.75f, "travel time");
                controller.SetInput(null, true, 0, 255, generic, false, 0, 255, null, false, 0, false);
                Equal(controller.Aperture, 1, "missing profile bypass");
                profile.closingSeconds = 0;
                var select = IrisExamples.CreateMacMode(false).elements[23];
                controller.SetInput(profile, true, 128, 255, generic, true, 100, 255, select, false, 0, true);
                Equal(controller.Aperture, 128f / 255, "separate selector uses position channel");
                profile.minimumHz = 1; profile.maximumHz = 2;
                controller.Reset();
                controller.SetInput(profile, true, 128, 255, generic, true, 201, 255, select, true, 1, true);
                controller.Tick(0.2f, profile);
                Equal(controller.Aperture, profile.pulse.Evaluate(0.4f), "separate speed channel");
                ValidateFixture(profile);
                ValidateShader();
                Debug.Log($"[IrisValidation] PASS: {checks} checks (controller + GPU cookie). Visual beam/pipeline acceptance remains separate.");
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }

        private static void Input(IrisController c, IrisProfile p, FixtureChannelElement e, int value, int max)
            => c.SetInput(p, true, value, max, e, false, 0, 255, null, false, 0, true);
        private static void Check(bool ok, string message)
        { if (!ok) throw new Exception("Iris validation failed: " + message); checks++; }
        private static void Equal(float value, float expected, string message)
            => Check(Mathf.Abs(value - expected) < 0.000001f, message + $" ({value} != {expected})");

        private static void ValidateFixture(IrisProfile profile)
        {
            var go = new GameObject("Iris validation temporary fixture");
            var definition = ScriptableObject.CreateInstance<FixtureDefinition>();
            var source = new Texture2D(2, 2);
            try
            {
                source.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white }); source.Apply();
                var light = go.AddComponent<Light>(); light.type = LightType.Spot; light.cookie = source; light.spotAngle = 40;
                var fixture = go.AddComponent<DmxFixtureComponent>();
                fixture.targetLight = light; fixture.fixture = definition; fixture.startAddress = 5;
                definition.irisProfile = profile;
                definition.modes.Add(IrisExamples.CreateMacMode(true));
                var serialized = new SerializedObject(fixture);
                serialized.FindProperty("syncBeamGoboToDmx").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                fixture.Initialize();
                var bytes = new byte[512];
                // Relative CH24/25 at absolute CH28/29: (128 << 8) | 1.
                bytes[27] = 128; bytes[28] = 1;
                fixture.ApplyFromUniverseBuffer(bytes);
                Equal(fixture.CurrentIrisAperture, 1f - 32769f / 51400f, "byte[] DMX + address + coarse/fine");
                Check(light.cookie is RenderTexture, "fixture cookie composition");
                Equal(light.spotAngle, 40, "iris leaves Zoom angle unchanged");
                Texture output = light.cookie; uint version = output.updateCount;
                fixture.ApplyFromUniverseBuffer(bytes);
                Check(light.cookie == output && output.updateCount == version, "unchanged cookie is cached");
                var integers = new int[512]; integers[27] = 128; integers[28] = 2;
                fixture.ApplyFromUniverseBuffer(integers);
                Equal(fixture.CurrentIrisAperture, 1f - 32770f / 51400f, "int[] DMX fine byte");
                fixture.syncIrisToDmx = false;
                fixture.ApplyFromUniverseBuffer(integers);
                Check(light.cookie == source, "disabled iris restores original gobo");
                fixture.syncIrisToDmx = true;
                fixture.ApplyFromUniverseBuffer(integers);
                fixture.enabled = false;
                // This component is not ExecuteAlways: edit-mode tests must dispatch its
                // runtime teardown explicitly instead of relying on the editor's toggle.
                fixture.SendMessage("OnDisable", SendMessageOptions.RequireReceiver);
                Check(light.cookie == source, "component disable restores original gobo");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void ValidateShader()
        {
            var shader = Resources.Load<Shader>("ArtNet/IrisCookie");
            Check(shader != null && shader.isSupported, "cookie shader supported");
            var material = new Material(shader);
            var target = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            try
            {
                material.SetVector("_IrisShape", new Vector4(0.5f, 0.01f, 0, 0));
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); pixels.Apply();
                Check(pixels.GetPixel(32, 32).r > 0.99f, "center transmits");
                Check(pixels.GetPixel(2, 32).r < 0.01f, "outer area blocked");
                material.SetVector("_IrisShape", Vector4.zero);
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                pixels.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); pixels.Apply();
                Check(pixels.GetPixel(32, 32).r < 0.01f, "fully closed");
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
