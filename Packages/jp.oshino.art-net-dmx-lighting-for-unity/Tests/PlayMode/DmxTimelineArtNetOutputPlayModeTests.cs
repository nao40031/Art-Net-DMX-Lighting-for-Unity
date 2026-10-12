using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using ArtNet.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ArtNet.PlayMode.Tests
{
    public sealed class DmxTimelineArtNetOutputPlayModeTests
    {
        [UnityTest]
        public IEnumerator TimelineBufferIsSentToLoopbackInPlayMode()
        {
            yield return null;

            var rigObject = new GameObject("Test Rig");
            var senderObject = new GameObject("Test Sender");
            var outputObject = new GameObject("Test Output");
            using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Client.ReceiveTimeout = 1500;

            try
            {
                var rig = rigObject.AddComponent<DmxRigController>();
                rig.inputMode = DmxRigController.InputMode.PlaybackOnly;
                var values = new byte[512];
                values[0] = 203;
                rig.InjectUniverse(12, values, DmxRigController.UniverseBufferSource.TimelinePlayback);

                var sender = senderObject.AddComponent<ArtNetSender>();
                sender.Close();
                sender.port = ((IPEndPoint)listener.Client.LocalEndPoint).Port;
                sender.Open();

                outputObject.SetActive(false);
                var output = outputObject.AddComponent<DmxTimelineArtNetOutput>();
                SetField(output, "enableOutput", true);
                SetField(output, "sender", sender);
                SetField(output, "rigs", new List<DmxRigController> { rig });
                SetField(output, "universeSelection", DmxTimelineArtNetOutput.UniverseSelection.All);
                outputObject.SetActive(true);
                output.SendCurrentFrameOnce();

                IPEndPoint endpoint = null;
                var packet = new ArtNetData(listener.Receive(ref endpoint));
                Assert.That(packet.Universe, Is.EqualTo(12));
                Assert.That(packet.Channels[0], Is.EqualTo(203));
                Assert.That(output.PacketsSent, Is.EqualTo(1));
            }
            finally
            {
                Object.Destroy(outputObject);
                Object.Destroy(senderObject);
                Object.Destroy(rigObject);
            }
        }

        private static void SetField(object instance, string name, object value)
        {
            typeof(DmxTimelineArtNetOutput)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(instance, value);
        }
    }
}
