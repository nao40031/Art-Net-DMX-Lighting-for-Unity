using UnityEngine;

namespace ArtNet.Runtime
{
    public interface ILightDriver
    {
        void Initialize(Light targetLight);
        void Apply(float dimmer01, Color rgb);
    }
}
