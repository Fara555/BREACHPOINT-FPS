using System.Collections.Generic;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    // Created only when a debug consumer first requests a name.
    public static class EnemyAnimationStateNames
    {
        private static readonly Dictionary<int, string> Names = Build();
        public static string Get(int hash) => hash == 0 ? "None" : Names.TryGetValue(hash, out string name) ? name : "Unmapped Animator state";
        private static Dictionary<int, string> Build()
        {
            var names = new Dictionary<int, string>();
            foreach (string state in new[] { "SteadyIdle", "SteadyStartWalk", "SteadyWalk", "SteadyStopWalk", "StandingLocomotion", "CrouchLocomotion", "RaiseWeapon", "LowerWeapon", "StandingToCrouch", "CrouchToStanding", "Death", "CrouchDeath" })
                names.Add(Animator.StringToHash("Base Layer." + state), "Base Layer/" + state);
            foreach (string mode in new[] { "Steady", "Combat", "Crouch" })
                foreach (string turn in new[] { "Turn90Left", "Turn90Right", "Turn180Left", "Turn180Right" })
                    names.Add(Animator.StringToHash("Base Layer." + mode + turn), mode + "/" + turn);
            foreach (string state in new[] { "None", "Fire", "CrouchFire", "Reload", "CrouchReload", "Hit", "CrouchHit" })
                names.Add(Animator.StringToHash("Actions." + state), "Actions/" + state);
            foreach (string state in new[] { "None", "Fire", "CrouchFire" })
                names.Add(Animator.StringToHash("Recoil." + state), "Recoil/" + state);
            return names;
        }
    }
}
