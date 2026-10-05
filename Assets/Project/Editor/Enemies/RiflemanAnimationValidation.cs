using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        internal static readonly string[] AnimationScenarioNames =
        {
            "Idle", "Start walk", "Continuous walk", "Stop walk", "Steady turn 90 left", "Steady turn 90 right", "Steady turn 180 left", "Steady turn 180 right",
            "Idle aiming", "Walk forward", "Walk backward", "Walk strafe", "Walk diagonals", "Run forward", "Run backward", "Run strafe", "Sprint eight directions", "Run acceleration", "Run stopping",
            "Enter crouch", "Crouch idle", "Crouch eight directions", "Combat and crouch turns", "Exit crouch", "Fire idle", "Fire moving", "Fire crouched", "Reload idle", "Reload moving", "Reload crouched",
            "Hit idle", "Hit moving", "Hit crouched", "Front death", "Back death", "Left death", "Right death", "Crouching death", "Moving death to ragdoll", "Steady to Combat", "Combat to Steady", "IK baseline comparison", "Action interruption priorities", "Disabled Animator fallback"
        };
        private static readonly HashSet<string> ObservedAnimationPaths = new HashSet<string>();
        private static readonly Vector3 AnimationStart = new Vector3(4f, 0f, -8f);
        private static StreamWriter _animationSamples;

        [MenuItem("Breachpoint/Enemies/Validation/Run complete animation matrix")]
        public static void RunAnimationMatrix() => RunAnimationScenario(-1);
        public static void RunAnimationScenario(int index)
        {
            SessionState.SetInt("EnemyTools.AnimationScenario", index);
            ValidateStage(70);
        }
        public static void RunAnimationRange(int first)
        {
            if (first < 0 || first >= AnimationScenarioNames.Length) throw new ArgumentOutOfRangeException(nameof(first));
            SessionState.SetInt("EnemyTools.AnimationScenario", 1000 + first);
            ValidateStage(70);
        }
        private static IEnumerator RiflemanAnimationTests(GameLifetimeScope scope, EnemyWorld world)
        {
            Directory.CreateDirectory(EnemyTools.Evidence);
            ObservedAnimationPaths.Clear();
            _animationSamples = new StreamWriter(EnemyTools.Evidence + "/animation-samples.csv", false);
            _animationSamples.WriteLine("Time,Layer,State,Clip,Weight,MoveSpeed,MoveX,MoveY,RootYaw");
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope)) enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), AnimationStart, Quaternion.identity);
            GameObject player = new GameObject("Rework animation target"); player.layer = 6;
            CapsuleCollider collider = player.AddComponent<CapsuleCollider>(); collider.center = Vector3.up; collider.height = 2f; collider.radius = 0.4f;
            Health health = player.AddComponent<Health>(); health.Configure(1000000f, false);
            PerceptionTarget target = player.AddComponent<PerceptionTarget>(); target.Initialize(world, Faction.Player);
            yield return null;
            var brain = enemy.GetComponent<EnemyBrain>(); brain.enabled = false;
            var actor = enemy.GetComponent<EnemyActor>();
            var bridge = enemy.GetComponent<EnemyAnimationBridge>();
            Animator animator = bridge.Animator;
            PlayCheck(animator.cullingMode == AnimatorCullingMode.AlwaysAnimate, "Prefab/runtime keep the bone-attached gameplay muzzle updating outside the camera");
            RigBuilder rig = animator.GetComponent<RigBuilder>();
            PlayCheck(animator.layerCount == 3 && !animator.applyRootMotion && animator.isHuman && animator.avatar.isValid, "Three-layer controller with additive recoil has valid Humanoid and root motion disabled");
            PlayCheck(rig.layers.Count == 2 && rig.layers[0].rig.name == "AimRig" && rig.layers[1].rig.name == "LeftHandRig", "Authored AimRig -> LeftHandRig order remains intact");
            PlayCheck(actor.Muzzle != null && FindUnique(animator.transform, "LeftHandGrip") != null, "Existing weapon muzzle and hand grip remain assigned");
            int selected = SessionState.GetInt("EnemyTools.AnimationScenario", -1);
            try
            {
                for (int index = 0; index < AnimationScenarioNames.Length; index++)
                {
                    if (selected >= 1000 ? index < selected - 1000 : selected >= 0 && index != selected) continue;
                    enemy.SetActive(false); enemy.transform.SetPositionAndRotation(AnimationStart, Quaternion.identity); enemy.SetActive(true);
                    yield return null;
                    brain.ResetForSpawn(); brain.enabled = false;
                    player.transform.position = AnimationStart + Vector3.forward * 10f;
                    brain.Memory.Target = target; brain.Memory.Visible = true; brain.Memory.HasContact = true;
                    bool combat = index >= 8 && index < 39;
                    if (combat) brain.States.Change(EnemyStateId.Combat, "deterministic animation scenario");
                    foreach (var wait in WaitEnumerable(combat ? 1.25f : .35f)) yield return wait;
                    AppendResult("SCENARIO " + (index + 1) + ": " + AnimationScenarioNames[index]);
                    IEnumerator test = AnimationCase(index, enemy, brain, actor, bridge, target);
                    while (test.MoveNext()) { SampleAnimation(animator); yield return test.Current; }
                    SampleAnimation(animator);
                    PlayCheck(true, "Scenario " + (index + 1) + " PASS: " + AnimationScenarioNames[index]);
                }
                PlayCheck(!actor.Health.IsDead || brain.States.Current == EnemyStateId.Dead, "Gameplay health and terminal state remain authoritative");
                File.WriteAllLines(EnemyTools.Evidence + "/observed-animation-assets.txt", ObservedAnimationPaths.OrderBy(path => path));
            }
            finally
            {
                _animationSamples.Dispose(); _animationSamples = null;
                Object.Destroy(enemy); Object.Destroy(player);
            }
            yield return null;
        }
        private static void SampleAnimation(Animator animator)
        {
            if (!animator.enabled) return;
            for (int layer = 0; layer < animator.layerCount; layer++)
            {
                if (animator.GetLayerWeight(layer) <= 0.01f && layer > 0) continue;
                foreach (var info in animator.GetCurrentAnimatorClipInfo(layer))
                {
                    if (info.weight < 0.05f) continue;
                    ObservedAnimationPaths.Add(AssetDatabase.GetAssetPath(info.clip));
                    _animationSamples?.WriteLine(string.Join(",", Time.time.ToString("F3", System.Globalization.CultureInfo.InvariantCulture), layer,
                        EnemyAnimationStateNames.Get(animator.GetCurrentAnimatorStateInfo(layer).fullPathHash), info.clip.name, info.weight.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                        animator.GetFloat("MoveSpeed").ToString("F3", System.Globalization.CultureInfo.InvariantCulture), animator.GetFloat("MoveX").ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                        animator.GetFloat("MoveY").ToString("F3", System.Globalization.CultureInfo.InvariantCulture), animator.transform.root.eulerAngles.y.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)));
                }
            }
        }
        private static bool StateMatches(Animator animator, string name, int layer = 0) => animator.GetCurrentAnimatorStateInfo(layer).IsName(name) || animator.GetNextAnimatorStateInfo(layer).IsName(name);
        // A reaction contributes during the user's incoming blend, before becoming current.
        // Require positive transition progress so a merely queued destination cannot pass.
        private static bool HasActionPose(Animator animator, string tag, int layer = 1) => animator.GetCurrentAnimatorStateInfo(layer).IsTag(tag) || animator.IsInTransition(layer) && animator.GetNextAnimatorStateInfo(layer).IsTag(tag) && animator.GetAnimatorTransitionInfo(layer).normalizedTime > .01f;
        // A manually tuned incoming blend already evaluates the destination turn pose.
        private static bool HasTurnPose(Animator animator) => animator.GetCurrentAnimatorStateInfo(0).IsTag("Turn") || animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag("Turn");
        private static IEnumerator WaitForTurnFixture(Animator animator, string state)
        {
            // Settle the user's authored stance before measuring the separate turn-start deadline.
            float until = Time.time + 3f;
            while (Time.time < until && (!StateMatches(animator, state) || animator.IsInTransition(0))) yield return null;
            PlayCheck(StateMatches(animator, state) && !animator.IsInTransition(0), "Current authored stance settles before isolated turn probe: " + state);
        }
        private static IEnumerator MoveAnimation(EnemyActor actor, Vector3 direction, EnemyMovePace pace, float seconds = 0.9f)
        {
            Vector3 start = actor.transform.position;
            Vector3 destination = start + direction.normalized * 10f;
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                actor.Navigation.MoveTo(destination, pace, Time.time);
                if (pace != EnemyMovePace.Walk || actor.GetComponent<EnemyBrain>().States.Group != EnemyStateGroup.Passive)
                    actor.Navigation.Face(actor.transform.position + Vector3.forward * 20f, Time.deltaTime);
                yield return null;
            }
            // A short startup probe validates acceleration, not full-speed cruise distance.
            float minimumTravel = seconds < .3f ? .02f : Mathf.Sqrt(.02f);
            PlayCheck((actor.transform.position - start).sqrMagnitude > minimumTravel * minimumTravel && actor.Navigation.Velocity.sqrMagnitude > .0004f && !actor.Navigation.Failed, "Actual NavMesh movement succeeds: " + pace + "/" + direction + " | " + actor.Navigation.Result + " | " + actor.transform.position);
        }
        private static IEnumerator AnimationCase(int index, GameObject enemy, EnemyBrain brain, EnemyActor actor, EnemyAnimationBridge bridge, PerceptionTarget target)
        {
            Animator animator = bridge.Animator;
            if (index == 42)
            {
                brain.States.Change(EnemyStateId.Combat, "priority fixture"); brain.Memory.Target = target; brain.Memory.Visible = true;
                Physics.SyncTransforms(); brain.Combat.Attack(target, Time.time - 1f); brain.Combat.Attack(target, Time.time);
                PlayCheck(brain.Combat.RequestReload(Time.time), "Priority fixture starts real reload");
                foreach (var wait in WaitEnumerable(0.25f)) yield return wait;
                PlayCheck(!animator.GetComponent<RigBuilder>().layers[1].active, "Reload releases hand IK through runtime rig layer activity");
                actor.Health.TakeDamage(new DamageInfo(1f, actor.Eyes.position, Vector3.back, target.gameObject));
                foreach (var wait in WaitEnumerable(0.15f)) yield return wait;
                PlayCheck(HasActionPose(animator, "Hit") && brain.Combat.IsReloading, "Hit overrides reload presentation without cancelling gameplay timer");
                foreach (var wait in WaitEnumerable(0.7f)) yield return wait;
                PlayCheck(animator.GetCurrentAnimatorStateInfo(1).IsTag("Reload"), "After hit the ongoing reload presentation resumes");
                actor.Health.TakeDamage(new DamageInfo(100000f, actor.Eyes.position, Vector3.back, target.gameObject));
                foreach (var wait in WaitEnumerable(0.25f)) yield return wait;
                PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsTag("Death") && animator.GetLayerWeight(1) == 0f && animator.GetLayerWeight(2) == 0f && !brain.Combat.IsReloading && !animator.GetComponent<RigBuilder>().layers[0].active, "Death interrupts reload/hit and releases aim rig");
                enemy.SetActive(false); enemy.SetActive(true); yield return null; brain.ResetForSpawn(); brain.enabled = false;
                brain.States.Change(EnemyStateId.Combat, "turn interruption");
                foreach (var wait in WaitEnumerable(1.3f)) yield return wait;
                actor.Navigation.Face(enemy.transform.position + Vector3.left * 10f, Time.deltaTime);
                foreach (var wait in WaitEnumerable(0.15f)) yield return wait;
                PlayCheck(actor.Navigation.IsTurning, "Turn interruption fixture starts controlled turn");
                actor.Health.TakeDamage(new DamageInfo(1f, actor.Eyes.position, Vector3.back, target.gameObject));
                foreach (var wait in WaitEnumerable(0.2f)) yield return wait;
                PlayCheck(!actor.Navigation.IsTurning && HasActionPose(animator, "Hit"), "Hit cancels active controlled turn");
                bridge.SetCrouching(true);
                foreach (var wait in WaitEnumerable(1.3f)) yield return wait;
                PlayCheck(StateMatches(animator, "CrouchLocomotion") && StateMatches(animator, "None", 1), "Stance change during hit settles without locking either layer");
                yield break;
            }
            if (index == 43)
            {
                animator.enabled = false;
                actor.Health.TakeDamage(new DamageInfo(100000f, actor.Eyes.position, Vector3.back, target.gameObject));
                foreach (var wait in WaitEnumerable(0.15f)) yield return wait;
                PlayCheck(brain.States.Current == EnemyStateId.Dead && enemy.GetComponent<EnemyRagdollPresenter>().IsRagdoll, "Disabled Animator falls back to immediate ragdoll without affecting gameplay death");
                enemy.SetActive(false); enemy.SetActive(true); yield return null; brain.ResetForSpawn();
                PlayCheck(animator.enabled && !enemy.GetComponent<EnemyRagdollPresenter>().IsRagdoll, "Fallback death also resets for pooling");
                yield break;
            }
            if (index == 41)
            {
                bridge.enabled = false;
                RuntimeAnimatorController original = animator.runtimeAnimatorController;
                var probe = Object.Instantiate((UnityEditor.Animations.AnimatorController)original);
                probe.layers = new[] { probe.layers[0] };
                var rig = animator.GetComponent<RigBuilder>();
                try
                {
                    rig.Clear(); animator.runtimeAnimatorController = probe; animator.Rebind(); animator.Update(0f); rig.Build();
                    foreach (var wait in WaitEnumerable(0.5f)) yield return wait;
                    var hand = enemy.GetComponentInChildren<TwoBoneIKConstraint>(true);
                    PlayCheck(Vector3.Distance(hand.data.tip.position, hand.data.target.position) < .03f, "Single base layer reaches the preserved hand grip");
                    animator.SetBool("IsCombat", true);
                    // Disable the bridge only for this isolated controller comparison.
                    bridge.enabled = false;
                    foreach (var wait in WaitEnumerable(0.5f)) yield return wait;
                    PlayCheck(Vector3.Distance(hand.data.tip.position, hand.data.target.position) < .03f, "Single base layer retains combat hand grip");
                }
                finally
                {
                    rig.Clear(); animator.runtimeAnimatorController = original; animator.Rebind(); animator.Update(0f); rig.Build();
                    bridge.enabled = true; Object.DestroyImmediate(probe);
                }
                yield break;
            }
            if (index == 0 || index == 8)
            {
                PlayCheck(StateMatches(animator, index == 0 ? "SteadyIdle" : "StandingLocomotion"), "Correct stationary mode state");
                var hand = enemy.GetComponentInChildren<TwoBoneIKConstraint>(true);
                PlayCheck(Vector3.Distance(hand.data.tip.position, hand.data.target.position) < 0.03f, "Stable idle reaches the preserved hand grip");
                yield break;
            }
            if (index >= 1 && index <= 3)
            {
                if (index == 1)
                {
                    Vector3 startPosition = enemy.transform.position;
                    Vector3 destination = startPosition + Vector3.forward * 10f;
                    float deadline = Time.time + .2f;
                    while (Time.time < deadline) { actor.Navigation.MoveTo(destination, EnemyMovePace.Walk, Time.time); yield return null; }
                    PlayCheck(StateMatches(animator, "SteadyStartWalk") && actor.Navigation.MovementPhase == EnemyMovementPhase.Starting && Vector3.Distance(startPosition, enemy.transform.position) < .08f, "Authored Steady startup responds within 0.2 seconds before meaningful world travel");
                    deadline = Time.time + .45f;
                    while (Time.time < deadline) { actor.Navigation.MoveTo(destination, EnemyMovePace.Walk, Time.time); yield return null; }
                    PlayCheck(Vector3.Distance(startPosition, enemy.transform.position) > .02f && actor.Navigation.Velocity.sqrMagnitude > .0004f && !actor.Navigation.Failed, "Delayed Steady startup produces real NavMesh acceleration and travel within 0.65 seconds");
                    yield break;
                }
                IEnumerator move = MoveAnimation(actor, Vector3.forward, EnemyMovePace.Walk, index == 1 ? 0.2f : 1.05f);
                while (move.MoveNext()) yield return move.Current;
                PlayCheck(index == 1 ? StateMatches(animator, "SteadyStartWalk") : StateMatches(animator, "SteadyWalk"), "Steady locomotion responds without idle exit-time delay (state=" + EnemyAnimationStateNames.Get(animator.GetCurrentAnimatorStateInfo(0).fullPathHash) + ", normalized=" + animator.GetCurrentAnimatorStateInfo(0).normalizedTime + ", next=" + EnemyAnimationStateNames.Get(animator.GetNextAnimatorStateInfo(0).fullPathHash) + ")");
                if (index == 3)
                {
                    // Stop Walk requires cruise, not the user's still-blending StartWalk.
                    // Keep the actual stop-response assertion and its 0.4-second deadline.
                    float cruiseDeadline = Time.time + 3f;
                    while (Time.time < cruiseDeadline && (!animator.GetCurrentAnimatorStateInfo(0).IsName("SteadyWalk") || animator.IsInTransition(0))) yield return null;
                    PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName("SteadyWalk") && !animator.IsInTransition(0), "Stop probe begins from settled authored Steady cruise");
                    actor.Navigation.Stop();
                    float stopDeadline = Time.time + 0.4f;
                    while (Time.time < stopDeadline && !StateMatches(animator, "SteadyStopWalk")) yield return null;
                    PlayCheck(StateMatches(animator, "SteadyStopWalk"), "Stop movement selects authored stop clip within 0.4 seconds (speed=" + animator.GetFloat("MoveSpeed") + ", state=" + EnemyAnimationStateNames.Get(animator.GetCurrentAnimatorStateInfo(0).fullPathHash) + ")");
                    foreach (var wait in WaitEnumerable(1.2f)) yield return wait;
                    PlayCheck(StateMatches(animator, "SteadyIdle"), "Stop clip returns to idle without permanent lock");
                }
                yield break;
            }
            if (index >= 4 && index <= 7 || index == 22)
            {
                int groups = index == 22 ? 2 : 1;
                for (int group = 0; group < groups; group++)
                {
                    bridge.SetCrouching(index == 22 && group == 1);
                    IEnumerator settle = WaitForTurnFixture(animator, index == 22 ? group == 1 ? "CrouchLocomotion" : "StandingLocomotion" : "SteadyIdle");
                    while (settle.MoveNext()) yield return settle.Current;
                    int first = index == 22 ? 0 : index - 4; int last = index == 22 ? 4 : first + 1;
                    for (int turn = first; turn < last; turn++)
                    {
                        if (index == 22 && turn > first)
                        {
                            brain.ResetForSpawn(); brain.States.Change(EnemyStateId.Combat, "turn fixture"); bridge.SetCrouching(group == 1);
                            settle = WaitForTurnFixture(animator, group == 1 ? "CrouchLocomotion" : "StandingLocomotion");
                            while (settle.MoveNext()) yield return settle.Current;
                        }
                        float angle = turn == 0 ? -90f : turn == 1 ? 90f : turn == 2 ? -175f : 175f;
                        Vector3 facing = Quaternion.Euler(0f, angle, 0f) * enemy.transform.forward;
                        actor.Navigation.Face(enemy.transform.position + facing * 10f, Time.deltaTime);
                        foreach (var wait in WaitEnumerable(0.3f)) yield return wait;
                        PlayCheck(actor.Navigation.IsTurning && HasTurnPose(animator), "Turn clip and controlled root rotation start together: " + angle);
                        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                        Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                        Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                        Vector3 previousLeft = hips.InverseTransformPoint(leftFoot.position);
                        Vector3 previousRight = hips.InverseTransformPoint(rightFoot.position);
                        float footTravel = 0f; bool progressiveYaw = false;
                        float until = Time.time + 2.2f;
                        while (Time.time < until)
                        {
                            if (HasTurnPose(animator))
                            {
                                Vector3 left = hips.InverseTransformPoint(leftFoot.position);
                                Vector3 right = hips.InverseTransformPoint(rightFoot.position);
                                footTravel += Vector3.Distance(previousLeft, left) + Vector3.Distance(previousRight, right);
                                previousLeft = left; previousRight = right;
                                float remaining = Vector3.Angle(enemy.transform.forward, facing);
                                if (remaining > 5f && remaining < Mathf.Abs(angle) - 5f) progressiveYaw = true;
                            }
                            yield return null;
                        }
                        PlayCheck(footTravel > .05f && progressiveYaw, "Runtime turn has animated feet and progressive body facing: " + angle);
                        PlayCheck(!actor.Navigation.IsTurning && Vector3.Angle(enemy.transform.forward, facing) < 4f && animator.GetCurrentAnimatorStateInfo(0).IsTag("Locomotion"), "Turn finishes once, root reaches desired facing and returns to locomotion");
                    }
                }
                yield break;
            }
            if (index >= 9 && index <= 18 || index == 21)
            {
                EnemyMovePace pace = index == 21 ? EnemyMovePace.Crouch : index >= 16 ? EnemyMovePace.Sprint : index >= 13 ? EnemyMovePace.Run : EnemyMovePace.Walk;
                if (index == 17 || index == 18) pace = EnemyMovePace.Run;
                Vector3[] directions = index == 10 || index == 14 ? new[] { Vector3.back } : index == 11 || index == 15 ? new[] { Vector3.left, Vector3.right } : index == 12 ? new[] { new Vector3(-1,0,1), new Vector3(1,0,1), new Vector3(-1,0,-1), new Vector3(1,0,-1) } : index == 13 || index == 16 || index == 21 ? new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, new Vector3(-1,0,1), new Vector3(1,0,1), new Vector3(-1,0,-1), new Vector3(1,0,-1) } : new[] { Vector3.forward };
                foreach (Vector3 direction in directions)
                {
                    enemy.transform.SetPositionAndRotation(AnimationStart, Quaternion.identity); brain.ResetForSpawn(); brain.States.Change(EnemyStateId.Combat, "directional fixture"); bridge.SetCrouching(index == 21);
                    foreach (var wait in WaitEnumerable(1.3f)) yield return wait;
                    IEnumerator move = MoveAnimation(actor, direction, pace);
                    while (move.MoveNext()) yield return move.Current;
                    Vector3 local = enemy.transform.InverseTransformDirection(actor.Navigation.Velocity);
                    Vector2 expected = new Vector2(local.x, local.z).normalized;
                    Vector2 actual = new Vector2(animator.GetFloat("MoveX"), animator.GetFloat("MoveY")).normalized;
                    PlayCheck(Vector2.Dot(expected, actual) > 0.9f && animator.GetFloat("MoveSpeed") > 0.1f, "Directional parameters follow actual local motion: " + direction);
                    SampleAnimation(animator);
                    actor.Navigation.Stop();
                }
                if (index == 18) { foreach (var wait in WaitEnumerable(0.4f)) yield return wait; PlayCheck(animator.GetFloat("MoveSpeed") < 0.02f, "Run stops responsively without a long StopRun animation lock"); }
                yield break;
            }
            if (index >= 19 && index <= 23)
            {
                bridge.SetCrouching(true); foreach (var wait in WaitEnumerable(1.3f)) yield return wait;
                PlayCheck(StateMatches(animator, "CrouchLocomotion") && actor.Eyes.localPosition.y < 1.2f && enemy.GetComponent<CapsuleCollider>().height < 1.5f, "Gameplay stance and crouch pose agree");
                if (index == 23) { bridge.SetCrouching(false); foreach (var wait in WaitEnumerable(1.4f)) yield return wait; PlayCheck(StateMatches(animator, "StandingLocomotion") && !bridge.IsCrouching, "Crouch exits and restores standing stance"); }
                yield break;
            }
            if (index >= 24 && index <= 32)
            {
                bool crouch = index == 26 || index == 29 || index == 32;
                bool moving = index == 25 || index == 28 || index == 31;
                foreach (EnemyMovePace pace in moving ? new[] { EnemyMovePace.Walk, EnemyMovePace.Run, EnemyMovePace.Sprint } : crouch ? new[] { EnemyMovePace.Walk, EnemyMovePace.Crouch } : new[] { EnemyMovePace.Walk })
                {
                    enemy.transform.SetPositionAndRotation(AnimationStart, Quaternion.identity); brain.ResetForSpawn(); brain.States.Change(EnemyStateId.Combat, "action fixture");
                    bridge.SetCrouching(crouch); brain.Memory.Target = target; brain.Memory.Visible = true; target.transform.position = AnimationStart + Vector3.forward * 10f;
                    foreach (var wait in WaitEnumerable(1.3f)) yield return wait;
                    if (moving || crouch && pace == EnemyMovePace.Crouch) { IEnumerator move = MoveAnimation(actor, Vector3.forward, pace); while (move.MoveNext()) yield return move.Current; }
                    Physics.SyncTransforms();
                    Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                    float crouchHeadHeight = head.position.y - enemy.transform.position.y;
                    float maximumReloadHeight = crouchHeadHeight;
                    if (index <= 29)
                    {
                        brain.Combat.Attack(target, Time.time - 1f); brain.Combat.Attack(target, Time.time);
                        PlayCheck(brain.Combat.Ammo == brain.Config.Combat.MagazineSize - 1, "Action test uses actual gameplay shot");
                        if (index >= 27)
                        {
                            PlayCheck(brain.Combat.RequestReload(Time.time) && !brain.Combat.RequestReload(Time.time), "Gameplay reload starts once and rejects duplicate request");
                            foreach (var wait in WaitEnumerable(0.2f)) yield return wait;
                            PlayCheck(animator.GetCurrentAnimatorStateInfo(1).IsTag("Reload"), "Reload action matches stance/motion");
                            SampleAnimation(animator);
                            actor.Navigation.Stop();
                            float until = Time.time + brain.Config.Combat.ReloadDuration + 0.2f;
                            while (Time.time < until)
                            {
                                brain.Combat.Tick(Time.time);
                                maximumReloadHeight = Mathf.Max(maximumReloadHeight, head.position.y - enemy.transform.position.y);
                                yield return null;
                            }
                            if (index == 29) PlayCheck(maximumReloadHeight <= crouchHeadHeight + .25f, "Crouch reload preserves physical crouched body height throughout the action");
                            PlayCheck(!brain.Combat.IsReloading && brain.Combat.Ammo == brain.Config.Combat.MagazineSize && StateMatches(animator, "None", 1), "Reload timing matches gameplay and cannot lock actions");
                        }
                        else
                        {
                            foreach (var wait in WaitEnumerable(bridge.Config.ActionBlendIn + .04f)) yield return wait;
                            PlayCheck(animator.GetCurrentAnimatorStateInfo(2).IsTag("Fire") && animator.GetLayerWeight(2) > 0f, "Real firing selects fire action while locomotion remains on base layer | current=" + EnemyAnimationStateNames.Get(animator.GetCurrentAnimatorStateInfo(2).fullPathHash) + " | next=" + EnemyAnimationStateNames.Get(animator.GetNextAnimatorStateInfo(2).fullPathHash) + " | transition=" + animator.IsInTransition(2) + " | weight=" + animator.GetLayerWeight(2) + " | firing=" + animator.GetBool("IsFiring") + " | dt=" + Time.deltaTime);
                            actor.Navigation.Stop(); foreach (var wait in WaitEnumerable(0.4f)) yield return wait;
                            PlayCheck(StateMatches(animator, "None", 2), "Fire action stops shortly after gameplay shot");
                        }
                    }
                    else
                    {
                        foreach (Vector3 direction in new[] { Vector3.back, Vector3.right, Vector3.left })
                        {
                            if (moving || crouch && pace == EnemyMovePace.Crouch)
                            { enemy.transform.SetPositionAndRotation(AnimationStart, Quaternion.identity); actor.Navigation.ResetAt(AnimationStart); IEnumerator move = MoveAnimation(actor, Vector3.forward, pace, 0.5f); while (move.MoveNext()) yield return move.Current; }
                            actor.Health.TakeDamage(new DamageInfo(1f, actor.Eyes.position, direction, target.gameObject));
                            foreach (var wait in WaitEnumerable(0.15f)) yield return wait;
                            AppendResult("HIT OBSERVED current=" + EnemyAnimationStateNames.Get(animator.GetCurrentAnimatorStateInfo(1).fullPathHash) + " next=" + EnemyAnimationStateNames.Get(animator.GetNextAnimatorStateInfo(1).fullPathHash) + " transition=" + animator.IsInTransition(1) + " progress=" + animator.GetAnimatorTransitionInfo(1).normalizedTime + " nextTime=" + animator.GetNextAnimatorStateInfo(1).normalizedTime);
                            PlayCheck(HasActionPose(animator, "Hit"), "Nonlethal damage activates directional hit action");
                            SampleAnimation(animator);
                            foreach (var wait in WaitEnumerable(0.7f)) yield return wait;
                            PlayCheck(StateMatches(animator, "None", 1), "Hit action releases locomotion without permanent lock | state=" + EnemyAnimationStateNames.Get(animator.GetCurrentAnimatorStateInfo(1).fullPathHash) + " | length=" + animator.GetCurrentAnimatorStateInfo(1).length + " | normalized=" + animator.GetCurrentAnimatorStateInfo(1).normalizedTime + " | hit=" + animator.GetBool("IsHit") + " | fire=" + animator.GetBool("IsFiring") + " | reload=" + animator.GetBool("IsReloading"));
                        }
                        actor.Navigation.Stop();
                    }
                }
                yield break;
            }
            if (index >= 33 && index <= 38)
            {
                bridge.SetCrouching(index == 37); foreach (var wait in WaitEnumerable(1.3f)) yield return wait;
                if (index == 38) { IEnumerator move = MoveAnimation(actor, Vector3.forward, EnemyMovePace.Run); while (move.MoveNext()) yield return move.Current; }
                Vector3 direction = index == 34 ? Vector3.forward : index == 35 ? Vector3.right : index == 36 ? Vector3.left : Vector3.back;
                var ragdoll = enemy.GetComponent<EnemyRagdollPresenter>();
                if (index == 38) PlayCheck(actor.Navigation.Velocity.magnitude > bridge.Config.DeathAnimationMaxSpeed, "Moving death fixture has real running velocity");
                actor.Health.TakeDamage(new DamageInfo(100000f, actor.Eyes.position, direction, target.gameObject));
                PlayCheck(brain.States.Current == EnemyStateId.Dead && enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped, "Lethal damage immediately stops gameplay | state=" + brain.States.Current + " | velocity=" + actor.Navigation.Velocity + " | stopped=" + enemy.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped + " | rigPresenter=" + enemy.GetComponent<EnemyRigPresenter>().enabled);
                if (index == 38) PlayCheck(ragdoll.IsRagdoll && !animator.enabled && !animator.GetBool("IsDead") && !animator.GetCurrentAnimatorStateInfo(0).IsTag("Death"), "Running lethal hit immediately bypasses directional Death animation");
                Vector3 stoppedPosition = enemy.transform.position; yield return null; yield return null;
                PlayCheck(actor.Navigation.Velocity.sqrMagnitude < 0.001f && Vector3.Distance(stoppedPosition, enemy.transform.position) < 0.01f, "Death stops translation on the next navigation update");
                foreach (var wait in WaitEnumerable(0.22f)) yield return wait;
                if (index != 38) PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsTag("Death") && animator.GetLayerWeight(1) == 0f && animator.GetLayerWeight(2) == 0f, "Stationary death has highest priority and upper-body actions are disabled");
                SampleAnimation(animator);
                // Takeover now follows clip phase rather than the former fixed one-second timer.
                foreach (var wait in Enumerate(WaitForDeathTakeover(ragdoll))) yield return wait;
                PlayCheck(ragdoll.IsRagdoll && !animator.enabled && !animator.GetComponent<RigBuilder>().enabled && enemy.GetComponentsInChildren<Rigidbody>(true).All(body => !body.isKinematic), "Pose-preserving ragdoll takeover succeeds for the selected death flow");
                enemy.SetActive(false); enemy.SetActive(true); yield return null; brain.ResetForSpawn();
                PlayCheck(animator.enabled && animator.GetComponent<RigBuilder>().enabled && !ragdoll.IsRagdoll && enemy.GetComponentsInChildren<Rigidbody>(true).All(body => body.isKinematic) && !animator.GetBool("IsDead"), "Pool reset restores Animator, rigs, bodies and terminal parameters");
                yield break;
            }
            if (index == 39)
            {
                float startDeadline = Time.time + .2f;
                Vector3 startPosition = enemy.transform.position;
                while (Time.time < startDeadline) { actor.Navigation.MoveTo(startPosition + Vector3.forward * 10f, EnemyMovePace.Walk, Time.time); yield return null; }
                PlayCheck(StateMatches(animator, "SteadyStartWalk") && actor.Navigation.MovementPhase == EnemyMovementPhase.Starting && Vector3.Distance(startPosition, enemy.transform.position) < .08f, "Combat interruption fixture reaches visual StartWalk before the synchronized world ramp");
                brain.States.Change(EnemyStateId.Combat, "mode change during StartWalk");
                foreach (var wait in WaitEnumerable(1.3f)) yield return wait;
                PlayCheck(StateMatches(animator, "StandingLocomotion") && animator.GetBool("IsCombat"), "Explicit combat mode interrupts Steady start without CrossFade");
                PlayCheck(actor.Navigation.MovementPhase == EnemyMovementPhase.Idle && actor.Navigation.Velocity.sqrMagnitude < .0001f, "Combat entry cancels the pending passive movement envelope");
            }
            else if (index == 40)
            {
                brain.States.Change(EnemyStateId.Combat, "mode fixture"); foreach (var wait in WaitEnumerable(1.3f)) yield return wait;
                brain.Memory.HasContact = false; brain.Memory.Visible = false; brain.States.Change(EnemyStateId.Idle, "awareness expired");
                foreach (var wait in WaitEnumerable(1.8f)) yield return wait;
                PlayCheck(StateMatches(animator, "SteadyIdle") && !animator.GetBool("IsCombat"), "Awareness loss returns to Steady through conditioned Animator transition");
            }
        }
    }
}
