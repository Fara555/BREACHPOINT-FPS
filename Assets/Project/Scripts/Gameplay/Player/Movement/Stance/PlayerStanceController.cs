using UnityEngine;

namespace Breachpoint.Gameplay.Player.Movement.Stance
{
    public sealed class PlayerStanceController
    {
        private const float StanceThreshold = 0.01f;
        private const int ClearanceHitCapacity = 8;

        private readonly PlayerMovementConfig _config;

        private readonly RaycastHit[] _clearanceHits =
            new RaycastHit[ClearanceHitCapacity];

        private float _standingBottomLocalY;

        public bool IsCrouching { get; private set; }

        public bool IsStandingBlocked { get; private set; }

        public PlayerStanceController(
            PlayerMovementConfig config)
        {
            _config = config;
        }

        public void Initialize(
            CapsuleCollider capsuleCollider)
        {
            _standingBottomLocalY =
                _config.StandingCenterY -
                _config.StandingHeight *
                0.5f;

            ApplyColliderHeight(
                capsuleCollider,
                _config.StandingHeight);

            IsCrouching = false;
            IsStandingBlocked = false;
        }

        public void Update(
            CapsuleCollider capsuleCollider,
            Transform playerTransform,
            bool wantsToCrouch,
            bool isSliding,
            float fixedDeltaTime)
        {
            IsStandingBlocked =
                !wantsToCrouch &&
                !isSliding &&
                !CanStandUp(
                    capsuleCollider,
                    playerTransform);

            bool shouldCrouch =
                wantsToCrouch ||
                isSliding ||
                IsStandingBlocked;

            float targetHeight =
                shouldCrouch
                    ? _config.CrouchingHeight
                    : _config.StandingHeight;

            float newHeight =
                Mathf.MoveTowards(
                    capsuleCollider.height,
                    targetHeight,
                    _config.StanceTransitionSpeed *
                    fixedDeltaTime);

            ApplyColliderHeight(
                capsuleCollider,
                newHeight);

            IsCrouching =
                shouldCrouch ||
                Mathf.Abs(
                    capsuleCollider.height -
                    _config.StandingHeight) >
                StanceThreshold;
        }

        private void ApplyColliderHeight(
            CapsuleCollider capsuleCollider,
            float height)
        {
            float minimumHeight =
                capsuleCollider.radius * 2f;

            float validHeight =
                Mathf.Max(
                    height,
                    minimumHeight);

            capsuleCollider.height =
                validHeight;

            Vector3 center =
                capsuleCollider.center;

            center.y =
                _standingBottomLocalY +
                validHeight * 0.5f;

            capsuleCollider.center =
                center;
        }

        private bool CanStandUp(
            CapsuleCollider capsuleCollider,
            Transform playerTransform)
        {
            float currentHeight =
                capsuleCollider.height;

            float standingHeight =
                _config.StandingHeight;

            if (currentHeight >=
                standingHeight -
                StanceThreshold)
            {
                return true;
            }

            float radius =
                GetWorldCapsuleRadius(
                    capsuleCollider,
                    playerTransform);

            radius =
                Mathf.Max(
                    0.01f,
                    radius -
                    _config.ClearancePadding);

            Vector3 currentTopSphereCenter =
                GetTopSphereCenter(
                    capsuleCollider,
                    playerTransform,
                    currentHeight,
                    capsuleCollider.center.y,
                    radius);

            float standingCenterY =
                _standingBottomLocalY +
                standingHeight * 0.5f;

            Vector3 standingTopSphereCenter =
                GetTopSphereCenter(
                    capsuleCollider,
                    playerTransform,
                    standingHeight,
                    standingCenterY,
                    radius);

            Vector3 castOffset =
                standingTopSphereCenter -
                currentTopSphereCenter;

            float castDistance =
                castOffset.magnitude;

            if (castDistance <=
                Mathf.Epsilon)
            {
                return true;
            }

            Vector3 castDirection =
                castOffset /
                castDistance;

            int hitCount =
                Physics.SphereCastNonAlloc(
                    currentTopSphereCenter,
                    radius,
                    castDirection,
                    _clearanceHits,
                    castDistance,
                    _config.ClearanceMask,
                    QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider =
                    _clearanceHits[i].collider;

                if (hitCollider == null ||
                    hitCollider ==
                    capsuleCollider ||
                    hitCollider.transform
                        .IsChildOf(playerTransform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static Vector3 GetTopSphereCenter(
            CapsuleCollider capsuleCollider,
            Transform playerTransform,
            float localHeight,
            float localCenterY,
            float worldRadius)
        {
            Vector3 localCenter =
                capsuleCollider.center;

            localCenter.y =
                localCenterY;

            Vector3 worldCenter =
                playerTransform.TransformPoint(
                    localCenter);

            float verticalScale =
                Mathf.Abs(
                    playerTransform.lossyScale.y);

            float worldHeight =
                localHeight *
                verticalScale;

            float topOffset =
                Mathf.Max(
                    0f,
                    worldHeight * 0.5f -
                    worldRadius);

            return
                worldCenter +
                playerTransform.up *
                topOffset;
        }

        private static float GetWorldCapsuleRadius(
            CapsuleCollider capsuleCollider,
            Transform playerTransform)
        {
            Vector3 scale =
                playerTransform.lossyScale;

            float horizontalScale =
                Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.z));

            return
                capsuleCollider.radius *
                horizontalScale;
        }
    }
}