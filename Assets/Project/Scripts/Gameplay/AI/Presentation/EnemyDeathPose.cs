using System.Collections.Generic;
using UnityEngine;

namespace Breachpoint.Gameplay.AI
{
    // A transform-only target hierarchy. It never animates the physical skeleton,
    // renders a second character, or instantiates gameplay/physics components.
    internal sealed class EnemyDeathPose
    {
        private readonly Transform[] _source;
        private readonly Transform[] _target;
        private readonly Dictionary<Transform, Transform> _mapping;
        private readonly AnimatorControllerParameter[] _parameters;
        private readonly GameObject _root;
        private readonly List<AnimatorClipInfo> _clipInfo = new List<AnimatorClipInfo>(8);
        public Animator Animator { get; }
        private const float HitBlendIn = .06f;
        private float _hitElapsed;
        public bool IsMovingHit { get; private set; }
        public AnimationClip HitClip { get; private set; }
        public float HitClipWeight { get; private set; }
        public AnimationClip DeathClip { get; private set; }
        public float DeathClipWeight { get; private set; }

        public EnemyDeathPose(Animator source)
        {
            _mapping = new Dictionary<Transform, Transform>();
            _source = source.GetComponentsInChildren<Transform>(true);
            _target = new Transform[_source.Length];
            _root = new GameObject("Death Pose Target");
            _root.SetActive(false);
            _root.transform.SetParent(source.transform.parent, false);
            _root.transform.SetLocalPositionAndRotation(source.transform.localPosition, source.transform.localRotation);
            _root.transform.localScale = source.transform.localScale;
            _mapping.Add(source.transform, _root.transform);
            _target[0] = _root.transform;
            for (int i = 1; i < _source.Length; i++)
            {
                Transform target = new GameObject(_source[i].name).transform;
                target.SetParent(_mapping[_source[i].parent], false);
                target.SetLocalPositionAndRotation(_source[i].localPosition, _source[i].localRotation);
                target.localScale = _source[i].localScale;
                _mapping.Add(_source[i], target);
                _target[i] = target;
            }
            Animator = _root.AddComponent<Animator>();
            Animator.avatar = source.avatar;
            Animator.runtimeAnimatorController = source.runtimeAnimatorController;
            Animator.applyRootMotion = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Animator.enabled = false; // Evaluated exclusively by the physics controller.
            _parameters = source.parameters;
        }

        public Transform TargetFor(Transform source) => _mapping[source];

        public void Begin(Animator source, bool stationary, float hitDirection, bool movingHit = false)
        {
            ResetSnapshot();
            _root.SetActive(true);
            Animator.Rebind();
            foreach (var parameter in _parameters)
            {
                int hash = parameter.nameHash;
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float: Animator.SetFloat(hash, source.GetFloat(hash)); break;
                    case AnimatorControllerParameterType.Int: Animator.SetInteger(hash, source.GetInteger(hash)); break;
                    case AnimatorControllerParameterType.Bool: Animator.SetBool(hash, source.GetBool(hash)); break;
                    case AnimatorControllerParameterType.Trigger: Animator.ResetTrigger(hash); break;
                }
            }
            Animator.SetBool("IsDead", stationary);
            Animator.SetBool("IsFiring", false);
            Animator.SetBool("IsReloading", false);
            Animator.SetBool("IsHit", false);
            Animator.SetFloat("HitDirection", hitDirection);
            var state = source.GetCurrentAnimatorStateInfo(0);
            var next = source.GetNextAnimatorStateInfo(0);
            if (!stationary && source.IsInTransition(0) && next.IsTag("Locomotion")) state = next;
            if (!stationary && !state.IsTag("Locomotion"))
                Animator.Play(Animator.GetBool("IsCrouching") ? "Base Layer.CrouchLocomotion" : "Base Layer.StandingLocomotion", 0, 0f);
            else Animator.Play(state.fullPathHash, 0, state.normalizedTime);
            for (int layer = 1; layer < Animator.layerCount; layer++) Animator.SetLayerWeight(layer, 0f);
            if (!stationary && movingHit && Animator.layerCount > 1)
            {
                int hitState = UnityEngine.Animator.StringToHash(Animator.GetBool("IsCrouching") ? "Actions.CrouchHit" : "Actions.Hit");
                IsMovingHit = Animator.HasState(1, hitState);
                if (IsMovingHit)
                {
                    // The existing upper-body mask keeps the captured locomotion
                    // on the legs. No visible Animator writes physical bones.
                    Animator.SetBool("IsHit", true);
                    float reactionDirection = hitDirection == 3f ? 0f : hitDirection;
                    // CrouchHit's existing tree is right / centre / left, whereas
                    // standing Hit is centre / left / right. Keep that asset intact.
                    if (Animator.GetBool("IsCrouching")) reactionDirection = reactionDirection == 1f ? 2f : reactionDirection == 2f ? 0f : 1f;
                    Animator.SetFloat("HitDirection", reactionDirection);
                    Animator.Play(hitState, 1, 0f);
                }
            }
            Animator.Update(0f);
            // The first motor target is the evaluated pose, including live rigging.
            // Subsequent manual evaluations supply animation rotations only.
            for (int i = 0; i < _source.Length; i++)
                _target[i].SetLocalPositionAndRotation(_source[i].localPosition, _source[i].localRotation);
        }

        public void Evaluate(float deltaTime, float playbackSpeed)
        {
            if (IsMovingHit)
            {
                _hitElapsed += deltaTime;
                Animator.SetLayerWeight(1, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_hitElapsed / HitBlendIn)));
            }
            Animator.speed = playbackSpeed;
            Animator.Update(deltaTime);
            if (IsMovingHit && Animator.GetCurrentAnimatorStateInfo(1).IsTag("Hit"))
            {
                HitClipWeight = 0f;
                Animator.GetCurrentAnimatorClipInfo(1, _clipInfo);
                foreach (var clip in _clipInfo)
                    if (clip.weight > HitClipWeight) { HitClip = clip.clip; HitClipWeight = clip.weight; }
            }
            if (Animator.GetCurrentAnimatorStateInfo(0).IsTag("Death"))
            {
                Animator.GetCurrentAnimatorClipInfo(0, _clipInfo);
                foreach (var clip in _clipInfo)
                    if (clip.weight > DeathClipWeight) { DeathClip = clip.clip; DeathClipWeight = clip.weight; }
            }
        }

        public void FollowHorizontalPosition(Vector3 physicalPelvis, Vector3 targetPelvis)
        {
            _root.transform.position += Vector3.ProjectOnPlane(physicalPelvis - targetPelvis, Vector3.up);
        }

        public void Stop() => _root.SetActive(false);
        public void ResetSnapshot() { DeathClip = HitClip = null; DeathClipWeight = HitClipWeight = _hitElapsed = 0f; IsMovingHit = false; }
    }
}
