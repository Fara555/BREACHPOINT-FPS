using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Breachpoint.Gameplay.Weapons
{
    [DisallowMultipleComponent]
    public sealed class FirstPersonWeaponRenderer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private Camera _worldCamera;

        [SerializeField]
        private Camera _weaponCamera;

        [Header("Rendering")]
        [SerializeField]
        private LayerMask _weaponLayer = 1 << 7;

        [SerializeField, Min(0.01f)]
        private float _nearClipPlane = 0.01f;

        [SerializeField, Min(0.1f)]
        private float _farClipPlane = 10f;

        private int _originalCullingMask;
        private bool _isWorldCameraMaskApplied;

        private void OnEnable()
        {
            ResolveReferences();
            ValidateReferences();

            if (_worldCamera == null ||
                _weaponCamera == null ||
                _weaponLayer.value == 0)
            {
                return;
            }

            ConfigureWeaponCamera();
            ApplyWorldCameraCullingMask();
        }

        private void LateUpdate()
        {
            if (_worldCamera == null || _weaponCamera == null)
            {
                return;
            }

            _weaponCamera.fieldOfView = _worldCamera.fieldOfView;
            _weaponCamera.rect = _worldCamera.rect;
        }

        private void OnDisable()
        {
            RestoreWorldCameraCullingMask();

            if (_weaponCamera != null)
            {
                _weaponCamera.enabled = false;
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void ConfigureWeaponCamera()
        {
            _weaponCamera.CopyFrom(_worldCamera);
            _weaponCamera.enabled = true;
            _weaponCamera.depth = _worldCamera.depth + 1f;
            _weaponCamera.clearFlags = CameraClearFlags.Depth;
            _weaponCamera.cullingMask = _weaponLayer;
            _weaponCamera.nearClipPlane = _nearClipPlane;
            _weaponCamera.farClipPlane = _farClipPlane;
            _weaponCamera.useOcclusionCulling = false;

            ConfigureHdrpCamera();
        }

        private void ConfigureHdrpCamera()
        {
            HDAdditionalCameraData weaponCameraData =
                _weaponCamera.GetComponent<HDAdditionalCameraData>();

            if (weaponCameraData == null)
            {
                Debug.LogError(
                    $"{nameof(FirstPersonWeaponRenderer)} requires " +
                    $"{nameof(HDAdditionalCameraData)} on WeaponCamera.",
                    _weaponCamera);
                return;
            }

            HDAdditionalCameraData worldCameraData =
                _worldCamera.GetComponent<HDAdditionalCameraData>();

            weaponCameraData.clearColorMode =
                HDAdditionalCameraData.ClearColorMode.None;
            weaponCameraData.clearDepth = true;
            weaponCameraData.volumeAnchorOverride =
                _worldCamera.transform;

            if (worldCameraData != null)
            {
                weaponCameraData.volumeLayerMask =
                    worldCameraData.volumeLayerMask;
                weaponCameraData.allowDynamicResolution =
                    worldCameraData.allowDynamicResolution;
            }
        }

        private void ApplyWorldCameraCullingMask()
        {
            if (_isWorldCameraMaskApplied)
            {
                return;
            }

            _originalCullingMask = _worldCamera.cullingMask;
            _worldCamera.cullingMask &= ~_weaponLayer.value;
            _isWorldCameraMaskApplied = true;
        }

        private void RestoreWorldCameraCullingMask()
        {
            if (!_isWorldCameraMaskApplied || _worldCamera == null)
            {
                return;
            }

            _worldCamera.cullingMask = _originalCullingMask;
            _isWorldCameraMaskApplied = false;
        }

        private void ResolveReferences()
        {
            if (_worldCamera == null)
            {
                _worldCamera = GetComponent<Camera>();
            }
        }

        private void ValidateReferences()
        {
            if (_worldCamera == null)
            {
                Debug.LogError(
                    $"{nameof(FirstPersonWeaponRenderer)} requires a WorldCamera reference.",
                    this);
            }

            if (_weaponCamera == null)
            {
                Debug.LogError(
                    $"{nameof(FirstPersonWeaponRenderer)} requires a WeaponCamera reference.",
                    this);
            }

            if (_weaponLayer.value == 0)
            {
                Debug.LogError(
                    $"{nameof(FirstPersonWeaponRenderer)} requires a WeaponLayer.",
                    this);
            }
        }
    }
}
