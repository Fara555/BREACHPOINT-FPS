using UnityEngine;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class SurfaceIdentifier : MonoBehaviour
    {
        [SerializeField]
        private SurfaceType _surfaceType = SurfaceType.Default;

        public SurfaceType SurfaceType => _surfaceType;
    }
}
