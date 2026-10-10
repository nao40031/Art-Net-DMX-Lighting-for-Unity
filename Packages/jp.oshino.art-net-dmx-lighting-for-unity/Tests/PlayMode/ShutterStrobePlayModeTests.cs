/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using System.Collections;
using ArtNet.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArtNet.PlayMode.Tests
{
    public class ShutterStrobePlayModeTests
    {
        [UnityTest]
        public IEnumerator AnimatedGateRunsInPlayMode()
        {
            yield return null;

            Assert.That(Application.isPlaying, Is.True);

            var command = new ShutterStrobeCommand
            {
                mode = ShutterStrobeMode.Regular,
                frequencyHz = 2f,
                dutyCycle = 0.5f
            };

            float open = ShutterStrobeEvaluator.EvaluateGate(command, 0.1d, 1,
                ShutterStrobePreviewMode.FullFixtureRate, Application.isPlaying);
            float closed = ShutterStrobeEvaluator.EvaluateGate(command, 0.3d, 1,
                ShutterStrobePreviewMode.FullFixtureRate, Application.isPlaying);

            Assert.That(open, Is.EqualTo(1f));
            Assert.That(closed, Is.EqualTo(0f));
        }
    }
}
