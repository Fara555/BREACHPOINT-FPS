using UnityEngine;
using UnityEngine.Rendering;

namespace Breachpoint.Gameplay.Weapons.Effects
{
    public sealed class WeaponEnergyArcVfx : MonoBehaviour
    {
        [Header("Anchors")]
        [SerializeField]
        private Transform _source;

        [SerializeField]
        private Transform _target;

        [SerializeField]
        private Vector3 _sourceLocalOffset;

        [SerializeField]
        private Vector3 _targetLocalOffset = new Vector3(0f, 0.035f, 0f);

        [Tooltip("Optional per-route sources. Each primary arc uses a separate entry.")]
        [SerializeField]
        private Transform[] _sourceAnchors;

        [SerializeField]
        private Vector3[] _sourceAnchorOffsets;

        [Tooltip("Projects each source route onto the surface facing its target. " +
            "Offset X/Y spread routes across the shell; Z adjusts its radius.")]
        [SerializeField]
        private bool _projectSourcesToSurface;

        [SerializeField, Min(0f)]
        private float _sourceSurfaceRadius = 0.5f;

        [SerializeField, Range(0.15f, 0.6f)]
        private float _surfaceWrapPortion = 0.34f;

        [SerializeField, Range(0.35f, 0.95f)]
        private float _surfaceExitProgress = 0.78f;

        [SerializeField, Range(0f, 0.15f)]
        private float _surfaceArcClearance = 0.025f;

        [SerializeField, Range(0f, 1.5f)]
        private float _pathSeparation = 0.65f;

        [Tooltip("Optional per-route targets. A single target can be combined with several offsets.")]
        [SerializeField]
        private Transform[] _targetAnchors;

        [SerializeField]
        private Vector3[] _targetAnchorOffsets;

        [Header("Rendering")]
        [SerializeField]
        private Material _glowMaterial;

        [SerializeField]
        private Material _coreMaterial;

        [SerializeField, Range(1, 8)]
        private int _primaryArcCount = 3;

        [SerializeField, Range(0, 4)]
        private int _branchArcCount = 2;

        [SerializeField, Range(8, 32)]
        private int _segmentCount = 18;

        [SerializeField, Min(0.0001f)]
        private float _coreWidth = 0.0035f;

        [SerializeField, Min(0.0001f)]
        private float _glowWidth = 0.013f;

        [Header("Energy Flow Accents")]
        [SerializeField, Range(0, 8)]
        private int _travelingPulseCount = 4;

        [SerializeField, Range(3, 8)]
        private int _travelingPulseSegmentCount = 5;

        [SerializeField, Min(0.01f)]
        private float _travelingPulseSpeed = 1.35f;

        [SerializeField, Range(0.02f, 0.2f)]
        private float _travelingPulseLength = 0.075f;

        [SerializeField, Range(1f, 4f)]
        private float _travelingPulseWidthMultiplier = 2.15f;

        [SerializeField, Range(1f, 3f)]
        private float _travelingPulseGlowWidthMultiplier = 1.65f;

        [SerializeField, Range(0, 4)]
        private int _contactFlashCount = 3;

        [SerializeField, Range(1, 4)]
        private int _contactFlashRayCount = 3;

        [SerializeField, Min(0.001f)]
        private float _contactFlashRadius = 0.014f;

        [SerializeField, Min(0.1f)]
        private float _contactFlashRate = 4.2f;

        [Header("Motion")]
        [SerializeField, Min(0f)]
        private float _noiseAmplitude = 0.018f;

        [SerializeField, Min(0.1f)]
        private float _noiseFrequency = 7.5f;

        [SerializeField, Min(0f)]
        private float _pulseSpeed = 9f;

        [SerializeField, Range(0f, 1f)]
        private float _flicker = 0.22f;

        [Header("Envelope")]
        [SerializeField, Min(0f)]
        private float _fadeInDuration = 0.12f;

        private ArcPair[] _primaryArcs;
        private ArcPair[] _branchArcs;
        private TravelingPulse[] _travelingPulses;
        private ContactFlash[] _contactFlashes;
        private float _elapsed;
        private float _visibility;
        private float _fadeOutDuration;
        private float _fadeOutElapsed;
        private bool _isPlaying;
        private bool _isFadingOut;

        private void Awake()
        {
            BuildRenderers();
            StopEffect();
        }

        private void OnDisable()
        {
            StopEffect();
        }

        private void LateUpdate()
        {
            if (!_isPlaying || !HasValidRouteAnchors())
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            _elapsed += deltaTime;

            UpdateVisibility(deltaTime);
            UpdateWidths();
            RebuildArcs();
            UpdateTravelingPulses();
            UpdateContactFlashes();
        }

