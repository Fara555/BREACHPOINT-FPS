using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public enum EnemyCoverKind { Low, High }
    public enum EnemyHighCoverSide { FromExposure, Left, Right }
    [DisallowMultipleComponent]
    public sealed class EnemyCoverPoint : MonoBehaviour
    {
        [SerializeField] private EnemyCoverKind _kind;
        [SerializeField] private Collider _protection;
        [SerializeField] private Transform _exposure;
        [SerializeField] private EnemyHighCoverSide _highSide;
        [SerializeField, Min(.1f), Tooltip("Distance from the wall face to the actor root.")] private float _wallClearance = .38f;
        [SerializeField, Min(.1f), Tooltip("Distance inside the selected wall edge while protected.")] private float _cornerInset = .45f;
        [SerializeField, Min(.1f), Tooltip("Distance outside that edge when firing.")] private float _exposureClearance = .6f;
        [SerializeField, Tooltip("Peek depth relative to the wall center, toward the threat.")] private float _exposureNormalOffset;
        public bool ExposesRight => _highSide == EnemyHighCoverSide.Right || _highSide == EnemyHighCoverSide.FromExposure &&
            _exposure != null && Vector3.Dot(_exposure.position - transform.position, transform.right) > 0f;
        public float WallClearance => _wallClearance;
        public float CornerInset => _cornerInset;
        public EnemyCoverKind Kind => _kind;
        public Collider Protection => _protection;
        public Vector3 ProtectedPosition => _kind == EnemyCoverKind.High ? HighPosition(false) : transform.position;
        public Vector3 Normal => transform.forward;
        public Vector3 ExposurePosition => _kind == EnemyCoverKind.High ? HighPosition(true) : _exposure != null ? _exposure.position : transform.position;
        private Vector3 HighPosition(bool exposed)
        {
            if (!(_protection is BoxCollider box)) return exposed && _exposure != null ? _exposure.position : transform.position;
            Vector3 center = box.transform.TransformPoint(box.center);
            Vector3 halfX = box.transform.TransformVector(Vector3.right * box.size.x * .5f);
            Vector3 halfY = box.transform.TransformVector(Vector3.up * box.size.y * .5f);
            Vector3 halfZ = box.transform.TransformVector(Vector3.forward * box.size.z * .5f);
            float width = ProjectionRadius(transform.right, halfX, halfY, halfZ);
            float depth = ProjectionRadius(Normal, halfX, halfY, halfZ);
            float lateral = exposed ? width + _exposureClearance : Mathf.Max(0f, width - _cornerInset);
            Vector3 position = center + transform.right * ((ExposesRight ? 1f : -1f) * lateral) + Normal * (exposed ? _exposureNormalOffset : -depth - _wallClearance);
            position.y = transform.position.y;
            return position;
        }
        private static float ProjectionRadius(Vector3 axis, Vector3 x, Vector3 y, Vector3 z) =>
            Mathf.Abs(Vector3.Dot(axis, x)) + Mathf.Abs(Vector3.Dot(axis, y)) + Mathf.Abs(Vector3.Dot(axis, z));
        public void ConfigureHighEdge(Collider protection, EnemyHighCoverSide side)
        {
            _kind = EnemyCoverKind.High; _protection = protection; _highSide = side;
            transform.position = ProtectedPosition;
        }
        public bool IsUsable => isActiveAndEnabled && _protection != null && _protection.enabled && _protection.gameObject.activeInHierarchy && !_protection.isTrigger && (_kind == EnemyCoverKind.Low || _exposure != null || _protection is BoxCollider && _highSide != EnemyHighCoverSide.FromExposure);
        public void Configure(EnemyCoverKind kind, Collider protection, Transform exposure)
        { _kind = kind; _protection = protection; _exposure = exposure; _highSide = EnemyHighCoverSide.FromExposure; }
    }
}
