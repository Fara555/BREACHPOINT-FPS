using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public interface IEnemySuppressionWeapon
    {
        bool CanSuppress(Vector3 knownPoint, PerceptionTarget knownTarget);
        bool AttackSuppression(Vector3 knownPoint, PerceptionTarget knownTarget, float spreadMultiplier);
    }
}
