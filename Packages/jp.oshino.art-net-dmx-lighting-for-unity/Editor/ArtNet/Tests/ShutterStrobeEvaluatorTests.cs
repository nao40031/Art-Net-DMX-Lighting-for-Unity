/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using ArtNet.Runtime;
using NUnit.Framework;

namespace ArtNet.Editor.Tests
{
    public class ShutterStrobeEvaluatorTests
    {
        [Test]
        public void BuildCommand_MapsDmxRangeToFrequency()
        {
            var range = new FixtureChannelRange
            {
                dmxMin = 50,
                dmxMax = 200,
                type = FixtureRangeType.RegularStrobe,
                mappingContext = NormalizedMappingContext.ShutterFrequency,
                shutterFrequencyFromHz = 1f,
                shutterFrequencyToHz = 20f
            };

            ShutterStrobeCommand slow = ShutterStrobeEvaluator.BuildCommand(range, 50);
            ShutterStrobeCommand fast = ShutterStrobeEvaluator.BuildCommand(range, 200);

            Assert.That(slow.mode, Is.EqualTo(ShutterStrobeMode.Regular));
            Assert.That(slow.frequencyHz, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(fast.frequencyHz, Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void BuildCommand_RespectsInvertedMapping()
        {
            var range = new FixtureChannelRange
            {
                dmxMin = 0,
                dmxMax = 255,
                type = FixtureRangeType.RandomStrobe,
                mappingPreset = NormalizedMappingPreset.Inverted,
                shutterFrequencyFromHz = 2f,
                shutterFrequencyToHz = 12f
            };

            Assert.That(ShutterStrobeEvaluator.BuildCommand(range, 0).frequencyHz, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(ShutterStrobeEvaluator.BuildCommand(range, 255).frequencyHz, Is.EqualTo(2f).Within(0.0001f));
        }

        [TestCase(FixtureRangeType.Open, ShutterStrobeMode.Open)]
        [TestCase(FixtureRangeType.Closed, ShutterStrobeMode.Closed)]
        [TestCase(FixtureRangeType.PulseOpen, ShutterStrobeMode.PulseOpen)]
        [TestCase(FixtureRangeType.PulseClose, ShutterStrobeMode.PulseClose)]
        [TestCase(FixtureRangeType.RandomPulseClose, ShutterStrobeMode.RandomPulseClose)]
        public void ResolveMode_MapsGenericRangeTypes(FixtureRangeType rangeType, ShutterStrobeMode expected)
        {
            Assert.That(ShutterStrobeEvaluator.ResolveMode(rangeType), Is.EqualTo(expected));
        }

        [Test]
        public void EvaluateGate_RegularStrobeUsesDutyCycle()
        {
            var command = new ShutterStrobeCommand
            {
                mode = ShutterStrobeMode.Regular,
                frequencyHz = 2f,
                dutyCycle = 0.25f
            };

            Assert.That(Evaluate(command, 0.05d), Is.EqualTo(1f));
            Assert.That(Evaluate(command, 0.20d), Is.EqualTo(0f));
        }

        [Test]
        public void EvaluateGate_SafePreviewCapsFrequencyAtThreeHertz()
        {
            var command = new ShutterStrobeCommand
            {
                mode = ShutterStrobeMode.Regular,
                frequencyHz = 20f,
                dutyCycle = 0.5f
            };

            float safe = ShutterStrobeEvaluator.EvaluateGate(command, 0.04d, 123,
                ShutterStrobePreviewMode.SafeMaximum3Hz, true);
            float full = ShutterStrobeEvaluator.EvaluateGate(command, 0.04d, 123,
                ShutterStrobePreviewMode.FullFixtureRate, true);

            Assert.That(safe, Is.EqualTo(1f));
            Assert.That(full, Is.EqualTo(0f));
        }

        [Test]
        public void EvaluateGate_RandomModeIsDeterministicForSameSeedAndTime()
        {
            var command = new ShutterStrobeCommand
            {
                mode = ShutterStrobeMode.Random,
                frequencyHz = 8f,
                dutyCycle = 0.2f
            };

            float first = ShutterStrobeEvaluator.EvaluateGate(command, 1.234d, 456,
                ShutterStrobePreviewMode.FullFixtureRate, true);
            float second = ShutterStrobeEvaluator.EvaluateGate(command, 1.234d, 456,
                ShutterStrobePreviewMode.FullFixtureRate, true);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void ExistingRangeTypeNumericValuesRemainStable()
        {
            Assert.That((int)FixtureRangeType.PanTiltSpeedSmooth, Is.EqualTo(22));
            Assert.That((int)FixtureRangeType.RegularStrobe, Is.EqualTo(23));
        }

        private static float Evaluate(ShutterStrobeCommand command, double elapsedSeconds)
        {
            return ShutterStrobeEvaluator.EvaluateGate(command, elapsedSeconds, 123,
                ShutterStrobePreviewMode.FullFixtureRate, true);
        }
    }
}
