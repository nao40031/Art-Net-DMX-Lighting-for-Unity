using System.Collections.Generic;
using System.Reflection;
using ArtNet.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor.Tests
{
    public sealed class ColorSystemTests
    {
        [Test]
        public void CmyFiltersMultiplyAndRemainNeutralAtZero()
        {
            ColorMixProfile profile = ScriptableObject.CreateInstance<ColorMixProfile>();
            try
            {
                Color neutral = profile.EvaluateCmy(0f, 0f, 0f);
                Color mixed = profile.EvaluateCmy(0.5f, 0.25f, 0f);
                Assert.That(neutral.r, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(neutral.g, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(mixed.r, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(mixed.g, Is.EqualTo(0.75f).Within(0.0001f));
                Assert.That(mixed.b, Is.EqualTo(1f).Within(0.0001f));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void TemperatureFiltersAreIndependent()
        {
            ColorMixProfile profile = ScriptableObject.CreateInstance<ColorMixProfile>();
            try
            {
                Color warm = profile.EvaluateTemperature(FixtureAttribute.CTO, 1f);
                Color cool = profile.EvaluateTemperature(FixtureAttribute.CTB, 1f);
                Assert.That(warm.b, Is.LessThan(warm.r));
                Assert.That(cool.r, Is.LessThan(cool.b));
                profile.ctc.mapping = ColorTemperatureMapping.AbsoluteKelvin;
                profile.ctc.kelvinAtZero = 6500f;
                profile.ctc.kelvinAtFull = 3200f;
                Assert.That(profile.EvaluateTemperature(FixtureAttribute.CTC, 0f).r, Is.EqualTo(1f).Within(0.01f));
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void ColorWheelResolvesSlotsAndSplitWithoutModelAssumptions()
        {
            ColorWheelProfile profile = ScriptableObject.CreateInstance<ColorWheelProfile>();
            try
            {
                profile.ranges = new List<ColorWheelRange>
                {
                    new ColorWheelRange { dmxMin = 0, dmxMax = 63, type = ColorWheelRangeType.Open },
                    new ColorWheelRange { dmxMin = 64, dmxMax = 127, type = ColorWheelRangeType.Slot, colorA = Color.red },
                    new ColorWheelRange { dmxMin = 128, dmxMax = 191, type = ColorWheelRangeType.Split, colorA = Color.red, colorB = Color.blue },
                    new ColorWheelRange { dmxMin = 192, dmxMax = 255, type = ColorWheelRangeType.SpinCW }
                };
                Assert.That(profile.ResolveRange(100).EvaluateStatic(100).r, Is.EqualTo(1f));
                Color split = profile.ResolveRange(150).EvaluateStatic(150);
                Assert.That(split.r, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(split.b, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(profile.CountSpinSlots(), Is.EqualTo(2));
                Assert.That(profile.ResolveRange(300), Is.Null);
            }
            finally { Object.DestroyImmediate(profile); }
        }

        [Test]
        public void ElementModeApplies16BitCmyAnd8BitWheelToByteAndIntBuffers()
        {
            GameObject go = new GameObject("Color System Test");
            go.SetActive(false);
            FixtureType fixture = ScriptableObject.CreateInstance<FixtureType>();
            ColorWheelProfile wheel = ScriptableObject.CreateInstance<ColorWheelProfile>();
            try
            {
                wheel.ranges = new List<ColorWheelRange>
                {
                    new ColorWheelRange { dmxMin = 0, dmxMax = 127, type = ColorWheelRangeType.Open },
                    new ColorWheelRange { dmxMin = 128, dmxMax = 255, type = ColorWheelRangeType.Slot, colorA = Color.red }
                };
                fixture.modes = new List<FixtureMode>
                {
                    new FixtureMode
                    {
                        channelCount = 4,
                        elements = new List<FixtureChannelElement>
                        {
                            new FixtureChannelElement { attribute = FixtureAttribute.Cyan, byteRole = FixtureByteRole.Coarse },
                            new FixtureChannelElement { attribute = FixtureAttribute.Cyan, byteRole = FixtureByteRole.Fine },
                            new FixtureChannelElement { attribute = FixtureAttribute.ColorWheel, role = FixtureChannelRole.SelectMode },
                            new FixtureChannelElement { attribute = FixtureAttribute.Dimmer }
                        },
                        wheelBindings = new List<FixtureWheelBinding>
                        {
                            new FixtureWheelBinding { attribute = FixtureAttribute.ColorWheel, instance = 1, colorWheel = wheel }
                        }
                    }
                };
                Light light = go.AddComponent<Light>();
                GenericLightDriver driver = go.AddComponent<GenericLightDriver>();
                DmxFixtureComponent component = go.AddComponent<DmxFixtureComponent>();
                component.fixture = fixture;
                component.targetLight = light;
                driver.Initialize(light);
                go.SetActive(true);
                component.ResolveMapping();
                Assert.That((bool)typeof(DmxFixtureComponent).GetField("_usesElementMode", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component), Is.True);

                byte[] bytes = new byte[512];
                bytes[0] = 128;
                bytes[1] = 0;
                bytes[2] = 200;
                bytes[3] = 255;
                component.ApplyFromUniverseBuffer(bytes);
                Assert.That(((Color)typeof(DmxFixtureComponent).GetField("_targetColor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component)).r,
                    Is.EqualTo(1f - (128 << 8) / 65535f).Within(0.002f));
                Assert.That(light.color.r, Is.EqualTo(1f - (128 << 8) / 65535f).Within(0.002f));
                Assert.That(light.color.g, Is.EqualTo(0f).Within(0.002f));

                int[] values = new int[512];
                for (int i = 0; i < 4; i++) values[i] = bytes[i];
                component.ApplyFromUniverseBuffer(values);
                Assert.That(light.color.r, Is.EqualTo(1f - (128 << 8) / 65535f).Within(0.002f));
                Assert.That(light.color.b, Is.EqualTo(0f).Within(0.002f));

                wheel.use16BitRanges = true;
                wheel.ranges[0].dmxMax = 32767;
                wheel.ranges[1].dmxMin = 32768;
                wheel.ranges[1].dmxMax = 65535;
                fixture.modes.Add(new FixtureMode
                {
                    channelCount = 5,
                    elements = new List<FixtureChannelElement>
                    {
                        new FixtureChannelElement { attribute = FixtureAttribute.Cyan, byteRole = FixtureByteRole.Coarse },
                        new FixtureChannelElement { attribute = FixtureAttribute.Cyan, byteRole = FixtureByteRole.Fine },
                        new FixtureChannelElement { attribute = FixtureAttribute.ColorWheel, role = FixtureChannelRole.SelectMode, byteRole = FixtureByteRole.Coarse },
                        new FixtureChannelElement { attribute = FixtureAttribute.ColorWheel, role = FixtureChannelRole.SelectMode, byteRole = FixtureByteRole.Fine },
                        new FixtureChannelElement { attribute = FixtureAttribute.Dimmer }
                    },
                    wheelBindings = new List<FixtureWheelBinding>
                    {
                        new FixtureWheelBinding { attribute = FixtureAttribute.ColorWheel, instance = 1, colorWheel = wheel }
                    }
                });
                component.mode = 1;
                component.ResolveMapping();
                bytes[2] = 128;
                bytes[3] = 0;
                bytes[4] = 255;
                component.ApplyFromUniverseBuffer(bytes);
                Assert.That(light.color.r, Is.EqualTo(1f - (128 << 8) / 65535f).Within(0.002f));
                Assert.That(light.color.b, Is.EqualTo(0f).Within(0.002f));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(wheel);
                Object.DestroyImmediate(fixture);
            }
        }

        [Test]
        public void LegacyRgbwKeepsUnclampedAdditiveWhiteWithoutNewProfiles()
        {
            GameObject go = new GameObject("Legacy RGBW Test");
            FixtureType fixture = ScriptableObject.CreateInstance<FixtureType>();
            try
            {
                fixture.modes = new List<FixtureMode>
                {
                    new FixtureMode
                    {
                        channelCount = 4,
                        channels = new List<FunctionChannel>
                        {
                            new FunctionChannel { function = FixtureFunction.Red, channel = 1 },
                            new FunctionChannel { function = FixtureFunction.Green, channel = 2 },
                            new FunctionChannel { function = FixtureFunction.Blue, channel = 3 },
                            new FunctionChannel { function = FixtureFunction.White, channel = 4 }
                        }
                    }
                };
                Light light = go.AddComponent<Light>();
                DmxFixtureComponent component = go.AddComponent<DmxFixtureComponent>();
                component.fixture = fixture;
                component.targetLight = light;
                component.ResolveMapping();
                byte[] values = new byte[512];
                values[0] = 255;
                values[3] = 255;
                component.ApplyFromUniverseBuffer(values);
                Color target = (Color)typeof(DmxFixtureComponent)
                    .GetField("_targetColor", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(component);
                Assert.That(target.r, Is.EqualTo(2f).Within(0.0001f));
                Assert.That(target.g, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(fixture);
            }
        }

        [Test]
        public void MacUltraBasic48chUsesItsPublishedColorProfiles()
        {
            const string fixturePath = "Packages/jp.oshino.art-net-dmx-lighting-for-unity/Runtime/ArtNet/FixtureType/MACUltraPerformance_FixtureType.asset";
            FixtureType fixture = AssetDatabase.LoadAssetAtPath<FixtureType>(fixturePath);

            Assert.That(fixture, Is.Not.Null);
            Assert.That(fixture.colorMixProfile, Is.Not.Null);
            Assert.That(fixture.colorMixProfile.ctc.mapping, Is.EqualTo(ColorTemperatureMapping.AbsoluteKelvin));
            Assert.That(fixture.colorMixProfile.ctc.kelvinAtZero, Is.EqualTo(6000f));
            Assert.That(fixture.colorMixProfile.ctc.kelvinAtFull, Is.EqualTo(2850f));

            FixtureMode mode = fixture.modes[0];
            Assert.That(mode.modeName, Is.EqualTo("Basic 48ch"));
            Assert.That(mode.channelCount, Is.EqualTo(48));
            Assert.That(mode.elements[3].attribute, Is.EqualTo(FixtureAttribute.Cyan));
            Assert.That(mode.elements[3].byteRole, Is.EqualTo(FixtureByteRole.Coarse));
            Assert.That(mode.elements[4].byteRole, Is.EqualTo(FixtureByteRole.Fine));
            Assert.That(mode.elements[5].attribute, Is.EqualTo(FixtureAttribute.Magenta));
            Assert.That(mode.elements[7].attribute, Is.EqualTo(FixtureAttribute.Yellow));
            Assert.That(mode.elements[9].attribute, Is.EqualTo(FixtureAttribute.CTC));
            Assert.That(mode.elements[11].attribute, Is.EqualTo(FixtureAttribute.ColorWheel));

            FixtureWheelBinding binding = mode.wheelBindings.Find(item => item.attribute == FixtureAttribute.ColorWheel && item.instance == 1);
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.colorWheel, Is.Not.Null);
            Assert.That(binding.colorWheel.use16BitRanges, Is.False);
            AssertWheelRange(binding.colorWheel, 1, 14, ColorWheelRangeType.Continuous);
            AssertWheelRange(binding.colorWheel, 15, 15, ColorWheelRangeType.Slot);
            AssertWheelRange(binding.colorWheel, 105, 160, ColorWheelRangeType.Slot);
            AssertWheelRange(binding.colorWheel, 193, 214, ColorWheelRangeType.SpinCW);
            AssertWheelRange(binding.colorWheel, 215, 221, ColorWheelRangeType.Stop);
            AssertWheelRange(binding.colorWheel, 222, 243, ColorWheelRangeType.SpinCCW);
            AssertWheelRange(binding.colorWheel, 244, 247, ColorWheelRangeType.Random);
            AssertWheelRange(binding.colorWheel, 248, 251, ColorWheelRangeType.Random);
            AssertWheelRange(binding.colorWheel, 252, 255, ColorWheelRangeType.Random);
            Assert.That(binding.colorWheel.CountSpinSlots(), Is.EqualTo(7));
        }

        private static void AssertWheelRange(ColorWheelProfile profile, int min, int max, ColorWheelRangeType type)
        {
            ColorWheelRange range = profile.ranges.Find(item => item.dmxMin == min && item.dmxMax == max);
            Assert.That(range, Is.Not.Null, $"Expected DMX {min}-{max}.");
            Assert.That(range.type, Is.EqualTo(type));
        }
    }
}
