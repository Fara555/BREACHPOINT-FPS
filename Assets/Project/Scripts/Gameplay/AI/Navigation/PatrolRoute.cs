using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public sealed class PatrolRoute : MonoBehaviour
    {
        [SerializeField] private Transform[] _points = new Transform[0];
        public int Count => _points.Length;
        public bool TryGetPoint(int index, out Vector3 point)
        {
            point = transform.position;
            if (Count == 0 || _points[index % Count] == null) return false;
            point = _points[index % Count].position; return true;
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < Count; i++) if (TryGetPoint(i, out Vector3 point))
            { Gizmos.DrawSphere(point, 0.15f); if (TryGetPoint(i + 1, out Vector3 next)) Gizmos.DrawLine(point, next); }
        }
    }
}
