/*!
 * Copyright (c) 2026 Oshino
 *
 * Released under the MIT license.
 * see https://opensource.org/licenses/MIT
 */

using UnityEngine;

namespace ArtNet.Runtime
{
    public interface ILightDriver
    {
        void Initialize(Light targetLight);
        void Apply(float dimmer01, Color rgb);
    }
}
