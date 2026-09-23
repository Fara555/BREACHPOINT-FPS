using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyDebugView : MonoBehaviour
    {
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var brain = GetComponent<EnemyBrain>();
            if (brain == null || brain.Config == null) return;
            Gizmos.color = brain.Memory.Visible ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, brain.Config.Perception.ViewRange);
            if (brain.Memory.HasContact) Gizmos.DrawLine(transform.position + Vector3.up, brain.Memory.LastKnownPosition);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 2.3f,
                $"{brain.States.Group}/{brain.States.Current}\n{brain.States.LastReason}\nHP {GetComponent<EnemyActor>().Health.CurrentHealth:0} | Ammo {brain.Combat.Ammo} | Alert {brain.Memory.Alert:0.00}");
        }
#endif
    }
}
