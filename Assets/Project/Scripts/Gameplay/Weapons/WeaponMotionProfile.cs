using System;
using UnityEngine;

namespace Breachpoint.Gameplay.Weapons
{
    [Serializable]
    public struct WeaponMotionProfile
    {
        [SerializeField, Min(0f)]
        private float _cyclesPerMeter;

        [SerializeField, Min(0.01f)]
        private float _referenceSpeed;

        [SerializeField]
        private Vector3 _positionAmplitude;

        [SerializeField]
        private Vector3 _rotationAmplitude;

        public float CyclesPerMeter => _cyclesPerMeter;
        public float ReferenceSpeed => _referenceSpeed;
        public Vector3 PositionAmplitude => _positionAmplitude;
        public Vector3 RotationAmplitude => _rotationAmplitude;
    }
}
