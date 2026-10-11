using System.Collections;
using System.Collections.Generic;
using ArtNet.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArtNet.PlayMode.Tests
{
    public sealed class ColorWheelPlayModeTests
    {
        [UnityTest]
        public IEnumerator SpinContinuesWithoutNewDmxPackets()
        {
            GameObject go = new GameObject("Color Wheel Spin Test");
            go.SetActive(false);
            FixtureType fixture = ScriptableObject.CreateInstance<FixtureType>();
            ColorWheelProfile wheel = ScriptableObject.CreateInstance<ColorWheelProfile>();
            try
            {
                wheel.ranges = new List<ColorWheelRange>
                {
                    new ColorWheelRange { dmxMin = 0, dmxMax = 63, type = ColorWheelRangeType.Open },
                    new ColorWheelRange { dmxMin = 64, dmxMax = 127, type = ColorWheelRangeType.Slot, colorA = Color.red },
                    new ColorWheelRange { dmxMin = 128, dmxMax = 191, type = ColorWheelRangeType.Slot, colorA = Color.blue },
                    new ColorWheelRange { dmxMin = 192, dmxMax = 255, type = ColorWheelRangeType.SpinCW,
                        cyclesPerSecondAtMin = 1f, cyclesPerSecondAtMax = 1f }
                };
                fixture.modes = new List<FixtureMode>
                {
                    new FixtureMode
                    {
                        channelCount = 2,
                        elements = new List<FixtureChannelElement>
                        {
                            new FixtureChannelElement { attribute = FixtureAttribute.ColorWheel, role = FixtureChannelRole.SelectMode },
                            new FixtureChannelElement { attribute = FixtureAttribute.Dimmer }
                        },
                        wheelBindings = new List<FixtureWheelBinding>
                        {
                            new FixtureWheelBinding { attribute = FixtureAttribute.ColorWheel, colorWheel = wheel }
                        }
                    }
                };
                Light light = go.AddComponent<Light>();
                go.AddComponent<GenericLightDriver>();
                DmxFixtureComponent component = go.AddComponent<DmxFixtureComponent>();
                component.fixture = fixture;
                component.targetLight = light;
                go.SetActive(true);
                component.ResolveMapping();

                byte[] data = new byte[512];
                data[0] = 200;
                data[1] = 255;
                component.ApplyFromUniverseBuffer(data);
                yield return null;
                Color first = light.color;
                yield return new WaitForSecondsRealtime(0.2f);
                Color later = light.color;
                Assert.That(Mathf.Abs(later.r - first.r) + Mathf.Abs(later.b - first.b), Is.GreaterThan(0.1f));
            }
            finally
            {
                Object.Destroy(go);
                Object.Destroy(wheel);
                Object.Destroy(fixture);
            }
        }
    }
}