        public void PlayEffect()
        {
            if (_primaryArcs == null)
            {
                BuildRenderers();
            }

            if (_primaryArcs == null ||
                !HasValidRouteAnchors())
            {
                return;
            }

            _elapsed = 0f;
            _visibility = _fadeInDuration <= 0f ? 1f : 0f;
            _fadeOutElapsed = 0f;
            _isFadingOut = false;
            _isPlaying = true;
            SetRenderersEnabled(true);
            RebuildArcs();
            UpdateWidths();
            UpdateTravelingPulses();
            UpdateContactFlashes();
        }

        public void BeginFadeOut(float duration)
        {
            if (!_isPlaying)
            {
                return;
            }

            _fadeOutDuration = Mathf.Max(0f, duration);
            _fadeOutElapsed = 0f;
            _isFadingOut = true;

            if (_fadeOutDuration <= 0f)
            {
                StopEffect();
            }
        }

        public void StopEffect()
        {
            _isPlaying = false;
            _isFadingOut = false;
            _visibility = 0f;
            SetRenderersEnabled(false);
            SetAccentRenderersEnabled(false);
        }

        private void BuildRenderers()
        {
            if (_glowMaterial == null || _coreMaterial == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponEnergyArcVfx)} requires glow and core materials.",
                    this);
                return;
            }

            _primaryArcs = new ArcPair[_primaryArcCount];

            for (int i = 0; i < _primaryArcs.Length; i++)
            {
                _primaryArcs[i] = CreateArcPair(
                    $"Primary_{i:00}",
                    _segmentCount,
                    false);
            }

            int branchSegments = Mathf.Max(6, _segmentCount / 2);
            _branchArcs = new ArcPair[_branchArcCount];

            for (int i = 0; i < _branchArcs.Length; i++)
            {
                _branchArcs[i] = CreateArcPair(
                    $"Branch_{i:00}",
                    branchSegments,
                    true);
            }

            BuildTravelingPulses();
            BuildContactFlashes();
        }

        private ArcPair CreateArcPair(
            string arcName,
            int positionCount,
            bool isBranch)
        {
            GameObject arcRoot = new(arcName);
            arcRoot.layer = gameObject.layer;
            arcRoot.transform.SetParent(transform, false);

            LineRenderer glow = CreateRenderer(
                arcRoot,
                "Glow",
                _glowMaterial,
                isBranch ? _glowWidth * 0.72f : _glowWidth,
                positionCount);

            LineRenderer core = CreateRenderer(
                arcRoot,
                "Core",
                _coreMaterial,
                isBranch ? _coreWidth * 0.65f : _coreWidth,
                positionCount);

            return new ArcPair(
                glow,
                core,
                new Vector3[positionCount],
                isBranch);
        }

        private LineRenderer CreateRenderer(
            GameObject parent,
            string rendererName,
            Material material,
            float width,
            int positionCount)
        {
            LineRenderer renderer = CreateLineRenderer(
                parent,
                rendererName,
                material,
                positionCount);
            renderer.widthMultiplier = width;
            renderer.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.14f),
                new Keyframe(0.06f, 1.08f),
                new Keyframe(0.84f, 0.95f),
                new Keyframe(1f, 0.12f));
            return renderer;
        }

        private LineRenderer CreateLineRenderer(
            GameObject parent,
            string rendererName,
            Material material,
            int positionCount)
        {
            GameObject rendererObject = new(rendererName);
            rendererObject.layer = gameObject.layer;
            rendererObject.transform.SetParent(parent.transform, false);

            LineRenderer renderer = rendererObject.AddComponent<LineRenderer>();
            renderer.enabled = false;
            renderer.useWorldSpace = true;
            renderer.alignment = LineAlignment.View;
            renderer.textureMode = LineTextureMode.Stretch;
            renderer.positionCount = positionCount;
            renderer.numCapVertices = 3;
            renderer.numCornerVertices = 3;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }

        private void BuildTravelingPulses()
        {
            _travelingPulses = new TravelingPulse[_travelingPulseCount];

            for (int i = 0; i < _travelingPulses.Length; i++)
            {
                GameObject pulseRoot = new($"FlowPulse_{i:00}");
                pulseRoot.layer = gameObject.layer;
                pulseRoot.transform.SetParent(transform, false);
                LineRenderer glow = CreateLineRenderer(
                    pulseRoot,
                    "Glow",
                    _glowMaterial,
                    _travelingPulseSegmentCount);
                LineRenderer core = CreateLineRenderer(
                    pulseRoot,
                    "Core",
                    _coreMaterial,
                    _travelingPulseSegmentCount);
                AnimationCurve pulseWidthCurve = new(
                    new Keyframe(0f, 0f),
                    new Keyframe(0.62f, 0.68f),
                    new Keyframe(0.86f, 1f),
                    new Keyframe(1f, 0.18f));
                glow.widthCurve = pulseWidthCurve;
                core.widthCurve = pulseWidthCurve;
                _travelingPulses[i] = new TravelingPulse(
                    glow,
                    core,
                    new Vector3[_travelingPulseSegmentCount],
                    i % _primaryArcCount,
                    i / (float)Mathf.Max(1, _travelingPulseCount));
            }
        }

        private void BuildContactFlashes()
        {
            _contactFlashes = new ContactFlash[_contactFlashCount];

            for (int i = 0; i < _contactFlashes.Length; i++)
            {
                GameObject flashRoot = new($"ContactFlash_{i:00}");
                flashRoot.layer = gameObject.layer;
                flashRoot.transform.SetParent(transform, false);
                AccentPair[] rays = new AccentPair[_contactFlashRayCount];

                for (int rayIndex = 0; rayIndex < rays.Length; rayIndex++)
                {
                    GameObject rayRoot = new($"Ray_{rayIndex:00}");
                    rayRoot.layer = gameObject.layer;
                    rayRoot.transform.SetParent(flashRoot.transform, false);
                    LineRenderer glow = CreateLineRenderer(
                        rayRoot,
                        "Glow",
                        _glowMaterial,
                        2);
                    LineRenderer core = CreateLineRenderer(
                        rayRoot,
                        "Core",
                        _coreMaterial,
                        2);
                    AnimationCurve rayWidthCurve = new(
                        new Keyframe(0f, 1f),
                        new Keyframe(1f, 0f));
                    glow.widthCurve = rayWidthCurve;
                    core.widthCurve = rayWidthCurve;
                    rays[rayIndex] = new AccentPair(glow, core);
                }

                _contactFlashes[i] = new ContactFlash(
                    rays,
                    i / (float)Mathf.Max(1, _contactFlashCount));
            }
        }

        private void RebuildArcs()
        {
            SetRenderersEnabled(true);

            for (int i = 0; i < _primaryArcs.Length; i++)
            {
                ArcPair arc = _primaryArcs[i];

                if (!TryGetRoute(
                        i,
                        out Vector3 arcStart,
                        out Vector3 arcEnd,
                        out Transform sourceAnchor) ||
                    !TryBuildBasis(arcStart, arcEnd, out Vector3 side, out Vector3 up))
                {
                    arc.SetEnabled(false);
                    continue;
                }

                float seed = 17.31f + i * 9.73f;
                float amplitude = _noiseAmplitude * (0.82f + i * 0.11f);

                if (_projectSourcesToSurface)
                {
                    FillWrappedArc(
                        arc.Positions,
                        sourceAnchor.position,
                        arcStart,
                        arcEnd,
                        side,
                        up,
                        seed,
                        amplitude);
                }
                else
                {
                    FillArc(
                        arc.Positions,
                        arcStart,
                        arcEnd,
                        side,
                        up,
                        seed,
                        amplitude);
                }

                arc.ApplyPositions();
            }

            for (int i = 0; i < _branchArcs.Length; i++)
            {
                ArcPair parentArc = _primaryArcs[i % _primaryArcs.Length];
                int startIndex = Mathf.Clamp(
                    Mathf.RoundToInt((parentArc.Positions.Length - 1) * (0.28f + i * 0.08f)),
                    1,
                    parentArc.Positions.Length - 2);
                int endIndex = Mathf.Clamp(
                    Mathf.RoundToInt((parentArc.Positions.Length - 1) * (0.7f + i * 0.06f)),
                    startIndex + 2,
                    parentArc.Positions.Length - 1);
                Vector3 branchStart = parentArc.Positions[startIndex];
                Vector3 branchEnd = parentArc.Positions[endIndex];

                if (!TryBuildBasis(
                        branchStart,
                        branchEnd,
                        out Vector3 branchSide,
                        out Vector3 branchUp))
                {
                    _branchArcs[i].SetEnabled(false);
                    continue;
                }

                branchEnd += branchSide * ((i & 1) == 0 ? 0.008f : -0.008f);
                FillArc(
                    _branchArcs[i].Positions,
                    branchStart,
                    branchEnd,
                    branchSide,
                    branchUp,
                    61.17f + i * 13.91f,
                    _noiseAmplitude * 0.72f);
                _branchArcs[i].ApplyPositions();
            }
        }

        private void UpdateTravelingPulses()
        {
            if (_travelingPulses == null || _primaryArcs.Length == 0)
            {
                return;
            }

            for (int i = 0; i < _travelingPulses.Length; i++)
            {
                TravelingPulse pulse = _travelingPulses[i];
                ArcPair route = _primaryArcs[
                    pulse.RouteIndex % _primaryArcs.Length];
                float progress = Mathf.Repeat(
                    _elapsed * _travelingPulseSpeed + pulse.PhaseOffset,
                    1.15f) - 0.075f;

                if (!route.Core.enabled || progress < 0f || progress > 1f)
                {
                    pulse.SetEnabled(false);
                    continue;
                }

                float trailStart = Mathf.Max(
                    0f,
                    progress - _travelingPulseLength);
                int lastIndex = pulse.Positions.Length - 1;

                for (int positionIndex = 0;
                    positionIndex < pulse.Positions.Length;
                    positionIndex++)
                {
                    float pulseT = positionIndex / (float)lastIndex;
                    float routeT = Mathf.Lerp(
                        trailStart,
                        progress,
                        pulseT);
                    pulse.Positions[positionIndex] = SamplePolyline(
                        route.Positions,
                        routeT);
                }

                float edgeFade = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0f, 0.08f, progress)) *
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(1f, 0.88f, progress));
                float activity = Mathf.Lerp(
                    0.82f,
                    1.16f,
                    Mathf.PerlinNoise(
                        31.7f + i * 7.13f,
                        _elapsed * 11.4f));
                float pulseCoreWidth = _coreWidth *
                    _travelingPulseWidthMultiplier *
                    _visibility * edgeFade * activity;
                float pulseGlowWidth = _glowWidth *
                    _travelingPulseGlowWidthMultiplier *
                    _visibility * edgeFade * activity;
                pulse.Core.widthMultiplier = pulseCoreWidth;
                pulse.Glow.widthMultiplier = pulseGlowWidth;
                pulse.SetEnabled(pulseCoreWidth > 0.00001f);
                pulse.Core.SetPositions(pulse.Positions);
                pulse.Glow.SetPositions(pulse.Positions);
            }
        }

        private void UpdateContactFlashes()
        {
            if (_contactFlashes == null || _primaryArcs.Length == 0)
            {
                return;
            }

            for (int i = 0; i < _contactFlashes.Length; i++)
            {
                ContactFlash flash = _contactFlashes[i];
                float cycleTime = _elapsed * _contactFlashRate +
                    flash.PhaseOffset;
                int cycleIndex = Mathf.FloorToInt(cycleTime);
                float cycleProgress = cycleTime - cycleIndex;
                const float visiblePortion = 0.44f;

                if (cycleProgress > visiblePortion)
                {
                    flash.SetEnabled(false);
                    continue;
                }

                int routeIndex = PositiveModulo(
                    cycleIndex * 3 + i * 2,
                    _primaryArcs.Length);
                ArcPair route = _primaryArcs[routeIndex];

                if (!route.Core.enabled || route.Positions.Length < 2)
                {
                    flash.SetEnabled(false);
                    continue;
                }

                int lastIndex = route.Positions.Length - 1;
                Vector3 contactPoint = route.Positions[lastIndex];
                Vector3 previousPoint = route.Positions[lastIndex - 1];

                if (!TryBuildBasis(
                        previousPoint,
                        contactPoint,
                        out Vector3 side,
                        out Vector3 up))
                {
                    flash.SetEnabled(false);
                    continue;
                }

                Vector3 incomingDirection = (
                    contactPoint - previousPoint).normalized;
                float envelope = Mathf.Sin(
                    cycleProgress / visiblePortion * Mathf.PI) *
                    _visibility;
                float coreWidth = _coreWidth * 1.45f * envelope;
                float glowWidth = _glowWidth * 0.82f * envelope;

                for (int rayIndex = 0;
                    rayIndex < flash.Rays.Length;
                    rayIndex++)
                {
                    float angle = (rayIndex / (float)flash.Rays.Length) *
                        Mathf.PI * 2f +
                        cycleIndex * 1.37f +
                        i * 0.71f;
                    Vector3 radialDirection =
                        side * Mathf.Cos(angle) +
                        up * Mathf.Sin(angle);
                    Vector3 rayDirection = (
                        radialDirection * 0.94f -
                        incomingDirection * 0.34f).normalized;
                    float rayVariation = Mathf.Lerp(
                        0.72f,
                        1.18f,
                        Mathf.PerlinNoise(
                            cycleIndex * 0.37f + rayIndex * 2.17f,
                            i * 3.41f));
                    AccentPair ray = flash.Rays[rayIndex];
                    ray.Core.widthMultiplier = coreWidth;
                    ray.Glow.widthMultiplier = glowWidth;
                    ray.SetPosition(0, contactPoint);
                    ray.SetPosition(
                        1,
                        contactPoint +
                        rayDirection * _contactFlashRadius *
                        envelope * rayVariation);
                    ray.SetEnabled(coreWidth > 0.00001f);
                }
            }
        }

        private static Vector3 SamplePolyline(
            Vector3[] positions,
            float normalizedPosition)
        {
            float scaledPosition = Mathf.Clamp01(normalizedPosition) *
                (positions.Length - 1);
            int startIndex = Mathf.Min(
                Mathf.FloorToInt(scaledPosition),
                positions.Length - 2);
            float segmentT = scaledPosition - startIndex;
            return Vector3.LerpUnclamped(
                positions[startIndex],
                positions[startIndex + 1],
                segmentT);
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private bool HasValidRouteAnchors()
        {
            Transform source = GetAnchor(_sourceAnchors, _source, 0);
            Transform target = GetAnchor(_targetAnchors, _target, 0);
            return source != null && target != null;
        }

        private bool TryGetRoute(
            int routeIndex,
            out Vector3 start,
            out Vector3 end,
            out Transform sourceAnchor)
        {
            Transform source = GetAnchor(
                _sourceAnchors,
                _source,
                routeIndex);
            Transform target = GetAnchor(
                _targetAnchors,
                _target,
                routeIndex);

            if (source == null || target == null)
            {
                start = default;
                end = default;
                sourceAnchor = null;
                return false;
            }

            Vector3 sourceOffset = GetOffset(
                _sourceAnchorOffsets,
                _sourceLocalOffset,
                routeIndex);
            Vector3 targetOffset = GetOffset(
                _targetAnchorOffsets,
                _targetLocalOffset,
                routeIndex);
            end = target.TransformPoint(targetOffset);
            start = _projectSourcesToSurface
                ? GetSurfaceSourcePosition(
                    source,
                    end,
                    sourceOffset,
                    _sourceSurfaceRadius)
                : source.TransformPoint(sourceOffset);
            sourceAnchor = source;
            return true;
        }

        private static Vector3 GetSurfaceSourcePosition(
            Transform source,
            Vector3 targetPosition,
            Vector3 routeOffset,
            float radius)
        {
            Vector3 localForward = source.InverseTransformPoint(targetPosition);

            if (localForward.sqrMagnitude <= 0.000001f)
            {
                return source.position;
            }

            localForward.Normalize();
            Vector3 referenceAxis = Mathf.Abs(
                Vector3.Dot(localForward, Vector3.up)) > 0.92f
                ? Vector3.right
                : Vector3.up;
            Vector3 localSide = Vector3.Cross(
                localForward,
                referenceAxis).normalized;
            Vector3 localUp = Vector3.Cross(
                localSide,
                localForward).normalized;
            Vector3 surfaceDirection = (
                localForward +
                localSide * routeOffset.x +
                localUp * routeOffset.y).normalized;
            float routeRadius = Mathf.Max(0f, radius + routeOffset.z);
            return source.TransformPoint(surfaceDirection * routeRadius);
        }

        private static Transform GetAnchor(
            Transform[] anchors,
            Transform fallback,
            int routeIndex)
        {
            if (anchors == null || anchors.Length == 0)
            {
                return fallback;
            }

            Transform anchor = anchors[routeIndex % anchors.Length];
            return anchor != null ? anchor : fallback;
        }

        private static Vector3 GetOffset(
            Vector3[] offsets,
            Vector3 fallback,
            int routeIndex)
        {
            return offsets != null && offsets.Length > 0
                ? offsets[routeIndex % offsets.Length]
                : fallback;
        }

        private static bool TryBuildBasis(
            Vector3 start,
            Vector3 end,
            out Vector3 side,
            out Vector3 up)
        {
            Vector3 delta = end - start;

            if (delta.sqrMagnitude <= 0.000001f)
            {
                side = default;
                up = default;
                return false;
            }

            Vector3 forward = delta.normalized;
            side = Vector3.Cross(forward, Vector3.up);

            if (side.sqrMagnitude < 0.01f)
            {
                side = Vector3.Cross(forward, Vector3.right);
            }

            side.Normalize();
            up = Vector3.Cross(side, forward).normalized;
            return true;
        }

        private void FillArc(
            Vector3[] positions,
            Vector3 start,
            Vector3 end,
            Vector3 side,
            Vector3 up,
            float seed,
            float amplitude)
        {
            int lastIndex = positions.Length - 1;
            float animatedPhase = _elapsed * _noiseFrequency;

            for (int i = 0; i < positions.Length; i++)
            {
                float t = i / (float)lastIndex;
                float envelope = Mathf.Sin(t * Mathf.PI);
                float sideNoise = SignedNoise(seed, t * 3.7f + animatedPhase);
                float upNoise = SignedNoise(seed + 23.41f, t * 4.9f - animatedPhase * 1.13f);
                float microNoise = SignedNoise(seed + 47.83f, t * 11.3f + animatedPhase * 1.71f);
                Vector3 displacement =
                    side * (sideNoise + microNoise * 0.32f) +
                    up * (upNoise - microNoise * 0.24f);

                positions[i] = Vector3.LerpUnclamped(start, end, t) +
                    displacement * amplitude * envelope;
            }

            positions[0] = start;
            positions[lastIndex] = end;
        }

        private void FillWrappedArc(
            Vector3[] positions,
            Vector3 sphereCenter,
            Vector3 start,
            Vector3 end,
            Vector3 side,
            Vector3 up,
            float seed,
            float amplitude)
        {
            Vector3 startRadial = start - sphereCenter;
            Vector3 targetRadial = end - sphereCenter;

            if (startRadial.sqrMagnitude <= 0.000001f ||
                targetRadial.sqrMagnitude <= 0.000001f)
            {
                FillArc(
                    positions,
                    start,
                    end,
                    side,
                    up,
                    seed,
                    amplitude);
                return;
            }

            float sphereRadius = startRadial.magnitude;
            Vector3 startDirection = startRadial / sphereRadius;
            Vector3 targetDirection = targetRadial.normalized;
            Vector3 exitDirection = Vector3.Slerp(
                startDirection,
                targetDirection,
                _surfaceExitProgress).normalized;
            float clearedRadius = sphereRadius *
                (1f + _surfaceArcClearance);
            Vector3 exitPoint = sphereCenter +
                exitDirection * clearedRadius;
            Vector3 exitTangent = Vector3.ProjectOnPlane(
                targetDirection,
                exitDirection);

            if (exitTangent.sqrMagnitude <= 0.000001f)
            {
                exitTangent = Vector3.ProjectOnPlane(
                    end - start,
                    exitDirection);
            }

            if (exitTangent.sqrMagnitude <= 0.000001f)
            {
                exitTangent = side;
            }

            exitTangent.Normalize();
            Vector3 separationDirection =
                side * SignedNoise(seed * 0.071f, 1.73f) +
                up * SignedNoise(seed * 0.093f, 6.41f);

            if (separationDirection.sqrMagnitude <= 0.000001f)
            {
                separationDirection = side;
            }

            separationDirection.Normalize();
            float separationVariation = Mathf.Lerp(
                0.78f,
                1.12f,
                Mathf.PerlinNoise(seed * 0.037f, 2.91f));
            Vector3 routeSeparation = separationDirection *
                sphereRadius * _pathSeparation * separationVariation;
            Vector3 firstControl = exitPoint +
                exitTangent * sphereRadius * 0.72f +
                exitDirection * sphereRadius * 0.12f +
                routeSeparation * 0.35f;
            Vector3 secondControl = Vector3.Lerp(
                exitPoint,
                end,
                0.82f) +
                exitDirection * sphereRadius * 0.1f +
                routeSeparation * 0.55f;
            int lastIndex = positions.Length - 1;
            float animatedPhase = _elapsed * _noiseFrequency;

            for (int i = 0; i < positions.Length; i++)
            {
                float t = i / (float)lastIndex;
                Vector3 basePosition;

                if (t <= _surfaceWrapPortion)
                {
                    float surfaceT = Mathf.SmoothStep(
                        0f,
                        1f,
                        t / _surfaceWrapPortion);
                    Vector3 surfaceDirection = Vector3.Slerp(
                        startDirection,
                        exitDirection,
                        surfaceT).normalized;
                    basePosition = sphereCenter +
                        surfaceDirection * clearedRadius;
                }
                else
                {
                    float travelT = (t - _surfaceWrapPortion) /
                        (1f - _surfaceWrapPortion);
                    basePosition = CubicBezier(
                        exitPoint,
                        firstControl,
                        secondControl,
                        end,
                        travelT);
                }

                float envelope = Mathf.Sin(t * Mathf.PI);
                float sideNoise = SignedNoise(
                    seed,
                    t * 3.7f + animatedPhase);
                float upNoise = SignedNoise(
                    seed + 23.41f,
                    t * 4.9f - animatedPhase * 1.13f);
                float microNoise = SignedNoise(
                    seed + 47.83f,
                    t * 11.3f + animatedPhase * 1.71f);
                Vector3 displacement =
                    side * (sideNoise + microNoise * 0.32f) +
                    up * (upNoise - microNoise * 0.24f);
                Vector3 position = basePosition +
                    displacement * amplitude * envelope;

                if (t <= _surfaceWrapPortion)
                {
                    Vector3 radial = position - sphereCenter;

                    if (radial.sqrMagnitude <
                        clearedRadius * clearedRadius)
                    {
                        position = sphereCenter +
                            radial.normalized * clearedRadius;
                    }
                }

                positions[i] = position;
            }

            positions[0] = start;
            positions[lastIndex] = end;
        }

        private static Vector3 CubicBezier(
            Vector3 start,
            Vector3 firstControl,
            Vector3 secondControl,
            Vector3 end,
            float t)
        {
            float inverseT = 1f - t;
            return inverseT * inverseT * inverseT * start +
                3f * inverseT * inverseT * t * firstControl +
                3f * inverseT * t * t * secondControl +
                t * t * t * end;
        }

        private void UpdateVisibility(float deltaTime)
        {
            if (_isFadingOut)
            {
                _fadeOutElapsed += deltaTime;
                _visibility = 1f - Mathf.Clamp01(
                    _fadeOutElapsed / _fadeOutDuration);

                if (_fadeOutElapsed >= _fadeOutDuration)
                {
                    StopEffect();
                }

                return;
            }

            if (_visibility < 1f)
            {
                _visibility = _fadeInDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(_elapsed / _fadeInDuration);
            }
        }

        private void UpdateWidths()
        {
            float pulse = 0.95f + Mathf.Sin(_elapsed * _pulseSpeed) * 0.2f;
            float flicker = 1f - _flicker +
                Mathf.PerlinNoise(7.13f, _elapsed * 22f) * _flicker;
            float scale = _visibility * pulse * flicker;

            UpdateArcWidths(_primaryArcs, scale);
            UpdateArcWidths(_branchArcs, scale * 0.86f);
        }

        private void UpdateArcWidths(ArcPair[] arcs, float scale)
        {
            if (arcs == null)
            {
                return;
            }

            for (int i = 0; i < arcs.Length; i++)
            {
                float variation = 0.88f +
                    Mathf.PerlinNoise(i * 3.11f, _elapsed * 14f) * 0.24f;
                float routeActivity = Mathf.Lerp(
                    0.62f,
                    1.12f,
                    Mathf.SmoothStep(
                        0.18f,
                        0.86f,
                        Mathf.PerlinNoise(
                            19.7f + i * 4.31f,
                            _elapsed * (7.2f + i * 0.45f))));
                arcs[i].Glow.widthMultiplier = _glowWidth *
                    (arcs[i].IsBranch ? 0.72f : 1f) *
                    scale * variation * routeActivity;
                arcs[i].Core.widthMultiplier = _coreWidth *
                    (arcs[i].IsBranch ? 0.65f : 1f) *
                    scale * variation * routeActivity;
            }
        }

        private void SetRenderersEnabled(bool isEnabled)
        {
            SetRenderersEnabled(_primaryArcs, isEnabled);
            SetRenderersEnabled(_branchArcs, isEnabled);
        }

        private static void SetRenderersEnabled(
            ArcPair[] arcs,
            bool isEnabled)
        {
            if (arcs == null)
            {
                return;
            }

            for (int i = 0; i < arcs.Length; i++)
            {
                arcs[i].Glow.enabled = isEnabled;
                arcs[i].Core.enabled = isEnabled;
            }
        }

        private void SetAccentRenderersEnabled(bool isEnabled)
        {
            if (_travelingPulses != null)
            {
                for (int i = 0; i < _travelingPulses.Length; i++)
                {
                    _travelingPulses[i].SetEnabled(isEnabled);
                }
            }

            if (_contactFlashes == null)
            {
                return;
            }

            for (int i = 0; i < _contactFlashes.Length; i++)
            {
                _contactFlashes[i].SetEnabled(isEnabled);
            }
        }

        private static float SignedNoise(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }

        private void OnValidate()
        {
            _primaryArcCount = Mathf.Clamp(_primaryArcCount, 1, 8);
            _branchArcCount = Mathf.Clamp(_branchArcCount, 0, 4);
            _segmentCount = Mathf.Clamp(_segmentCount, 8, 32);
            _coreWidth = Mathf.Max(0.0001f, _coreWidth);
            _glowWidth = Mathf.Max(_coreWidth, _glowWidth);
            _travelingPulseCount = Mathf.Clamp(
                _travelingPulseCount,
                0,
                8);
            _travelingPulseSegmentCount = Mathf.Clamp(
                _travelingPulseSegmentCount,
                3,
                8);
            _travelingPulseSpeed = Mathf.Max(
                0.01f,
                _travelingPulseSpeed);
            _travelingPulseLength = Mathf.Clamp(
                _travelingPulseLength,
                0.02f,
                0.2f);
            _travelingPulseWidthMultiplier = Mathf.Clamp(
                _travelingPulseWidthMultiplier,
                1f,
                4f);
            _travelingPulseGlowWidthMultiplier = Mathf.Clamp(
                _travelingPulseGlowWidthMultiplier,
                1f,
                3f);
            _contactFlashCount = Mathf.Clamp(
                _contactFlashCount,
                0,
                4);
            _contactFlashRayCount = Mathf.Clamp(
                _contactFlashRayCount,
                1,
                4);
            _contactFlashRadius = Mathf.Max(
                0.001f,
                _contactFlashRadius);
            _contactFlashRate = Mathf.Max(
                0.1f,
                _contactFlashRate);
            _noiseAmplitude = Mathf.Max(0f, _noiseAmplitude);
            _noiseFrequency = Mathf.Max(0.1f, _noiseFrequency);
            _pulseSpeed = Mathf.Max(0f, _pulseSpeed);
            _fadeInDuration = Mathf.Max(0f, _fadeInDuration);
            _sourceSurfaceRadius = Mathf.Max(0f, _sourceSurfaceRadius);
            _surfaceWrapPortion = Mathf.Clamp(
                _surfaceWrapPortion,
                0.15f,
                0.6f);
            _surfaceExitProgress = Mathf.Clamp(
                _surfaceExitProgress,
                0.35f,
                0.95f);
            _surfaceArcClearance = Mathf.Clamp(
                _surfaceArcClearance,
                0f,
                0.15f);
            _pathSeparation = Mathf.Clamp(
                _pathSeparation,
                0f,
                1.5f);
        }

        private sealed class ArcPair
        {
            public ArcPair(
                LineRenderer glow,
                LineRenderer core,
                Vector3[] positions,
                bool isBranch)
            {
                Glow = glow;
                Core = core;
                Positions = positions;
                IsBranch = isBranch;
            }

            public LineRenderer Glow { get; }
            public LineRenderer Core { get; }
            public Vector3[] Positions { get; }
            public bool IsBranch { get; }

            public void SetEnabled(bool isEnabled)
            {
                Glow.enabled = isEnabled;
                Core.enabled = isEnabled;
            }

            public void ApplyPositions()
            {
                Glow.SetPositions(Positions);
                Core.SetPositions(Positions);
            }
        }

        private sealed class TravelingPulse
        {
            public TravelingPulse(
                LineRenderer glow,
                LineRenderer core,
                Vector3[] positions,
                int routeIndex,
                float phaseOffset)
            {
                Glow = glow;
                Core = core;
                Positions = positions;
                RouteIndex = routeIndex;
                PhaseOffset = phaseOffset;
            }

            public LineRenderer Glow { get; }
            public LineRenderer Core { get; }
            public Vector3[] Positions { get; }
            public int RouteIndex { get; }
            public float PhaseOffset { get; }

            public void SetEnabled(bool isEnabled)
            {
                Glow.enabled = isEnabled;
                Core.enabled = isEnabled;
            }
        }

        private sealed class ContactFlash
        {
            public ContactFlash(
                AccentPair[] rays,
                float phaseOffset)
            {
                Rays = rays;
                PhaseOffset = phaseOffset;
            }

            public AccentPair[] Rays { get; }
            public float PhaseOffset { get; }

            public void SetEnabled(bool isEnabled)
            {
                for (int i = 0; i < Rays.Length; i++)
                {
                    Rays[i].SetEnabled(isEnabled);
                }
            }
        }

        private sealed class AccentPair
        {
            public AccentPair(LineRenderer glow, LineRenderer core)
            {
                Glow = glow;
                Core = core;
            }

            public LineRenderer Glow { get; }
            public LineRenderer Core { get; }

            public void SetPosition(int index, Vector3 position)
            {
                Glow.SetPosition(index, position);
                Core.SetPosition(index, position);
            }

            public void SetEnabled(bool isEnabled)
            {
                Glow.enabled = isEnabled;
                Core.enabled = isEnabled;
            }
        }
    }
}
