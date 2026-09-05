using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Breachpoint.Gameplay.Weapons
{
    public sealed class PlayerWeaponView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private PlayerWeaponController _weaponController;

        [SerializeField]
        private GameObject _armsRoot;

        [SerializeField]
        private Animator _armsAnimator;

        [SerializeField]
        private WeaponView[] _weaponViews;

        [SerializeField, Min(0)]
        private int _startingViewIndex;

        private PlayableGraph _reloadGraph;
        private WeaponView _currentView;
        private Transform _weaponAnimatorRoot;
        private Vector3 _weaponAnimatorRootPosition;
        private Quaternion _weaponAnimatorRootRotation;
        private Vector3 _weaponAnimatorRootScale;
        private bool _hasWeaponAnimatorRootPose;

        public WeaponView CurrentView => _currentView;

        private void Awake()
        {
            ResolveReferences();
            ValidateReferences();

            if (_armsRoot != null)
            {
                _armsRoot.SetActive(true);
            }

            EquipStartingView();
        }

        private void OnEnable()
        {
            if (_weaponController == null)
            {
                return;
            }

            _weaponController.ReloadStarted += HandleReloadStarted;
            _weaponController.ReloadCompleted += HandleReloadCompleted;
        }

        private void OnDisable()
        {
            if (_weaponController != null)
            {
                _weaponController.ReloadStarted -= HandleReloadStarted;
                _weaponController.ReloadCompleted -= HandleReloadCompleted;
            }

            StopReloadAnimation();
        }

        private void LateUpdate()
        {
            if (_reloadGraph.IsValid())
            {
                RestoreWeaponAnimatorRootPose();
            }
        }

        public void Equip(WeaponView weaponView)
        {
            if (weaponView == null || weaponView == _currentView)
            {
                return;
            }

            StopReloadAnimation();

            if (_weaponViews != null)
            {
                foreach (WeaponView view in _weaponViews)
                {
                    if (view != null)
                    {
                        view.SetVisible(view == weaponView);
                    }
                }
            }

            _currentView = weaponView;
        }

        private void EquipStartingView()
        {
            if (_weaponViews == null || _weaponViews.Length == 0)
            {
                return;
            }

            int index = Mathf.Clamp(
                _startingViewIndex,
                0,
                _weaponViews.Length - 1);

            Equip(_weaponViews[index]);
        }

        private void HandleReloadStarted(float duration)
        {
            if (_currentView == null ||
                _currentView.Animations == null)
            {
                return;
            }

            WeaponViewAnimationSet animations =
                _currentView.Animations;

            if (!animations.HasReloadAnimation)
            {
                return;
            }

            StopReloadAnimation();
            CaptureWeaponAnimatorRootPose();

            _reloadGraph = PlayableGraph.Create(
                $"{name} Reload Animation");
            _reloadGraph.SetTimeUpdateMode(
                DirectorUpdateMode.GameTime);

            AddClip(
                _armsAnimator,
                animations.ArmsReload,
                duration,
                "Arms Reload");

            AddClip(
                _currentView.WeaponAnimator,
                animations.WeaponReload,
                duration,
                "Weapon Reload");

            _reloadGraph.Play();
        }

        private void HandleReloadCompleted()
        {
            StopReloadAnimation();
        }

        private void AddClip(
            Animator animator,
            AnimationClip clip,
            float duration,
            string outputName)
        {
            if (animator == null || clip == null)
            {
                return;
            }

            AnimationClipPlayable clipPlayable =
                AnimationClipPlayable.Create(
                    _reloadGraph,
                    clip);

            double playbackSpeed = duration > 0f
                ? clip.length / duration
                : 1d;

            clipPlayable.SetSpeed(playbackSpeed);

            AnimationPlayableOutput output =
                AnimationPlayableOutput.Create(
                    _reloadGraph,
                    outputName,
                    animator);

            output.SetSourcePlayable(clipPlayable);
        }

        private void StopReloadAnimation()
        {
            if (_reloadGraph.IsValid())
            {
                _reloadGraph.Destroy();
            }

            ResetAnimator(_armsAnimator);

            if (_currentView != null)
            {
                ResetAnimator(_currentView.WeaponAnimator);
            }

            RestoreWeaponAnimatorRootPose();
            ClearWeaponAnimatorRootPose();
        }

        private void CaptureWeaponAnimatorRootPose()
        {
            Animator weaponAnimator =
                _currentView != null
                    ? _currentView.WeaponAnimator
                    : null;

            if (weaponAnimator == null)
            {
                return;
            }

            _weaponAnimatorRoot = weaponAnimator.transform;
            _weaponAnimatorRootPosition =
                _weaponAnimatorRoot.localPosition;
            _weaponAnimatorRootRotation =
                _weaponAnimatorRoot.localRotation;
            _weaponAnimatorRootScale =
                _weaponAnimatorRoot.localScale;
            _hasWeaponAnimatorRootPose = true;
        }

        private void RestoreWeaponAnimatorRootPose()
        {
            if (!_hasWeaponAnimatorRootPose ||
                _weaponAnimatorRoot == null)
            {
                return;
            }

            _weaponAnimatorRoot.SetLocalPositionAndRotation(
                _weaponAnimatorRootPosition,
                _weaponAnimatorRootRotation);
            _weaponAnimatorRoot.localScale =
                _weaponAnimatorRootScale;
        }

        private void ClearWeaponAnimatorRootPose()
        {
            _weaponAnimatorRoot = null;
            _hasWeaponAnimatorRootPose = false;
        }

        private static void ResetAnimator(Animator animator)
        {
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            animator.Rebind();
            animator.Update(0f);
        }

        private void OnValidate()
        {
            ResolveReferences();
            ValidateReferences();
        }

        private void ResolveReferences()
        {
            if (_armsAnimator == null && _armsRoot != null)
            {
                _armsAnimator =
                    _armsRoot.GetComponentInChildren<Animator>(true);
            }
        }

        private void ValidateReferences()
        {
            if (_weaponController == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerWeaponView)} requires a PlayerWeaponController reference.",
                    this);
            }

            if (_armsRoot == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerWeaponView)} requires an ArmsRoot reference.",
                    this);
            }

            if (_armsAnimator == null)
            {
                Debug.LogError(
                    $"{nameof(PlayerWeaponView)} requires an ArmsAnimator reference.",
                    this);
            }

            if (_weaponViews == null || _weaponViews.Length == 0)
            {
                Debug.LogError(
                    $"{nameof(PlayerWeaponView)} requires at least one weapon view.",
                    this);
            }
        }
    }
}
