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
using VContainer;
using Object = UnityEngine.Object;
namespace Breachpoint.Editor.Enemies
{
    public static partial class EnemyValidationRunner
    {
        internal const int HighCoverReviewStart = 57;
        internal const string HighCoverCaptureKey = "EnemyTools.HighCover.Capture";
        internal static readonly string[] HighCoverReviewNames = {
            "High Cover — take / idle / leave / fire / return (right)",
            "High Cover — protected reload and expose again",
            "High Cover — death during entry",
            "High Cover — death during exit",
            "High Cover — disable / reset / reuse",
            "High Cover — take / idle / leave / fire / return (left)",
            "High Cover \u2014 lost target / bounded release"
        };
        private static IEnumerator HighCoverChecks(GameLifetimeScope scope, EnemyWorld world)
        {
            var service = scope.Container.Resolve<EnemyCoverService>();
            var original = new List<EnemyCoverPoint>(service.Points);
            foreach(var point in original) service.Unregister(point);
            var priorRandom = UnityEngine.Random.state;
            bool priorCapture = SessionState.GetBool(HighCoverCaptureKey, false);
            SessionState.SetBool(HighCoverCaptureKey, false);
            var camera = CreateFinalCamera();
            try
            {
                for(int index=0;index<HighCoverReviewNames.Length;index++)
                {
                    UnityEngine.Random.InitState(4705);
                    AppendResult("HIGH COVER: " + HighCoverReviewNames[index]);
                    using(var fixture = new RiflemanTacticalFixture(scope,world,1,"HighCover",2,index==1?0:-1))
                    {
                        var sequence = HumanHighCover(index,fixture,camera);
                        try { while(sequence.MoveNext()) { fixture.Observe(); yield return sequence.Current; } }
                        finally { (sequence as IDisposable)?.Dispose(); }
                    }
                    yield return null;
                    PlayCheck(service.ReservationCount==0,"High Cover teardown releases every reservation");
                }
            }
            finally { SessionState.SetBool(HighCoverCaptureKey, priorCapture); Object.Destroy(camera.gameObject); UnityEngine.Random.state=priorRandom; foreach(var point in original) service.Register(point); }
        }
        private static Vector3 AnimatorPosition(EnemyBrain owner) => owner.GetComponent<EnemyAnimationBridge>().Animator.transform.position;
        private static void MeasureHighCoverSkin(EnemyBrain brain, EnemyCoverPoint point, out float soleGap, out float backGap, out float edgeInset)
        {
            var animator=brain.GetComponent<EnemyAnimationBridge>().Animator;
            var feet=new HashSet<Transform>(new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightToes}.Select(animator.GetBoneTransform));
            var torso=new HashSet<Transform>(new[]{HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest}.Select(animator.GetBoneTransform));
            Vector3 rear=point.ProtectedPosition+point.Normal*point.WallClearance;
            float minimum=float.PositiveInfinity, maximum=float.NegativeInfinity;
            Vector3 lowest=Vector3.zero;
            var mesh=new Mesh();
            try
            {
                foreach(var renderer in brain.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if(renderer.sharedMesh==null) continue;
                    renderer.BakeMesh(mesh); var vertices=mesh.vertices; var weights=renderer.sharedMesh.boneWeights; var bones=renderer.bones;
                    for(int i=0;i<vertices.Length;i++)
                    {
                        var w=weights[i];
                        float Weight(HashSet<Transform> set,int bone,float weight)=>set.Contains(bones[bone])?weight:0f;
                        Vector3 world=renderer.transform.TransformPoint(vertices[i]);
                        if(Weight(feet,w.boneIndex0,w.weight0)+Weight(feet,w.boneIndex1,w.weight1)+Weight(feet,w.boneIndex2,w.weight2)+Weight(feet,w.boneIndex3,w.weight3)>.5f && world.y<minimum) { minimum=world.y; lowest=world; }
                        if(Weight(torso,w.boneIndex0,w.weight0)+Weight(torso,w.boneIndex1,w.weight1)+Weight(torso,w.boneIndex2,w.weight2)+Weight(torso,w.boneIndex3,w.weight3)>.5f) maximum=Mathf.Max(maximum,Vector3.Dot(world-rear,point.Normal));
                    }
                }
            }
            finally { Object.Destroy(mesh); }
            var physics=new EnemyPhysics();
            if(!physics.TryFirstHit(lowest+Vector3.up*.5f,Vector3.down,2f,brain.Config.Perception.ObstructionMask,brain.transform,out var hit)) throw new InvalidOperationException("No physical floor beneath the rendered shoes.");
            soleGap=minimum-hit.point.y; backGap=-maximum;
            float sign=point.ExposesRight?1f:-1f;
            Vector3 edge=point.ProtectedPosition+point.transform.right*(sign*point.CornerInset);
            edgeInset=sign*Vector3.Dot(edge-animator.GetBoneTransform(HumanBodyBones.Head).position,point.transform.right);
        }
        private static void SeedHighCoverContact(EnemyBrain brain,PerceptionTarget target)
        {
            var memory=brain.Memory;
            memory.Target=target; memory.HasContact=true;
            memory.LastSeenTime=Time.time; memory.LastKnownPosition=target.transform.position; memory.KnownAimPosition=target.AimPosition;
        }
        private static IEnumerator HumanHighCover(int index, RiflemanTacticalFixture fixture, Camera camera)
        {
            yield return null;
            float initializedBy=Time.time+2f;
            while(fixture.Brains[0].Tactics.Member==null && Time.time<initializedBy) yield return null;
            PlayCheck(fixture.Brains[0].Tactics.Member!=null,"High Cover fixture completes its normal startup");
            var config=new SerializedObject(fixture.Brains[0].Config.Tactics);
            config.FindProperty("<CoverPreferenceBonus>k__BackingField").floatValue=150f;
            config.ApplyModifiedPropertiesWithoutUndo();
            SeedHighCoverContact(fixture.Brains[0],fixture.Target);
            var point=fixture.AddCover(EnemyCoverKind.High,0f,-5.9f,index==5 ? EnemyHighCoverSide.Left : EnemyHighCoverSide.Right);
            Physics.SyncTransforms();
            EnemyBrain owner=null;
            bool taking=false,idle=false,leaving=false,shot=false,returned=false,reload=false,interrupted=false,reset=false;
            float deadline=Time.time+38f;
            float exitHipMinimum=float.PositiveInfinity, exitHipMaximum=float.NegativeInfinity, exitVerticalSpeed=0f;
            float previousHipHeight=0f, previousHipTime=0f; bool measuringExitHeight=false;
            var heightTrace=new System.Text.StringBuilder("Time,Phase,State,Normalized,Transition,RootY,ModelY,HipsY,HeadY,GroundY,VerticalSpeed\n");
            float maximumExitAimError=0f, maximumExitBodyError=0f; bool measuredExit=false;
            float maximumGripError=0f; bool reachedCorner=false;
            var skinChecks=new HashSet<int>(); bool physicalContact=false;
            Vector3 plantedLeft=Vector3.zero, plantedRight=Vector3.zero; bool measuringPlant=false; float plantedTravel=0f;
            Vector3 exitPlantedToe=Vector3.zero; bool measuringExitPlant=false, checkedExitPlant=false;
            float exitPlantTravel=0f, settledExitSpeed=0f, peakExitSpeed=0f;
            bool captureFrames=SessionState.GetBool(HighCoverCaptureKey,false);
            bool captureSide=false;
            int shots=0; bool invalidShot=false; float nextTrace=0f;
            bool reloadCompleted=false, shotAfterReload=false, targetMoved=false, released=false, followedHiddenTarget=false;
            Vector3 confirmedPosition=Vector3.zero;
            var captured = new HashSet<string>();
            var trace = new System.Text.StringBuilder("Time,Owner,Phase,State,Normalized,Speed,Ammo,Reload,RootX,RootZ,HipsYaw,MuzzleYaw,AimWeight,RootYaw,MuzzleError,BodyError\n");
            Action fired=()=>
            {
                shots++;
                if(owner.Tactics.CurrentCover==point)
                {
                    invalidShot |= owner.Tactics.CoverPhase!=EnemyCoverActionPhase.Exposed || owner.GetComponent<EnemyAnimationBridge>().IsHighCoverPose;
                    shot=true; shotAfterReload |= reloadCompleted;
                }
            };
            try
            {
                while(Time.time<deadline)
                {
                    fixture.Observe();
                    if(owner==null)
                    {
                        owner=fixture.Brains.FirstOrDefault(b=>b.Tactics.CurrentCover==point);
                        if(owner!=null) owner.Combat.Fired+=fired;
                    }
                    if(owner!=null)
                    {
                        var bridge=owner.GetComponent<EnemyAnimationBridge>(); var animator=bridge.Animator;
                        TacticalReviewBrain=owner;
                        var phase=owner.Tactics.CoverPhase;
                        var state=animator.GetCurrentAnimatorStateInfo(0);
                        if(state.IsTag("HighCover") && !animator.IsInTransition(0) && !owner.Combat.IsReloading && owner.GetComponent<EnemyRigPresenter>().HandWeight>=.999f && !HasActionPose(animator,"Reload") && !HasActionPose(animator,"Hit"))
                        {
                            var grip=owner.GetComponentInChildren<UnityEngine.Animations.Rigging.TwoBoneIKConstraint>();
                            maximumGripError=Mathf.Max(maximumGripError,Vector3.Distance(grip.data.tip.position,grip.data.target.position));
                        }
                        if(owner.TryGetComponent<EnemyHighCoverPresentation>(out var contact))
                        {
                            bool exitHeight=state.IsName("LeavingHighCover") || animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("LeavingHighCover");
                            if(exitHeight)
                            {
                                float hip=animator.GetBoneTransform(HumanBodyBones.Hips).position.y-contact.GroundHeight;
                                float speedY=measuringExitHeight && Time.time>previousHipTime ? Mathf.Abs(hip-previousHipHeight)/(Time.time-previousHipTime) : 0f;
                                exitVerticalSpeed=Mathf.Max(exitVerticalSpeed,speedY);
                                if(state.IsName("LeavingHighCover") && !animator.IsInTransition(0)) { exitHipMinimum=Mathf.Min(exitHipMinimum,hip); exitHipMaximum=Mathf.Max(exitHipMaximum,hip); }
                                heightTrace.AppendLine(FormattableString.Invariant($"{Time.time:F4},{phase},{EnemyAnimationStateNames.Get(state.fullPathHash)},{state.normalizedTime:F4},{animator.IsInTransition(0)},{owner.transform.position.y:F5},{animator.transform.position.y:F5},{hip:F5},{animator.GetBoneTransform(HumanBodyBones.Head).position.y:F5},{contact.GroundHeight:F5},{speedY:F4}"));
                                previousHipHeight=hip; previousHipTime=Time.time;
                            }
                            measuringExitHeight=exitHeight;
                            bool planted=state.IsName("TakingHighCover") && state.normalizedTime>.72f && state.normalizedTime<.94f && !animator.IsInTransition(0);
                            Vector3 left=animator.GetBoneTransform(HumanBodyBones.LeftToes).position, right=animator.GetBoneTransform(HumanBodyBones.RightToes).position;
                            if(planted && !measuringPlant) { plantedLeft=left; plantedRight=right; }
                            if(planted) plantedTravel=Mathf.Max(plantedTravel,Vector3.ProjectOnPlane(left-plantedLeft,Vector3.up).magnitude,Vector3.ProjectOnPlane(right-plantedRight,Vector3.up).magnitude);
                            measuringPlant=planted;
                            bool exitPlant=state.IsName("LeavingHighCover") && state.normalizedTime>=.96f;
                            Vector3 supportToe=point.ExposesRight?right:left;
                            if(exitPlant && !measuringExitPlant) exitPlantedToe=supportToe;
                            if(exitPlant)
                            {
                                checkedExitPlant=true;
                                exitPlantTravel=Mathf.Max(exitPlantTravel,Vector3.ProjectOnPlane(supportToe-exitPlantedToe,Vector3.up).magnitude);
                                settledExitSpeed=Mathf.Max(settledExitSpeed,owner.GetComponent<EnemyActor>().Navigation.Velocity.magnitude);
                            }
                            measuringExitPlant=exitPlant;
                            if(state.IsName("LeavingHighCover")) peakExitSpeed=Mathf.Max(peakExitSpeed,owner.GetComponent<EnemyActor>().Navigation.Velocity.magnitude);
                            if(state.IsTag("HighCover") && state.normalizedTime>.45f && !animator.IsInTransition(0) && skinChecks.Add(state.shortNameHash))
                            {
                                MeasureHighCoverSkin(owner,point,out float soleGap,out float backGap,out float edgeInset);
                                AppendResult(FormattableString.Invariant($"SKIN {EnemyAnimationStateNames.Get(state.fullPathHash)} sole={soleGap:F4} back={backGap:F4} headEdge={edgeInset:F4}"));
                                PlayCheck(Mathf.Abs(soleGap)<.025f,"Actual rendered shoe soles contact the physical floor during High Cover");
                                if(state.IsName("HighCoverIdle"))
                                {
                                    PlayCheck(backGap>=-.02f && backGap<.04f,"Actual rendered back lies against the wall within 4 cm");
                                    PlayCheck(edgeInset>.03f && edgeInset<.17f,"Actual head is inside the selected edge by about 10 cm");
                                    physicalContact=true;
                                    captureSide=captureFrames && (index==0 || index==5);
                                }
                            }
                        }
                        if(state.IsName("HighCoverIdle") && phase==EnemyCoverActionPhase.Protected)
                        {
                            float gap=Vector3.ProjectOnPlane(owner.transform.position-point.ProtectedPosition,Vector3.up).magnitude;
                            if(!reachedCorner) PlayCheck(gap<=.081f,"Protected High Cover reaches the selected corner within 8 cm");
                            reachedCorner=true;
                        }
                        taking |= state.IsName("TakingHighCover"); idle |= state.IsName("HighCoverIdle"); leaving |= state.IsName("LeavingHighCover");
                        TacticalReviewPhase=owner.Tactics.CurrentCover==point ? phase + " / " + EnemyAnimationStateNames.Get(state.fullPathHash) : "Cover released";
                        if(camera!=null)
                        {
                            camera.fieldOfView=46f;
                            camera.transform.position=point.ProtectedPosition+new Vector3(index==5?-3.5f:3.5f,1.5f,-.2f);
                            camera.transform.LookAt(point.ProtectedPosition+Vector3.up*.9f);
                            if(captureSide)
                            {
                                ScreenCapture.CaptureScreenshot(EnemyTools.Evidence+"/high-contact-side-"+index+".png");
                                captureSide=false;
                            }
                        }
                        if(Time.time>=nextTrace)
                        {
                            nextTrace=Time.time+.1f;
                            var actor=owner.GetComponent<EnemyActor>();
                            trace.AppendLine(FormattableString.Invariant($"{Time.time:F2},{owner.name},{phase},{EnemyAnimationStateNames.Get(state.fullPathHash)},{state.normalizedTime:F2},{actor.Navigation.Velocity.magnitude:F3},{owner.Combat.Ammo},{owner.Combat.IsReloading},{owner.transform.position.x:F3},{owner.transform.position.z:F3},{animator.GetBoneTransform(HumanBodyBones.Hips).eulerAngles.y:F2},{actor.Muzzle.eulerAngles.y:F2},{owner.GetComponent<EnemyRigPresenter>().AimRigWeight:F3},{owner.transform.eulerAngles.y:F2},{bridge.MuzzleAimErrorDegrees:F2},{bridge.BodyAimErrorDegrees:F2}"));
                        }
                        if(captureFrames && camera!=null && (index==0 || index==5) && !animator.IsInTransition(0) &&
                            (state.IsTag("HighCover") && state.normalizedTime>.4f || shot && phase==EnemyCoverActionPhase.Exposed))
                        {
                            string capture=state.IsTag("HighCover")?state.shortNameHash.ToString():"Fire";
                            if(captured.Add(capture)) ScreenCapture.CaptureScreenshot(EnemyTools.Evidence+"/high-cover-"+index+"-"+capture+".png");
                        }
                        if(state.IsName("LeavingHighCover") && !animator.IsInTransition(0))
                        {
                            if(state.normalizedTime>.9f && state.normalizedTime<1f)
                            {
                                measuredExit=true;
                                maximumExitAimError=Mathf.Max(maximumExitAimError,bridge.MuzzleAimErrorDegrees);
                                maximumExitBodyError=Mathf.Max(maximumExitBodyError,bridge.BodyAimErrorDegrees);
                            }
                            if(captureFrames && camera!=null && (index==0 || index==5))
                            {
                                int quarter=Mathf.Min(23,Mathf.FloorToInt(Mathf.Clamp01(state.normalizedTime)*24));
                                if(captured.Add("Exit"+quarter)) ScreenCapture.CaptureScreenshot(EnemyTools.Evidence+"/high-exit-"+index+"-"+quarter+".png");
                            }
                        }
                        returned |= shot && owner.Tactics.CurrentCover==point && phase==EnemyCoverActionPhase.Protected && state.IsName("HighCoverIdle");
                        reloadCompleted |= reload && !owner.Combat.IsReloading;
                        reload |= owner.Combat.IsReloading && owner.Tactics.CurrentCover==point && phase==EnemyCoverActionPhase.Protected;
                        bool deathMoment=index==2 && state.IsName("TakingHighCover") && state.normalizedTime>.3f || index==3 && state.IsName("LeavingHighCover") && state.normalizedTime>.3f;
                        if(!interrupted && deathMoment)
                        {
                            interrupted=true;
                            owner.GetComponent<EnemyActor>().Health.TakeDamage(new DamageInfo(100000f,owner.transform.position+Vector3.up,Vector3.back,fixture.Target.gameObject));
                            PlayCheck(owner.States.Current==EnemyStateId.Dead && owner.GetComponent<EnemyRagdollPresenter>().IsRagdoll && owner.Tactics.CurrentCover==null && fixture.Covers.Reservation(point)==null,
                                "Death from High Cover immediately owns presentation and releases cover");
                            foreach(var wait in WaitEnumerable(2f)) yield return wait;
                            break;
                        }
                        if(index==4 && !reset && idle)
                        {
                            owner.gameObject.SetActive(false);
                            PlayCheck(fixture.Covers.Reservation(point)==null,"Disabled High Cover owner releases reservation");
                            owner.gameObject.SetActive(true); owner.ResetForSpawn(); reset=true;
                            PlayCheck(owner.GetComponent<EnemyHighCoverPresentation>().AppliedOffset==Vector3.zero && owner.GetComponent<CapsuleCollider>().center==AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath).GetComponent<CapsuleCollider>().center,"Reset restores the original visual/hurtbox offsets before ordinary locomotion");
                            PlayCheck(animator.GetInteger("HighCoverPhase")==0 && !bridge.IsHighCoverPose,"Reset clears High Cover pose and parameters before the next AI tick reacquires cover");
                            SeedHighCoverContact(owner,fixture.Target);
                            yield return null;
                        }
                        if(index==6 && idle && !targetMoved)
                        {
                            confirmedPosition=owner.Memory.LastKnownPosition;
                            fixture.Target.transform.position=new Vector3(50,0,30); Physics.SyncTransforms(); targetMoved=true;
                        }
                        if(index==6 && targetMoved)
                        {
                            followedHiddenTarget |= owner.Memory.LastKnownPosition!=confirmedPosition;
                            if(owner.Tactics.CurrentCover==null && !owner.Memory.HasContact) { released=true; break; }
                        }
                        if(returned && (index==0 || index==5 || index==1 && reload && shotAfterReload || index==4 && reset)) break;
                    }
                    yield return null;
                }
                PlayCheck(owner!=null && taking,"Autonomous high cover owner plays Taking High Cover");
                if(index!=2) PlayCheck(idle,"Protected owner plays looping High Cover Idle");
                if(index!=2 && index!=4 && index!=6) PlayCheck(leaving,"Cover exit plays Leaving High Cover");
                if(index==6) PlayCheck(targetMoved && released && !followedHiddenTarget,"Lost High Cover contact expires and releases its reservation within the bounded memory/search window");
                else if(index==2 || index==3) PlayCheck(interrupted,"Requested cover action received lethal interruption");
                else
                {
                    PlayCheck(shot && shots>0 && returned,"Owner fires from a clear exposure position and physically returns to animated protection");
                    if(index==1) PlayCheck(reload && reloadCompleted && shotAfterReload,"Actual magazine depletion returns for a protected reload and resumes exposed firing afterwards");
                    if(index==4) PlayCheck(reset,"High Cover enemy survives disable/reset/reuse and a new cover cycle");
                }
                if(measuredExit)
                {
                    AppendResult(FormattableString.Invariant($"EXIT heading body={maximumExitBodyError:F2} barrel={maximumExitAimError:F2}"));
                    PlayCheck(maximumExitBodyError<5f && maximumExitAimError<20f,"High Cover finishes facing the confirmed target with its rifle already aimed before the locomotion handoff");
                }
                if(index==0 || index==5) PlayCheck(measuredExit,"Observed late High Cover exit heading on both corners");
                if(!float.IsInfinity(exitHipMinimum))
                {
                    AppendResult(FormattableString.Invariant($"EXIT vertical hip range={exitHipMaximum-exitHipMinimum:F4} m peak vertical speed={exitVerticalSpeed:F3} m/s"));
                    PlayCheck(exitHipMaximum-exitHipMinimum<.04f && exitVerticalSpeed<.15f,"Leaving High Cover retains stable pelvis height through the step and both state transitions");
                }
                if(checkedExitPlant)
                {
                    AppendResult(FormattableString.Invariant($"EXIT foot contact drift={exitPlantTravel:F4} m settledSpeed={settledExitSpeed:F3} m/s peakSpeed={peakExitSpeed:F3} m/s"));
                    PlayCheck(exitPlantTravel<.035f && settledExitSpeed<.15f && peakExitSpeed<1.8f,"Leaving High Cover settles its arriving foot and stops authored travel before ordinary locomotion resumes");
                }
                if(index==0 || index==5) PlayCheck(checkedExitPlant,"Observed the arriving-foot support transfer on both High Cover edges");
                PlayCheck(plantedTravel<.035f,"Taking High Cover planted feet stay fixed during the settled tail | maximum toe drift="+plantedTravel);
                PlayCheck(index==2 || physicalContact,"Verified rendered wall/edge/floor contact, independently of the navigation marker");
                AppendResult("High Cover maximum settled grip error: " + maximumGripError.ToString("F4") + " m");
                PlayCheck(maximumGripError<.025f,"Settled High Cover gestures keep the rifle grip within 2.5 cm (reload/hit own their arms)");
                PlayCheck(index==2 || reachedCorner,"High Cover reaches wall-adjacent corner protection");
                PlayCheck(!invalidShot,"Actual cover shots wait for exposed aiming pose");
                PlayCheck(!fixture.ProtectedShot,"Protected High Cover never fires through the barrier");
                if(index==4)
                {
                    var stance=owner.GetComponent<EnemyStance>(); stance.SetCrouching(true);
                    owner.GetComponent<EnemyHighCoverPresentation>().enabled=false;
                    PlayCheck(owner.GetComponent<CapsuleCollider>().center==stance.BodyCenterForCurrentStance && owner.GetComponent<EnemyActor>().Eyes.localPosition==stance.EyesPositionForCurrentStance,"Removing High Cover offsets preserves the active Low/crouch stance baseline");
                    owner.GetComponent<EnemyHighCoverPresentation>().enabled=true; stance.SetCrouching(false);
                    yield return null;
                    Vector3 before=AnimatorPosition(owner);
                    owner.GetComponent<EnemyActor>().Health.TakeDamage(new DamageInfo(100000f,owner.transform.position+Vector3.up,Vector3.back,fixture.Target.gameObject));
                    PlayCheck(owner.GetComponent<EnemyActor>().Health.IsDead && Vector3.Distance(before,AnimatorPosition(owner))<.001f,"Protected death preserves the anchored pose at the moment physics takes ownership");
                    foreach(var wait in WaitEnumerable(2f)) yield return wait;
                    owner.ResetForSpawn();
                    PlayCheck(owner.GetComponent<EnemyHighCoverPresentation>().AppliedOffset==Vector3.zero && owner.GetComponent<CapsuleCollider>().center==AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath).GetComponent<CapsuleCollider>().center,"Reuse after protected death restores normal visual/hurtbox offsets");
                }
                AppendResult("High Cover case PASS: " + HighCoverReviewNames[index]);
            }
            finally { if(owner!=null) owner.Combat.Fired-=fired; File.WriteAllText(EnemyTools.Evidence+"/high-cover-"+index+"-trace.csv",trace.ToString()); File.WriteAllText(EnemyTools.Evidence+"/high-vertical-"+index+".csv",heightTrace.ToString()); }
        }
    }
}