using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public enum EnemyCoverKind { Low, High }
    [DisallowMultipleComponent]
    public sealed class EnemyCoverPoint : MonoBehaviour
    {
        [SerializeField] private EnemyCoverKind _kind;
        [SerializeField] private Collider _protection;
        [SerializeField] private Transform _exposure;
        public EnemyCoverKind Kind => _kind;
        public Collider Protection => _protection;
        public Vector3 ProtectedPosition => transform.position;
        public Vector3 Normal => transform.forward;
        public Vector3 ExposurePosition => _exposure != null ? _exposure.position : transform.position;
        public bool IsUsable => isActiveAndEnabled && _protection != null && _protection.enabled && _protection.gameObject.activeInHierarchy && !_protection.isTrigger && (_kind == EnemyCoverKind.Low || _exposure != null);
        public void Configure(EnemyCoverKind kind, Collider protection, Transform exposure)
        { _kind = kind; _protection = protection; _exposure = exposure; }
    }
}
