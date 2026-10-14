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

        [UnityTest]
        public IEnumerator SenderReconnectsWhenHostChangesInPlayMode()
        {
            var senderObject = new GameObject("Test Sender Host Change");
            using var listener = new UdpClient(new IPEndPoint(IPAddress.Parse("127.0.0.2"), 0));
            listener.Client.ReceiveTimeout = 1500;

            try
            {
                var sender = senderObject.AddComponent<ArtNetSender>();
                sender.Close();
                sender.port = ((IPEndPoint)listener.Client.LocalEndPoint).Port;
                sender.Open();

                typeof(ArtNetSender)
                    .GetField("host", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(sender, "127.0.0.2");
                yield return null;

                var channels = new int[512];
                channels[0] = 201;
                sender.Send(channels, universe: 7);

                IPEndPoint endpoint = null;
                var packet = new ArtNetData(listener.Receive(ref endpoint));
                Assert.That(packet.Universe, Is.EqualTo(7));
                Assert.That(packet.Channels[0], Is.EqualTo(201));
            }
            finally
            {
                Object.Destroy(senderObject);
            }
        }

        [UnityTest]
        public IEnumerator SenderCanAlsoSendToLoopbackForLocalMonitors()
        {
            yield return null;

            var senderObject = new GameObject("Test Sender Loopback");
            using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Client.ReceiveTimeout = 1500;

            try
            {
                var sender = senderObject.AddComponent<ArtNetSender>();
                sender.Close();
                sender.port = ((IPEndPoint)listener.Client.LocalEndPoint).Port;
                typeof(ArtNetSender)
                    .GetField("host", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(sender, "127.0.0.2");
                typeof(ArtNetSender)
                    .GetField("sendToLoopback", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(sender, true);
                sender.Open();

                var channels = new int[512];
                channels[0] = 177;
                sender.Send(channels, universe: 3);

                IPEndPoint endpoint = null;
                var packet = new ArtNetData(listener.Receive(ref endpoint));
                Assert.That(packet.Universe, Is.EqualTo(3));
                Assert.That(packet.Channels[0], Is.EqualTo(177));
            }
            finally
            {
                Object.Destroy(senderObject);
            }
        }

        [UnityTest]
        public IEnumerator SenderCanSelectOutgoingNetworkInterface()
        {
            yield return null;

            var senderObject = new GameObject("Test Sender Interface");
            using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Client.ReceiveTimeout = 1500;

            try
            {
                var sender = senderObject.AddComponent<ArtNetSender>();
                sender.Close();
                sender.port = ((IPEndPoint)listener.Client.LocalEndPoint).Port;
                typeof(ArtNetSender)
                    .GetField("localInterfaceAddress", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(sender, "127.0.0.2");
                sender.Open();
                sender.Send(new int[512], universe: 4);

                IPEndPoint endpoint = null;
                var packet = new ArtNetData(listener.Receive(ref endpoint));
                Assert.That(packet.Universe, Is.EqualTo(4));
                Assert.That(endpoint.Address, Is.EqualTo(IPAddress.Parse("127.0.0.2")));
            }
            finally
            {
                Object.Destroy(senderObject);
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
