using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        private static readonly string[] CrouchDeathNames = {
            "Crouch — stand still → idle", "Crouch — walk → stop → enter", "Low Cover — run → brake → crouch",
            "Crouch — enter while walking", "Crouch — forward → stop", "Crouch — lateral → stop",
            "Death — stationary hit from front", "Death — stationary hit from back", "Death — stationary hit from left", "Death — stationary hit from right",
            "Death — walking forward", "Death — running forward", "Death — stationary crouch",
            "Death — sprinting", "Death — strafing left", "Death — strafing right", "Death — crouch moving"
        };

        private static IEnumerator CrouchDeathChecks(GameLifetimeScope scope, EnemyWorld world)
        {
            for (int i = 0; i < CrouchDeathNames.Length; i++)
            {
                AppendResult("FOCUSED REVIEW: " + CrouchDeathNames[i]);
                using (var fixture = new RiflemanTacticalFixture(scope, world, 1, "CrouchDeathReview"))
                {
                    IEnumerator review = HumanCrouchDeath(i, fixture);
                    try { while (review.MoveNext()) yield return review.Current; }
                    finally { (review as IDisposable)?.Dispose(); }
                }
                yield return null;
            }
        }

        private static IEnumerator HumanCrouchDeath(int index, RiflemanTacticalFixture fixture)
        {
            yield return null;
            var brain = fixture.Brains[0]; var actor = fixture.Actors[0]; var nav = actor.Navigation;
            brain.enabled = false; brain.ResetForSpawn();
            nav.ResetAt(new Vector3(0,0,-8)); brain.transform.rotation = Quaternion.identity;
            brain.States.Change(EnemyStateId.Combat, "focused crouch/death review");
            brain.Memory.Target = fixture.Target; brain.Memory.Visible = brain.Memory.HasContact = true;
            fixture.Target.transform.position = brain.transform.position + Vector3.forward * 12;
            var bridge = brain.GetComponent<EnemyAnimationBridge>(); var animator = bridge.Animator;
            TacticalReviewPhase = "Standing readiness";
            foreach (var wait in WaitEnumerable(1.6f)) yield return wait;
            if (index < 6)
            {
                if (index == 2)
                {
                    var point = fixture.AddCover(EnemyCoverKind.Low, 0,-3.5f);
                    TacticalReviewPhase = "Run to low cover / braking";
                    float until = Time.time + 5f;
                    do { nav.MoveTo(point.ProtectedPosition, EnemyMovePace.Run, Time.time); yield return null; }
                    while (Time.time < until && Vector3.Distance(brain.transform.position,point.ProtectedPosition) > .35f);
                    nav.RequestStop(); foreach (var wait in WaitEnumerable(.5f)) yield return wait;
                    PlayCheck(nav.Velocity.magnitude < .03f, "Low-cover crouch starts after actual braking");
                }
                if (index == 1)
                {
                    foreach (var wait in Enumerate(ReviewMove(fixture,Vector3.forward,EnemyMovePace.Walk,1.5f))) yield return wait;
                    nav.RequestStop(); foreach (var wait in WaitEnumerable(.5f)) yield return wait;
                }
                bridge.SetCrouching(true); TacticalReviewPhase = "Entering crouch";
                if (index == 3) foreach (var wait in Enumerate(ReviewMove(fixture,Vector3.forward,EnemyMovePace.Walk,2f))) yield return wait;
                else foreach (var wait in WaitEnumerable(1.7f)) yield return wait;
                if (index >= 4) foreach (var wait in Enumerate(ReviewMove(fixture,index == 5 ? Vector3.left : Vector3.forward,EnemyMovePace.Walk,2f))) yield return wait;
                if (index >= 3) PlayCheck(nav.Velocity.magnitude > .2f && new Vector2(animator.GetFloat("MoveX"),animator.GetFloat("MoveY")).magnitude > .2f, "Real moving crouch retains directional presentation");
                nav.RequestStop(); TacticalReviewPhase = "Protected crouch idle";
                foreach (var wait in WaitEnumerable(.8f)) yield return wait;
                PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsName("CrouchLocomotion") && nav.Velocity.magnitude < .03f &&
                    Mathf.Abs(animator.GetFloat("MoveX"))+Mathf.Abs(animator.GetFloat("MoveY")) < .02f, "Stationary crouch settles in centered idle");
                Vector3 position = brain.transform.position;
                // Exercise the exact cover-exposure reversal that previously revived stale direction.
                bridge.SetCrouching(false); TacticalReviewPhase = "Exposure / standing recovery";
                float deadline = Time.time + 1f; int frame = -1;
                while (Time.time < deadline)
                {
                    if (frame != Time.frameCount && animator.GetCurrentAnimatorStateInfo(0).IsName("CrouchLocomotion"))
                    { frame = Time.frameCount; PlayCheck(Mathf.Abs(animator.GetFloat("MoveX"))+Mathf.Abs(animator.GetFloat("MoveY")) < .02f,"Outgoing crouch remains centered after stance intent changes"); }
                    yield return null;
                }
                PlayCheck(Vector3.Distance(position,brain.transform.position) < .02f,"Stationary stance changes preserve cover position");
                yield break;
            }

            bool moving = index == 10 || index == 11 || index >= 13;
            if (index == 12 || index == 16) { bridge.SetCrouching(true); foreach (var wait in WaitEnumerable(1.7f)) yield return wait; }
            Vector3 moveDirection = index == 14 ? Vector3.left : index == 15 ? Vector3.right : Vector3.forward;
            EnemyMovePace pace = index == 13 ? EnemyMovePace.Sprint : index == 11 ? EnemyMovePace.Run : EnemyMovePace.Walk;
            if (moving) foreach (var wait in Enumerate(ReviewMove(fixture,moveDirection,pace,1.4f))) yield return wait;
            Vector3 travel = index == 7 ? Vector3.forward : index == 8 ? Vector3.right : index == 9 ? Vector3.left : Vector3.back;
            string expected = index == 12 ? "death crouching headshot front" : index == 7 ? "death from the back" : index == 8 ? "death from left" : index == 9 ? "death from right" : "death from the front";
            float expectedDirection = index == 7 ? 3 : index == 8 ? 1 : index == 9 ? 2 : 0;
            var ragdoll = brain.GetComponent<EnemyRagdollPresenter>();
            float hitAt = Time.time; Vector3 root = brain.transform.position;
            var weapon = actor.Muzzle.parent;
            Vector3 actualVelocity = Vector3.ProjectOnPlane(nav.Velocity,Vector3.up);
            float speed = actualVelocity.magnitude;
            PlayCheck(moving ? speed > bridge.Config.DeathAnimationMaxSpeed : speed <= bridge.Config.DeathAnimationMaxSpeed,"Death fixture has actual motion appropriate to its route: "+speed.ToString("F4")+" m/s");
            var transforms = animator.GetComponentsInChildren<Transform>(true);
            var poses = transforms.Select(t=>new Pose(t.localPosition,t.localRotation)).ToArray();
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            var bodies = brain.GetComponentsInChildren<Rigidbody>(true);
            Vector3 initialCentre = RagdollCentre(bodies);
            Vector3 lastPose = hips.position;
            bool capture = TacticalReviewRunning && SessionState.GetBool("EnemyTools.CrouchDeath.Capture",false);
            bool capturedReaction = false, capturedPhysics = false, capturedSettled = false;
            TacticalReviewPhase = moving ? "Immediate locomotion → ragdoll" : "Authored death reaction";
            fixture.Target.transform.position = brain.transform.position - travel * 8f;
            actor.Health.TakeDamage(new DamageInfo(100000,actor.Eyes.position,travel,fixture.Target.gameObject));
            PlayCheck(brain.States.Current == EnemyStateId.Dead && brain.GetComponent<UnityEngine.AI.NavMeshAgent>().isStopped && !nav.HasMovementRequest && Mathf.Abs(bridge.DeathPlanarSpeed-speed)<.0001f,$"Lethal damage snapshots actual speed and stops navigation authority | state={brain.States.Current} before={speed:F6} captured={bridge.DeathPlanarSpeed:F6} ragdoll={ragdoll.IsRagdoll}");
            if (moving)
            {
                PlayCheck(ragdoll.IsRagdoll && !animator.enabled && !animator.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>().enabled && ragdoll.TakeoverTime==hitAt,"Moving death releases physics synchronously in the lethal call");
                PlayCheck(!animator.GetBool("IsDead") && !animator.GetCurrentAnimatorStateInfo(0).IsTag("Death") && !animator.GetNextAnimatorStateInfo(0).IsTag("Death"),"Moving death bypasses all directional Death states and their trigger parameter");
                PlayCheck(transforms.Select((t,i)=>Vector3.Distance(t.localPosition,poses[i].position)<.0001f && Quaternion.Angle(t.localRotation,poses[i].rotation)<.01f).All(v=>v),"Immediate takeover preserves every current animated local bone/weapon pose");
                if (capture) ScreenCapture.CaptureScreenshot(EnemyTools.Evidence+"/death-review-"+index+"-immediate.png");
                yield return null; yield return null;
                PlayCheck(nav.Velocity.magnitude<.03f && Vector3.Distance(root,brain.transform.position)<.02f,"Moving casualty stops root navigation on the next simulation update");
            }
            else
            {
                PlayCheck(animator.GetFloat("HitDirection") == expectedDirection,"Real lethal damage selects the attacker-relative death direction");
                foreach (var wait in WaitEnumerable(.3f)) yield return wait;
                PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsTag("Death") && animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name==expected && c.weight>.99f),"Expected directional death clip dominates before physics: "+expected);
                PlayCheck(!ragdoll.IsRagdoll && Vector3.Distance(root,brain.transform.position)<.02f,"Authored reaction remains readable while gameplay navigation is stopped");
                float deadlineAt = hitAt + ragdoll.MaximumAnimationWait + .3f;
                while (!ragdoll.IsRagdoll && Time.time < deadlineAt)
                {
                    lastPose = hips.position;
                    if (capture && !capturedReaction && animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= .45f)
                    {
                        capturedReaction = true;
                        ScreenCapture.CaptureScreenshot(EnemyTools.Evidence+"/death-review-"+index+"-authored.png");
                    }
                    yield return null;
                }
                PlayCheck(ragdoll.IsRagdoll && ragdoll.TakeoverNormalizedTime >= .49f && !animator.enabled,"Ragdoll begins after the configured readable portion of the selected death");
            }
            PlayCheck((moving || Vector3.Distance(lastPose,hips.position)<.08f) && Mathf.Abs(ragdoll.AppliedMomentum.y)<.0001f && ragdoll.AppliedMomentum.magnitude<=(moving ? ragdoll.MaximumMovingDeathSpeed+.01f : 1.21f),"Takeover preserves pose and applies bounded horizontal momentum for the selected death flow");
            if (moving) PlayCheck(Vector3.Dot(ragdoll.AppliedMomentum.normalized,actualVelocity.normalized)>.999f,"Moving death impulse follows actual travel, independently of incoming damage direction");
            TacticalReviewPhase = "Ragdoll settling";
            float initialY = hips.position.y, maximumY = initialY, maximumAngular = 0f, maximumSpan = 0f;
            float forwardTravel = 0f;
            float end = Time.time + 1.4f;
            while (Time.time < end)
            {
                maximumY = Mathf.Max(maximumY,hips.position.y);
                forwardTravel = Mathf.Max(forwardTravel,Vector3.Dot(RagdollCentre(bodies)-initialCentre,actualVelocity.normalized));
                foreach (var body in bodies) { maximumAngular=Mathf.Max(maximumAngular,body.angularVelocity.magnitude); maximumSpan=Mathf.Max(maximumSpan,Vector3.Distance(body.position,hips.position)); }
                if (capture && !capturedPhysics && Time.time >= ragdoll.TakeoverTime+.35f)
                {
                    capturedPhysics = true;
                    ScreenCapture.CaptureScreenshot(EnemyTools.Evidence+"/death-review-"+index+"-ragdoll.png");
                }
                if (capture && !capturedSettled && Time.time >= ragdoll.TakeoverTime+1.2f)
                {
                    capturedSettled = true;
                    ScreenCapture.CaptureScreenshot(EnemyTools.Evidence+"/death-review-"+index+"-settled.png");
                }
                yield return null;
            }
            if (capture) SessionState.SetBool("EnemyTools.CrouchDeath.Capture",false);
            PlayCheck(maximumY-initialY < .12f && brain.States.Current==EnemyStateId.Dead && actor.Muzzle.IsChildOf(weapon),"Corpse remains terminal, attached and does not launch upward");
            PlayCheck(maximumSpan<2.2f,"Ragdoll limbs remain within human body dimensions during settling");
            if (moving) PlayCheck(forwardTravel>.15f,"Moving corpse visibly continues in its real travel direction: "+forwardTravel.ToString("F3")+" m");
            AppendResult($"DEATH MEASUREMENTS: speed={speed:F4} threshold={bridge.Config.DeathAnimationMaxSpeed:F4} animated={!moving} takeover={ragdoll.TakeoverTime-hitAt:F4}s momentum={ragdoll.AppliedMomentum} travel={forwardTravel:F4} rise={maximumY-initialY:F4} span={maximumSpan:F4} angular={maximumAngular:F3}");
            brain.gameObject.SetActive(false); brain.gameObject.SetActive(true); yield return null; brain.ResetForSpawn(); brain.enabled=false;
            PlayCheck(!ragdoll.IsRagdoll && animator.enabled && !animator.GetBool("IsDead") && brain.GetComponentsInChildren<Rigidbody>(true).All(b=>b.isKinematic),"Death presentation and physical bodies reset for reuse");
        }

        private static IEnumerator ReviewMove(RiflemanTacticalFixture fixture, Vector3 direction, EnemyMovePace pace, float seconds)
        {
            var brain = fixture.Brains[0]; var nav = fixture.Actors[0].Navigation;
            Vector3 goal = brain.transform.position + direction * 12;
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                nav.MoveTo(goal,brain.GetComponent<EnemyAnimationBridge>().IsCrouching ? EnemyMovePace.Crouch : pace,Time.time);
                fixture.Target.transform.position = brain.transform.position + Vector3.forward*12;
                nav.Face(fixture.Target.transform.position,Time.deltaTime);
                yield return null;
            }
        }

        private static Vector3 RagdollCentre(Rigidbody[] bodies)
        {
            Vector3 weightedPosition=Vector3.zero; float mass=0f;
            foreach (var body in bodies) { weightedPosition+=body.worldCenterOfMass*body.mass; mass+=body.mass; }
            return mass>0 ? weightedPosition/mass : Vector3.zero;
        }

        private static IEnumerator DeathClassificationChecks(GameLifetimeScope scope, EnemyWorld world)
        {
            string[] names = { "Real slow navigation below threshold", "Real slow navigation above threshold", "Sprint requested before actual travel", "Stop requested while actually braking" };
            for (int index=0; index<names.Length; index++)
            using (var fixture = new RiflemanTacticalFixture(scope,world,1,"DeathClassification"))
            {
                yield return null;
                var brain=fixture.Brains[0]; var actor=fixture.Actors[0]; var nav=actor.Navigation;
                var bridge=brain.GetComponent<EnemyAnimationBridge>(); var animator=bridge.Animator;
                var ragdoll=brain.GetComponent<EnemyRagdollPresenter>();
                brain.enabled=false; brain.ResetForSpawn();
                brain.States.Change(index<2 ? EnemyStateId.Idle : EnemyStateId.Combat,"death classification fixture");
                foreach (var wait in WaitEnumerable(1.6f)) yield return wait;
                EnemyMovementConfig movement=null;
                try
                {
                    if (index<2)
                    {
                        // Use actual path-following at controlled low speed, rather than
                        // replacing the velocity source or writing Animator parameters.
                        movement=UnityEngine.Object.Instantiate(brain.Config.Movement);
                        var data=new SerializedObject(movement);
                        data.FindProperty("<SteadyWalkSpeed>k__BackingField").floatValue=index==0 ? .04f : .12f;
                        data.ApplyModifiedPropertiesWithoutUndo(); nav.Initialize(movement);
                        foreach (var wait in Enumerate(ReviewMove(fixture,Vector3.forward,EnemyMovePace.Walk,1.8f))) yield return wait;
                        float measured=Vector3.ProjectOnPlane(nav.Velocity,Vector3.up).magnitude;
                        PlayCheck(Mathf.Abs(measured-(index==0 ? .04f : .12f))<.005f,"Boundary fixture actually navigates at its controlled world speed: "+measured);
                    }
                    else if (index==2)
                    {
                        nav.MoveTo(brain.transform.position+Vector3.forward*12,EnemyMovePace.Sprint,Time.time);
                        PlayCheck(nav.DesiredMovementTier==EnemyMovementTier.Sprint && nav.HasMovementRequest && nav.Velocity.magnitude<.001f,"Sprint intent exists while actual physical motion is still zero");
                    }
                    else
                    {
                        foreach (var wait in Enumerate(ReviewMove(fixture,Vector3.forward,EnemyMovePace.Run,1.4f))) yield return wait;
                        nav.RequestStop();
                        PlayCheck(!nav.HasMovementRequest && nav.RequestedWorldSpeed==0 && nav.Velocity.magnitude>bridge.Config.DeathAnimationMaxSpeed,"Stop intent exists while the actor is still physically moving");
                    }
                    float speed=Vector3.ProjectOnPlane(nav.Velocity,Vector3.up).magnitude;
                    bool animated=index==0 || index==2;
                    actor.Health.TakeDamage(new DamageInfo(100000,actor.Eyes.position,Vector3.back,fixture.Target.gameObject));
                    PlayCheck(Mathf.Abs(bridge.DeathPlanarSpeed-speed)<.0001f && brain.States.Current==EnemyStateId.Dead,"Classification captures lethal-time actual speed before stop: "+names[index]);
                    PlayCheck(animated ? !ragdoll.IsRagdoll && animator.enabled && animator.GetBool("IsDead") : ragdoll.IsRagdoll && !animator.enabled && !animator.GetBool("IsDead"),"Actual speed overrides requested motion: "+names[index]);
                    if (animated)
                    {
                        foreach (var wait in WaitEnumerable(.3f)) yield return wait;
                        PlayCheck(animator.GetCurrentAnimatorStateInfo(0).IsTag("Death"),"Effectively stationary actor reaches its authored death");
                        foreach (var wait in Enumerate(WaitForDeathTakeover(ragdoll))) yield return wait;
                        PlayCheck(ragdoll.IsRagdoll && ragdoll.TakeoverNormalizedTime>=.49f,"Boundary stationary death retains its readable reaction before physics");
                    }
                    AppendResult($"CLASSIFICATION: {names[index]} speed={speed:F5} threshold={bridge.Config.DeathAnimationMaxSpeed:F5} animated={animated}");
                }
                finally
                {
                    if (movement!=null) { nav.Initialize(brain.Config.Movement); UnityEngine.Object.Destroy(movement); }
                }
            }
            GameObject melee;
            using (VContainer.Unity.LifetimeScope.EnqueueParent(scope))
                melee=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Enemies/Prefabs/Melee.prefab"),new Vector3(0,0,-8),Quaternion.identity);
            try
            {
                yield return null;
                var brain=melee.GetComponent<EnemyBrain>(); brain.enabled=false; brain.ResetForSpawn();
                foreach (var wait in WaitEnumerable(.15f)) yield return wait;
                var bridge=melee.GetComponent<EnemyAnimationBridge>(); var actor=melee.GetComponent<EnemyActor>();
                PlayCheck(!bridge.Animator.parameters.Any(p=>p.name=="IsCombat") && melee.GetComponent<EnemyRagdollPresenter>()==null,"Legacy Melee fixture uses its original controller without Rifleman ragdoll");
                actor.Health.TakeDamage(new DamageInfo(100000,actor.Eyes.position,Vector3.back,null));
                PlayCheck(brain.States.Current==EnemyStateId.Dead && bridge.Animator.enabled && bridge.Animator.GetBool("Dead"),"Shared bridge preserves legacy Melee death presentation");
            }
            finally { UnityEngine.Object.Destroy(melee); }
        }

        private static IEnumerator WaitForDeathTakeover(EnemyRagdollPresenter ragdoll)
        {
            float deadline = Time.time + ragdoll.MaximumAnimationWait + .3f;
            while (!ragdoll.IsRagdoll && Time.time < deadline) yield return null;
        }

        private static IEnumerator CrouchEntryProbe(GameLifetimeScope scope, EnemyWorld world)
        {
            var service = scope.Container.Resolve<EnemyCoverService>();
            var original = service.Points.ToArray();
            foreach (var point in original) service.Unregister(point);
            var random = UnityEngine.Random.state;
            var report = new StringBuilder("Time,Enemy,Phase,World,Desired,LocalWorld,LocalDesired,MoveSpeed,X,Y,Crouching,Current,Next,Transition,EnterTime,Clips,HipsY,HipsDelta,HandDelta,Aim,Coarse,Hand\n");
            try
            {
                UnityEngine.Random.InitState(4706);
                using (var fixture = new RiflemanTacticalFixture(scope, world, 4, "CrouchEntry"))
                {
                    fixture.AddCover(EnemyCoverKind.Low, 8f, -5.9f);
                    var hips = fixture.Brains.Select(b => b.GetComponent<EnemyAnimationBridge>().Animator.GetBoneTransform(HumanBodyBones.Hips)).ToArray();
                    var hands = fixture.Brains.Select(b => b.GetComponent<EnemyAnimationBridge>().Animator.GetBoneTransform(HumanBodyBones.LeftHand)).ToArray();
                    var previousHips = hips.Select(t => t.position).ToArray();
                    var previousHands = hands.Select(t => t.position).ToArray();
                    var until = new float[4]; int frame = -1; bool entered = false;
                    float end = Time.time + 11f;
                    while (Time.time < end)
                    {
                        if (frame != Time.frameCount)
                        {
                            frame = Time.frameCount;
                            for (int i = 0; i < 4; i++)
                            {
                                var brain = fixture.Brains[i]; var bridge = brain.GetComponent<EnemyAnimationBridge>(); var nav = fixture.Actors[i].Navigation; var animator = bridge.Animator;
                                var current = animator.GetCurrentAnimatorStateInfo(0); bool transition = animator.IsInTransition(0); var next = animator.GetNextAnimatorStateInfo(0);
                                if (current.IsName("StandingToCrouch") || transition && next.IsName("StandingToCrouch")) { until[i] = Time.time + .5f; entered = true; }
                                if (Time.time <= until[i])
                                {
                                    float blend = transition ? animator.GetAnimatorTransitionInfo(0).normalizedTime : 0;
                                    // AnimatorClipInfo already includes evaluated transition weight.
                                    string clips = string.Join(";", animator.GetCurrentAnimatorClipInfo(0).Select(c => c.clip.name + ":" + c.weight.ToString("F3")));
                                    if (transition) clips += ";NEXT:" + string.Join(";", animator.GetNextAnimatorClipInfo(0).Select(c => c.clip.name + ":" + c.weight.ToString("F3")));
                                    var rig = brain.GetComponent<EnemyRigPresenter>();
                                    report.AppendLine(FormattableString.Invariant($"{Time.time:F3}|{i}|{brain.Tactics.CoverPhase}|{nav.Velocity}|{nav.PresentationDesiredVelocity}|{brain.transform.InverseTransformDirection(nav.Velocity)}|{brain.transform.InverseTransformDirection(nav.PresentationDesiredVelocity)}|{animator.GetFloat("MoveSpeed"):F4}|{animator.GetFloat("MoveX"):F4}|{animator.GetFloat("MoveY"):F4}|{bridge.IsCrouching}|{EnemyAnimationStateNames.Get(current.fullPathHash)}|{(transition?EnemyAnimationStateNames.Get(next.fullPathHash):"-")}|{blend:F3}|{current.normalizedTime:F3}|{clips}|{hips[i].position.y:F4}|{Vector3.Distance(previousHips[i],hips[i].position):F4}|{Vector3.Distance(previousHands[i],hands[i].position):F4}|{rig.AimRigWeight:F3}|{rig.CoarseWeight:F3}|{rig.HandWeight:F3}"));
                                }
                                previousHips[i] = hips[i].position; previousHands[i] = hands[i].position;
                            }
                        }
                        fixture.Observe(); yield return null;
                    }
                    PlayCheck(entered && fixture.SawProtectedLow, "Actual competing low-cover arrival enters protected crouch");
                }
            }
            finally
            {
                File.WriteAllText(EnemyTools.Evidence + "/crouch-entry-" + SessionState.GetString("EnemyTools.CrouchDeath.Label", "before") + ".txt", report.ToString());
                UnityEngine.Random.state = random;
                foreach (var point in original) service.Register(point);
            }
        }

        private static IEnumerator DeathPhysicsProbe(GameLifetimeScope scope, EnemyWorld world)
        {
            var report = new StringBuilder();
            using (var fixture = new RiflemanTacticalFixture(scope, world, 1, "DeathPhysics"))
            {
                yield return null;
                var brain = fixture.Brains[0]; var actor = fixture.Actors[0]; var bridge = brain.GetComponent<EnemyAnimationBridge>(); var animator = bridge.Animator;
                brain.enabled = false;
                var bodies = brain.GetComponentsInChildren<Rigidbody>(true);
                report.AppendLine("Total mass=" + bodies.Sum(b => b.mass));
                foreach (var body in bodies)
                {
                    var joint = body.GetComponent<CharacterJoint>();
                    report.AppendLine($"{body.name}: mass={body.mass} damping={body.linearDamping}/{body.angularDamping} interpolation={body.interpolation}" +
                        (joint != null ? $" joint={joint.connectedBody.name} connectedCollision={joint.enableCollision} swing={joint.swing1Limit.limit}/{joint.swing2Limit.limit} twist={joint.lowTwistLimit.limit}/{joint.highTwistLimit.limit}" : " root"));
                }
                var directions = new[] { Vector3.back, Vector3.forward, Vector3.right, Vector3.left };
                var names = new[] { "Front", "Back", "Left", "Right" };
                for (int i = 0; i < 4; i++)
                {
                    brain.ResetForSpawn(); brain.enabled = false; actor.Navigation.ResetAt(new Vector3(0,0,-8)); brain.transform.rotation = Quaternion.identity;
                    brain.States.Change(EnemyStateId.Combat, "directional death probe");
                    foreach (var wait in WaitEnumerable(1.6f)) yield return wait;
                    float hit = Time.time;
                    actor.Health.TakeDamage(new DamageInfo(100000f, actor.Eyes.position, directions[i], fixture.Target.gameObject));
                    var ragdoll = brain.GetComponent<EnemyRagdollPresenter>();
                    string overlaps = "", anchors = "";
                    while (!ragdoll.IsRagdoll && Time.time < hit + 5f)
                    {
                        var collisions = bodies.Select(b=>b.GetComponent<Collider>()).Where(c=>c!=null).ToArray();
                        var overlap = new List<string>();
                        for (int a=0; a<collisions.Length; a++) for(int b=a+1; b<collisions.Length; b++)
                            if (Physics.ComputePenetration(collisions[a],collisions[a].transform.position,collisions[a].transform.rotation,collisions[b],collisions[b].transform.position,collisions[b].transform.rotation,out _,out float depth))
                                overlap.Add(collisions[a].name+"/"+collisions[b].name+":"+depth.ToString("F3"));
                        overlaps = string.Join(";",overlap);
                        anchors = string.Join(";",brain.GetComponentsInChildren<CharacterJoint>(true).Select(j=>j.name+":"+Vector3.Distance(j.transform.TransformPoint(j.anchor),j.connectedBody.transform.TransformPoint(j.connectedAnchor)).ToString("F3")));
                        yield return null;
                    }
                    report.AppendLine("Pose overlaps: " + overlaps);
                    report.AppendLine("Anchor separation: " + anchors);
                    var clips = animator.GetCurrentAnimatorClipInfo(0);
                    float startY = animator.GetBoneTransform(HumanBodyBones.Hips).position.y, maxRise = 0, maxSpeed = 0, maxAngular = 0; string angularBody = "";
                    report.AppendLine($"{names[i]}: direction={animator.GetFloat("HitDirection")} clip={string.Join(";",clips.Select(c=>c.clip.name+":"+c.weight))} norm={animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F3} takeover={Time.time-hit:F3} initial={bodies[0].linearVelocity}");
                    float end = Time.time + .8f;
                    while (Time.time < end)
                    {
                        maxRise = Mathf.Max(maxRise, animator.GetBoneTransform(HumanBodyBones.Hips).position.y-startY);
                        foreach (var body in bodies) { maxSpeed = Mathf.Max(maxSpeed,body.linearVelocity.magnitude); if(body.angularVelocity.magnitude>maxAngular) { maxAngular=body.angularVelocity.magnitude; angularBody=body.name; } }
                        yield return null;
                    }
                    report.AppendLine($"{names[i]}: maxRise={maxRise:F3} maxSpeed={maxSpeed:F3} maxAngular={maxAngular:F3} body={angularBody}");
                }
                PlayCheck(true, "Directional real-damage and takeover measurements recorded");
            }
            File.WriteAllText(EnemyTools.Evidence + "/death-physics-" + SessionState.GetString("EnemyTools.CrouchDeath.Label", "before") + ".txt",report.ToString());
        }
    }

    public static partial class EnemyTools
    {
        private static void ConfigureMovingDeathMomentum()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
            var prefab=PrefabUtility.LoadPrefabContents(EnemyValidationRunner.RiflemanPath);
            try
            {
                var data=new SerializedObject(prefab.GetComponent<EnemyRagdollPresenter>());
                data.FindProperty("_movingDeathSpeed").animationCurveValue=new AnimationCurve(new Keyframe(0,0),new Keyframe(1.5f,2f),new Keyframe(3.2f,4.2f),new Keyframe(4.8f,6.5f));
                data.FindProperty("_maximumMovingDeathSpeed").floatValue=6.5f;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab,EnemyValidationRunner.RiflemanPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }

        private static void ConfigureDeathPolish()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before authoring.");
            var prefab = PrefabUtility.LoadPrefabContents(EnemyValidationRunner.RiflemanPath);
            try
            {
                var data = new SerializedObject(prefab.GetComponent<EnemyRagdollPresenter>());
                data.FindProperty("_takeoverDelay").floatValue = 1.2f;
                data.FindProperty("_momentumScale").floatValue = .25f;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(prefab, EnemyValidationRunner.RiflemanPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
    }
}
