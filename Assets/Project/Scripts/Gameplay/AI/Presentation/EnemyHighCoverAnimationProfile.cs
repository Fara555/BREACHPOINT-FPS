using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    [CreateAssetMenu(menuName = "Breachpoint/Enemies/High Cover animation profile")]
    public sealed class EnemyHighCoverAnimationProfile : ScriptableObject
    {
        [SerializeField] private AnimationCurve _takeX, _takeZ, _leaveX, _leaveZ;
        [SerializeField] private AnimationCurve _leaveRightX, _leaveRightZ;
        [SerializeField] private AnimationCurve[] _soleLeft, _soleRight;
        [SerializeField] private Vector2 _leftReference, _rightReference;
        [SerializeField] private float _standingSoleHeight;
        public float StandingSoleHeight => _standingSoleHeight;
        public bool IsValid => _takeX != null && _takeZ != null && _leaveX != null && _leaveZ != null && _soleLeft != null && _soleLeft.Length == 3 && _soleRight != null && _soleRight.Length == 3;
        public Vector2 Reference(bool right) => right ? _rightReference : _leftReference;
        public Vector3 Motion(bool taking, bool right, float time)
        {
            time = Mathf.Clamp01(time);
            // The right-handed ending stance is asymmetric; calibrate each exit's
            // planted foot separately. Older profiles retain their mirrored fallback.
            if (!taking && right && _leaveRightX != null && _leaveRightZ != null && _leaveRightX.length > 0 && _leaveRightZ.length > 0)
                return new Vector3(_leaveRightX.Evaluate(time), 0f, _leaveRightZ.Evaluate(time));
            return new Vector3((right ? -1f : 1f) * (taking ? _takeX : _leaveX).Evaluate(time), 0f, (taking ? _takeZ : _leaveZ).Evaluate(time));
        }
        public float Progress(bool taking, bool right, float time)
        {
            Vector3 end = Motion(taking, right, 1f);
            return Mathf.Clamp01(Vector3.Dot(Motion(taking, right, time), end) / Mathf.Max(.001f, end.sqrMagnitude));
        }
        public float SoleHeight(int phase, bool right, float time) => (right ? _soleRight : _soleLeft)[phase].Evaluate(phase == 1 ? Mathf.Repeat(time, 1f) : Mathf.Clamp01(time));
    }
}
