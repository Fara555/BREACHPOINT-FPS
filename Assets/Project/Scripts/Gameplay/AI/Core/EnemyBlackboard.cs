using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyBlackboard
    {
        public PerceptionTarget Target;
        public bool Visible;
        public bool HasContact;
        public bool HasNoise;
        public Vector3 LastKnownPosition;
        public Vector3 KnownAimPosition;
        public float SharedContactTime = float.NegativeInfinity;
        public Vector3 NoisePosition;
        public float LastSeenTime = float.NegativeInfinity;
        public float NoiseTime = float.NegativeInfinity;
        public float Alert;
        public float StunnedUntil;
        public float StateEnteredAt;
        public int PatrolIndex;

        public void Reset()
        {
            Target = null;
            Visible = HasContact = HasNoise = false;
            LastKnownPosition = KnownAimPosition = NoisePosition = Vector3.zero;
            SharedContactTime = float.NegativeInfinity;
            LastSeenTime = NoiseTime = float.NegativeInfinity;
            Alert = StunnedUntil = StateEnteredAt = 0f;
            PatrolIndex = 0;
        }
    }
}
