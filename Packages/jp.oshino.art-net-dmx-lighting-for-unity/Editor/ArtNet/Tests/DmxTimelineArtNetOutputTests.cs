using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using ArtNet.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ArtNet.Editor.Tests
{
    public sealed class DmxTimelineArtNetOutputTests
    {
        [TestCase(false, 1)]
        [TestCase(true, 2)]
        public void SendsOnlySelectedTimelineUniversesToLoopback(bool allUniverses, int expectedPackets)
        {
            var rigObject = new GameObject("Test Rig");
            var senderObject = new GameObject("Test Sender");
            var outputObject = new GameObject("Test Output");
            using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Client.ReceiveTimeout = 1500;

            try
            {
                var rig = rigObject.AddComponent<DmxRigController>();
                rig.inputMode = DmxRigController.InputMode.PlaybackOnly;
                rig.InjectUniverse(2, Frame(42), DmxRigController.UniverseBufferSource.TimelinePlayback);
                rig.InjectUniverse(7, Frame(77), DmxRigController.UniverseBufferSource.TimelinePlayback);

                var sender = senderObject.AddComponent<ArtNetSender>();
                sender.Close();
                sender.port = ((IPEndPoint)listener.Client.LocalEndPoint).Port;
                sender.Open();

                var output = outputObject.AddComponent<DmxTimelineArtNetOutput>();
                var serialized = new SerializedObject(output);
                serialized.FindProperty("enableOutput").boolValue = true;
                serialized.FindProperty("sendInEditMode").boolValue = true;
                serialized.FindProperty("sender").objectReferenceValue = sender;
                serialized.FindProperty("rigs").arraySize = 1;
                serialized.FindProperty("rigs").GetArrayElementAtIndex(0).objectReferenceValue = rig;
                serialized.FindProperty("universeSelection").enumValueIndex = allUniverses ? 1 : 0;
                serialized.FindProperty("universes").arraySize = 1;
                serialized.FindProperty("universes").GetArrayElementAtIndex(0).intValue = 7;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                output.RefreshRigs();

                output.SendCurrentFrameOnce();

                var received = new Dictionary<int, int>();
                for (int i = 0; i < expectedPackets; i++)
                {
                    IPEndPoint endpoint = null;
                    var packet = new ArtNetData(listener.Receive(ref endpoint));
                    received.Add(packet.Universe, packet.Channels[0]);
                }
                Assert.That(received.Count, Is.EqualTo(expectedPackets));
                Assert.That(received[7], Is.EqualTo(77));
                if (allUniverses) Assert.That(received[2], Is.EqualTo(42));
                Assert.That(output.PacketsSent, Is.EqualTo(expectedPackets));

                output.SendCurrentFrameOnce();
                Assert.That(output.PacketsSent, Is.EqualTo(expectedPackets * 2));

                // A different writer taking over Universe 7 must not be forwarded.
                rig.InjectUniverse(7, Frame(99), DmxRigController.UniverseBufferSource.ExternalInjection);
                output.SendCurrentFrameOnce();
                Assert.That(output.PacketsSent, Is.EqualTo(expectedPackets * 2 + (allUniverses ? 1 : 0)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(outputObject);
                UnityEngine.Object.DestroyImmediate(senderObject);
                UnityEngine.Object.DestroyImmediate(rigObject);
            }
        }

        [Test]
        public void ActiveUniverseNumbers_ExcludeDisabledRig()
        {
            var go = new GameObject("Test Rig");
            try
            {
                var rig = go.AddComponent<DmxRigController>();
                rig.inputMode = DmxRigController.InputMode.PlaybackOnly;
                rig.InjectUniverse(4, Frame(1), DmxRigController.UniverseBufferSource.TimelinePlayback);
                var numbers = new List<int>();
                rig.CopyActiveUniverseNumbers(numbers);
                Assert.That(numbers, Is.EquivalentTo(new[] { 4 }));
                rig.enabled = false;
                rig.CopyActiveUniverseNumbers(numbers);
                Assert.That(numbers, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        private static byte[] Frame(byte first)
        {
            var channels = new byte[512];
            channels[0] = first;
            return channels;
        }
    }
}
