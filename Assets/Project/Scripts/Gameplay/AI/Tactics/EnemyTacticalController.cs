using System;
using UnityEngine;
namespace Breachpoint.Gameplay.AI
{
    public enum EnemyCoverActionPhase { MovingToCover, Protected, MovingOut, Exposed, MovingIn, TakingHighCover, LeavingHighCover }
    public sealed class EnemyTacticalController : IDisposable
    {
        private const float HighCoverArrivalDistance = .08f;
        private readonly EnemyContext _context;
        private readonly EnemyTacticalConfig _config;
        private readonly EnemyCoverService _covers;
        private readonly EnemySquadService _squads;
        private readonly EnemyStance _stance;
        private readonly EnemyBrain _brain;
        private readonly EnemyAnimationBridge _presentation;
        private readonly EnemyHighCoverPresentation _highMotion;
        private float _highActionStarted;
        private readonly EnemyTacticalPolicy _policy;
        private readonly EnemyTacticalPlanner _planner;
        private EnemySquad _squad;
        private EnemySquadMemberState _member;
        private EnemyTacticalCandidate _current;
        private EnemyCoverPoint _cover;
        private EnemyCoverPoint _failedCover;
        private float _failedCoverUntil;
        private Vector3 _failedPosition;
        private float _failedPositionUntil;
        private float _enteredAt;
        private Vector3 _intentThreatAt;
        private float _nextDecision;
        private float _phaseUntil;
        private bool _coverReached;
        private bool _hadPressure;
        private bool _disposed;
        private string _joinedId;
        public EnemyTacticalCandidate Current => _current;
        public EnemyCoverPoint CurrentCover => _cover;
        // A committed High Cover action takes longer than the former walk-only
        // cycle. Retain its last confirmed contact for those authored actions,
        // without refreshing LastSeenTime or tracking the hidden target.
        public float ContactMemoryDuration => _context.Config.Perception.MemoryDuration +
            (_cover != null && _cover.Kind == EnemyCoverKind.High && _presentation != null && _presentation.SupportsHighCover
                ? _presentation.Config.HighCoverTakeDuration + _presentation.Config.HighCoverLeaveDuration : 0f);
        // The first protect/expose/return cycle contains two entries and one exit.
        // Keep the existing bounded ally-crossing grace for that authored cycle.
        public float CoverCommitmentDuration => _config.PositionTimeout +
            (_cover != null && _cover.Kind == EnemyCoverKind.High && _presentation != null && _presentation.SupportsHighCover
                ? _presentation.Config.HighCoverTakeDuration * 2f + _presentation.Config.HighCoverLeaveDuration : 0f);
        public System.Collections.Generic.IReadOnlyList<EnemyCoverPoint> CoverPoints => _covers.Points;
        public EnemyCoverReservation Reservation(EnemyCoverPoint point) => _covers.Reservation(point);
        public EnemyCoverRating CoverRating => _planner.CurrentCoverRating;
        public EnemyCoverActionPhase CoverPhase { get; private set; }
        public EnemySquad Squad => _squad;
        public EnemySquadMemberState Member => _member;
        public EnemyTacticalPlanner Planner => _planner;
        public bool FireLane { get; private set; }
        public EnemyKnowledge Knowledge { get; private set; }
        public TacticalReason LastDecisionReason { get; private set; }
        public float LastDecisionAt { get; private set; }
        public int DecisionCount { get; private set; }
        public bool MeasureDecisionCost { get; set; }
        public double DecisionMilliseconds { get; private set; }
        public long DecisionAllocatedBytes { get; private set; }
        public int MeasuredDecisions { get; private set; }
        public double AverageDecisionMilliseconds => MeasuredDecisions > 0 ? DecisionMilliseconds / MeasuredDecisions : 0;
        public event Action<EnemyTacticalCandidate, TacticalReason> DecisionChanged;
        public EnemyTacticalController(EnemyContext context, EnemyTacticalConfig config, EnemyCoverService covers, EnemySquadService squads)
        {
            _context = context; _config = config; _covers = covers; _squads = squads;
            _stance = context.Actor.GetComponent<EnemyStance>(); _brain = context.Actor.GetComponent<EnemyBrain>();
            _presentation = context.Actor.GetComponent<EnemyAnimationBridge>();
            _highMotion = context.Actor.GetComponent<EnemyHighCoverPresentation>();
            _policy = new EnemyTacticalPolicy(config); _planner = new EnemyTacticalPlanner(context, covers, squads, config);
            context.Combat.Fired += Fired;
        }
        public void Reset(bool active)
        {
            Suspend(); _failedCover = null; _failedPositionUntil = _failedCoverUntil = _nextDecision = _enteredAt = _phaseUntil = 0f;
            Knowledge = EnemyKnowledge.None; FireLane = false; DecisionCount = 0;
            _current = default; _intentThreatAt = _failedPosition = Vector3.zero; _joinedId = null;
            LastDecisionAt = 0f; LastDecisionReason = TacticalReason.NoCandidate; CoverPhase = EnemyCoverActionPhase.MovingToCover;
            DecisionMilliseconds = 0; DecisionAllocatedBytes = 0; MeasuredDecisions = 0; _planner.Reset();
            if (!active || _disposed) return;
            _joinedId = _context.Actor.SquadId; _squad = _squads.Join(_context, _joinedId, _config.Squad, out _member);
        }
        public void Suspend()
        {
            Pause(); _squads.Leave(_context, _context.Actor.Health.IsDead); _member = null; _squad = null;
        }
        public void Pause()
        {
            _covers.ReleaseOwner(_context); if (_member != null) { _squad.ReleaseSlots(_member); _member.CanPressure = false; }
            _cover = null; _coverReached = _hadPressure = false; _highActionStarted = 0f; _current.Valid = false; _nextDecision = 0f;
            if (!_context.Actor.Health.IsDead) _stance?.SetCrouching(false); _context.Combat.PauseAim();
        }
        public void Observe(float now)
        {
            if (_disposed || !_brain.enabled || _context.Actor.Health.IsDead) return;
            if (_member == null || _joinedId != _context.Actor.SquadId) Reset(true);
            _covers.Cleanup(now); _squad.UpdateCommunication(_member, now);
            EnemyBlackboard memory = _context.Memory;
            if (!memory.Visible && _squad.TryShared(_member, now, out var contact) && contact.SightTime > memory.LastSeenTime)
            {
                memory.Target = contact.Target; memory.LastKnownPosition = contact.Position; memory.KnownAimPosition = contact.AimPosition;
                memory.LastSeenTime = memory.SharedContactTime = contact.SightTime; memory.HasContact = true;
                memory.Alert = Mathf.Max(memory.Alert, _context.Config.Decision.AlertThreshold);
            }
            bool targetAlive = memory.Target != null && memory.Target.IsAlive;
            float age = now - memory.LastSeenTime;
            FireLane = targetAlive && (memory.Visible ? _context.Combat.HasFiringLine(memory.Target) : age <= _config.RecentSuppressionMemory && _context.Combat.CanSuppress(memory.KnownAimPosition, memory.Target));
            Knowledge = memory.Visible ? EnemyKnowledge.DirectSight : memory.HasContact ? _current.Intent == EnemyTacticalIntent.Search && _current.Valid ? EnemyKnowledge.SearchHypothesis : memory.SharedContactTime == memory.LastSeenTime ? EnemyKnowledge.SharedConfirmedContact : EnemyKnowledge.LastKnownPosition : memory.HasNoise ? EnemyKnowledge.HeardPosition : EnemyKnowledge.None;
            _member.RequiresExposure = _cover != null && !FireLane;
            _member.CanPressure = _brain.States.Group != EnemyStateGroup.Disabled && (FireLane || _cover != null && _planner.CurrentCoverRating.Valid && targetAlive && age <= ContactMemoryDuration);
            if (!memory.HasContact) Pause();
        }
        public void Tick(float deltaTime)
        {
            if (_member == null || !_member.Alive) return;
            float now = _context.Now;
            EnemyBlackboard memory = _context.Memory;
            if (!memory.HasContact) { Pause(); _context.Navigation.Stop(); return; }
            bool rushed = Vector3.Distance(_context.Actor.transform.position, memory.LastKnownPosition) < _context.Config.Combat.MinimumRange;
            bool failed = _context.Navigation.Failed || IsMoving(_current.Intent) && now - _enteredAt > _config.PositionTimeout && !_context.Navigation.Arrived;
            if (_cover != null && (_covers.Reservation(_cover)?.Owner != _context || !_cover.IsUsable || (_coverReached || CoverPhase == EnemyCoverActionPhase.TakingHighCover) && (CoverPhase == EnemyCoverActionPhase.MovingOut || CoverPhase == EnemyCoverActionPhase.MovingIn || CoverPhase == EnemyCoverActionPhase.TakingHighCover || CoverPhase == EnemyCoverActionPhase.LeavingHighCover) && now >= _phaseUntil)) failed = true;
            if (failed)
            { _failedCover = _cover; _failedCoverUntil = now + _config.RepositionAfter; _failedPosition = _current.Destination; _failedPositionUntil = _failedCoverUntil; Pause(); _context.Navigation.AcknowledgeFailure(); _nextDecision = now; }
            if (now >= _nextDecision)
            {
                _nextDecision = now + _config.DecisionInterval;
                bool reload = _context.Combat.IsReloading || _context.Combat.NeedsReload || _cover != null && _context.Combat.Ammo <= _context.Config.Combat.MagazineSize * _config.ReloadAmmoFraction;
                var facts = new EnemyTacticalFacts { ThreatAim = memory.KnownAimPosition, ThreatPosition = memory.LastKnownPosition, DirectSight = memory.Visible, FireLane = FireLane, RecentSuppression = now - memory.LastSeenTime <= _config.RecentSuppressionMemory && FireLane, ReloadNeeded = reload, CanCrouch = _stance != null, IntentAge = now - _enteredAt, CurrentCover = _cover, FailedCover = now < _failedCoverUntil ? _failedCover : null, HasFailedPosition = now < _failedPositionUntil, FailedPosition = _failedPosition, Current = _current };
                long decisionStart = MeasureDecisionCost ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
                long allocatedStart = MeasureDecisionCost ? GC.GetAllocatedBytesForCurrentThread() : 0;
                _planner.Build(facts, _squad, _member);
                var current = _current.Valid ? _planner.RescoredCurrent(_current) : _current;
                var selected = _policy.Choose(_planner.Candidates, _planner.Count, current, _enteredAt, now, rushed || failed || _cover == null && !_covers.PositionAvailable(_context.Actor.transform.position, _context, _config.PreferredSpacing) || Vector3.Distance(memory.LastKnownPosition, _intentThreatAt) > 4f, out var reason);
                LastDecisionAt = now; LastDecisionReason = reason; DecisionCount++;
                Adopt(selected, reason, now);
                if (MeasureDecisionCost)
                {
                    DecisionMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - decisionStart) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                    DecisionAllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - allocatedStart; MeasuredDecisions++;
                }
            }
            if (!_current.Valid) { _context.Navigation.Stop(); return; }
            if (_cover != null) ExecuteCover(now, deltaTime);
            else if (IsMoving(_current.Intent)) Move(_current.Destination, _current.Intent == EnemyTacticalIntent.Search ? EnemyMovePace.Walk : EnemyMovePace.Run, FlankSide(_current.Intent), now, deltaTime);
            else if (_current.Intent == EnemyTacticalIntent.ReloadProtected)
            { _squad.ReleaseSlots(_member); _member.Role = EnemySquadRole.Reloading; _context.Navigation.Stop(); _context.Combat.RequestReload(now); if (!_context.Combat.IsReloading) _current.Valid = false; }
            else Engage(now, deltaTime);
        }
        private void Adopt(EnemyTacticalCandidate selected, TacticalReason reason, float now)
        {
            bool changed = !_current.Valid || selected.Intent != _current.Intent || selected.Cover != _cover || Vector3.Distance(selected.Destination, _current.Destination) > 1f;
            if (!changed) { _current.Score = selected.Score; return; }
            if (selected.Cover != _cover) { _covers.ReleaseOwner(_context); _cover = null; }
            if (selected.Cover != null && !_covers.Reserve(selected.Cover, _context, _config.Cover, now))
            { _current.Valid = false; _nextDecision = now + 0.2f; return; }
            _squad.ReleaseSlots(_member);
            bool sameCover = _cover == selected.Cover && _cover != null;
            _intentMoveStart = _context.Actor.transform.position; _cover = selected.Cover; _current = selected; _enteredAt = now; _intentThreatAt = _context.Memory.LastKnownPosition; _hadPressure = false;
            if (!sameCover) { _coverReached = false; CoverPhase = EnemyCoverActionPhase.MovingToCover; _phaseUntil = now; }
            if (_cover == null) _stance?.SetCrouching(false);
            if (IsMoving(selected.Intent)) _context.Combat.PauseAim();
            DecisionChanged?.Invoke(selected, reason);
        }
        private void Move(Vector3 destination, EnemyMovePace pace, int flank, float now, float deltaTime, float speedOverride = 0f)
        {
            bool preciseCover = _cover != null && _cover.Kind == EnemyCoverKind.High;
            float arrivalDistance = preciseCover ? HighCoverArrivalDistance : .65f;
            bool pressure = _squad.HasPressure(_member, now);
            if (_hadPressure && !pressure && _current.Intent != EnemyTacticalIntent.Fallback)
            { _context.Navigation.Stop(); _squad.ReleaseMovement(_member); _current.Valid = false; _nextDecision = now + _config.DecisionInterval; _hadPressure = false; LastDecisionReason = TacticalReason.SharedPressure; return; }
            _hadPressure |= pressure;
            if ((preciseCover ? PlanarDistance(_context.Actor.transform.position, destination) : Vector3.Distance(_context.Actor.transform.position, destination)) <= arrivalDistance)
            {
                if (preciseCover) _context.Navigation.Stop(); else _context.Navigation.RequestStop(true); _squad.ReleaseMovement(_member);
                if (_cover == null && _current.Intent != EnemyTacticalIntent.Search) { _current.Valid = false; _nextDecision = now; }
                if (_current.Intent == EnemyTacticalIntent.Search) _context.Navigation.Face(_context.Actor.transform.position + Quaternion.Euler(0, (now - _enteredAt) * 50f, 0) * Vector3.forward * 5f, deltaTime);
                return;
            }
            if (!_covers.PositionAvailable(destination, _context, _config.PreferredSpacing) || !_squad.TryMover(_member, destination, flank, _config.PreferredSpacing, now, _current.Intent != EnemyTacticalIntent.Fallback))
            { _context.Navigation.Stop(); _squad.ReleaseMovement(_member); return; }
            _member.Role = _current.Intent == EnemyTacticalIntent.Search ? EnemySquadRole.Searching : flank != 0 ? EnemySquadRole.Flanker : EnemySquadRole.Mover;
            // Distance is measured from the adopted intent, so pace cannot oscillate at a threshold.
            if (pace == EnemyMovePace.Run) pace = MovementPace(_current.Intent, Vector3.Distance(_intentMoveStart, destination));
            _context.Navigation.MoveTo(destination, pace, now, speedOverride, preciseCover);
            Vector3 facing = _cover != null && (CoverPhase == EnemyCoverActionPhase.TakingHighCover || CoverPhase == EnemyCoverActionPhase.LeavingHighCover)
                ? _context.Actor.transform.position + _cover.Normal * 5f : _context.Memory.KnownAimPosition;
            _context.Navigation.Face(facing, deltaTime);
        }
        private void Engage(float now, float deltaTime)
        {
            _context.Navigation.Stop(); _squad.ReleaseMovement(_member);
            if (_presentation != null && _presentation.enabled && _presentation.IsHighCoverPose && _presentation.Animator.enabled)
            { _context.Combat.PauseAim(); return; }
            if (_context.Combat.NeedsReload) { _context.Combat.RequestReload(now); _squad.ReleaseShooter(_member); return; }
            float score = 30f + _context.Combat.Ammo / (float)_context.Config.Combat.MagazineSize * 10f + (_cover != null ? 20f : 0f) - Mathf.Min(20f, (now - _enteredAt) * 1.5f);
            if (!_squad.TryShooter(_member, score, now)) { _member.Role = EnemySquadRole.Support; return; }
            _context.Navigation.Face(_context.Memory.KnownAimPosition, deltaTime);
            Vector3 direction = _context.Memory.KnownAimPosition - _context.Actor.Eyes.position; direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f || Vector3.Angle(_context.Actor.transform.forward, direction) > 15f) return;
            if (_context.Memory.Visible) _context.Combat.Attack(_context.Memory.Target, now);
            else if (now - _context.Memory.LastSeenTime <= _config.RecentSuppressionMemory && now - _enteredAt <= _config.SuppressionDuration)
                _context.Combat.AttackSuppression(_context.Memory.KnownAimPosition, _context.Memory.Target, now);
            else { _squad.ReleaseShooter(_member); _current.Valid = false; }
        }
        private void ExecuteCover(float now, float deltaTime)
        {
            float protectedDistance = _cover.Kind == EnemyCoverKind.High ? PlanarDistance(_context.Actor.transform.position, _cover.ProtectedPosition) : Vector3.Distance(_context.Actor.transform.position, _cover.ProtectedPosition);
            if (!_covers.Pulse(_cover, _context, _config.Cover, now, protectedDistance <= 0.8f))
            { _current.Valid = false; _covers.ReleaseOwner(_context); _cover = null; return; }
            bool animatedHigh = _cover.Kind == EnemyCoverKind.High && _presentation != null && _presentation.SupportsHighCover;
            bool authoredMotion = animatedHigh && _highMotion != null && _highMotion.CanDriveMotion;
            if (animatedHigh && CoverPhase == EnemyCoverActionPhase.TakingHighCover)
            {
                StandForHighCover(); _context.Combat.PauseAim();
                if (authoredMotion && !MaintainHighMover(now, _cover.ProtectedPosition)) return;
                if (!authoredMotion) Move(_cover.ProtectedPosition, EnemyMovePace.Walk, 0, now, deltaTime, _presentation.Config.HighCoverEntrySpeed);
                _context.Navigation.Face(_context.Actor.transform.position + _cover.Normal * 5f, deltaTime);
                if (protectedDistance <= HighCoverArrivalDistance && HighCoverActionFinished(true, now))
                {
                    _context.Navigation.Stop(); _squad.ReleaseSlots(_member);
                    _coverReached = true; CoverPhase = EnemyCoverActionPhase.Protected; _phaseUntil = now + 1.2f;
                }
                return;
            }
            if (animatedHigh && CoverPhase == EnemyCoverActionPhase.LeavingHighCover)
            {
                _context.Combat.PauseAim();
                if (authoredMotion && !MaintainHighMover(now, _cover.ExposurePosition)) return;
                if (!authoredMotion) Move(_cover.ExposurePosition, EnemyMovePace.Walk, 0, now, deltaTime, _presentation.Config.HighCoverExitSpeed);
                Vector3 threatDirection = Vector3.ProjectOnPlane(_context.Memory.KnownAimPosition - _context.Actor.transform.position, Vector3.up);
                if (authoredMotion && threatDirection.sqrMagnitude > .001f)
                {
                    float facingBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(HighCoverExitFacingStart, HighCoverExitFacingReady, _presentation.HighCoverExitProgress));
                    Vector3 facing = Vector3.Slerp(_cover.Normal, threatDirection.normalized, facingBlend);
                    _context.Navigation.Face(_context.Actor.transform.position + facing * 5f, deltaTime);
                }
                if (HighCoverActionFinished(false, now))
                { CoverPhase = EnemyCoverActionPhase.MovingOut; _phaseUntil = now + _config.PositionTimeout; }
                return;
            }
            if (!_coverReached)
            {
                if (authoredMotion) { ApproachHighEntry(now, deltaTime); return; }
                if (animatedHigh && protectedDistance <= _presentation.Config.HighCoverEntryDistance)
                { BeginHighCoverAction(true, now); _context.Combat.PauseAim(); _context.Navigation.CancelTurn(); return; }
                if (protectedDistance > 0.65f) { Move(_cover.ProtectedPosition, EnemyMovePace.Run, 0, now, deltaTime); return; }
                _coverReached = true; CoverPhase = EnemyCoverActionPhase.Protected; _phaseUntil = now + 1.2f;
            }
            if (CoverPhase == EnemyCoverActionPhase.Protected)
            {
                _context.Navigation.Stop(); _squad.ReleaseSlots(_member);
                if (animatedHigh) _context.Navigation.Face(_context.Actor.transform.position + _cover.Normal * 5f, deltaTime);
                if (_cover.Kind == EnemyCoverKind.Low) _stance?.SetCrouching(true); else StandForHighCover();
                if (_context.Combat.IsReloading || _context.Combat.Ammo <= _context.Config.Combat.MagazineSize * _config.ReloadAmmoFraction)
                { _context.Combat.RequestReload(now); _member.Role = EnemySquadRole.Reloading; return; }
                if (_current.Intent == EnemyTacticalIntent.ReloadProtected) { _current.Valid = false; _nextDecision = now; return; }
                if (now < _phaseUntil || !_member.CanPressure) return;
                if (_cover.Kind == EnemyCoverKind.High)
                {
                    if (animatedHigh) BeginHighCoverAction(false, now);
                    else { CoverPhase = EnemyCoverActionPhase.MovingOut; _phaseUntil = now + _config.PositionTimeout; }
                }
                else if (_squad.TryShooter(_member, 60f, now)) { _stance?.SetCrouching(false); CoverPhase = EnemyCoverActionPhase.Exposed; _phaseUntil = now + _config.SuppressionDuration; }
            }
            else if (CoverPhase == EnemyCoverActionPhase.MovingOut || CoverPhase == EnemyCoverActionPhase.MovingIn)
            {
                if (authoredMotion && CoverPhase == EnemyCoverActionPhase.MovingIn) { ApproachHighEntry(now, deltaTime); return; }
                if (authoredMotion && _presentation.IsHighCoverPose)
                { _context.Navigation.Stop(); _context.Navigation.Face(_context.Memory.KnownAimPosition, deltaTime); return; }
                Vector3 point = CoverPhase == EnemyCoverActionPhase.MovingOut ? _cover.ExposurePosition : _cover.ProtectedPosition;
                if (animatedHigh && CoverPhase == EnemyCoverActionPhase.MovingIn && protectedDistance <= _presentation.Config.HighCoverEntryDistance)
                { BeginHighCoverAction(true, now); return; }
                Move(point, EnemyMovePace.Walk, 0, now, deltaTime);
                if ((animatedHigh ? PlanarDistance(_context.Actor.transform.position, point) : Vector3.Distance(_context.Actor.transform.position, point)) <= (animatedHigh ? HighCoverArrivalDistance : .65f))
                { _squad.ReleaseMovement(_member); CoverPhase = CoverPhase == EnemyCoverActionPhase.MovingOut ? EnemyCoverActionPhase.Exposed : EnemyCoverActionPhase.Protected; _phaseUntil = now + _config.SuppressionDuration; }
            }
            else
            {
                _stance?.SetCrouching(false);
                if (animatedHigh && _presentation.enabled && _presentation.Animator.enabled && (_presentation.IsHighCoverPose || !_presentation.IsAimPoseReady))
                {
                    _context.Navigation.Stop(); _context.Combat.PauseAim();
                    _context.Navigation.Face(_context.Memory.KnownAimPosition, deltaTime);
                    _phaseUntil = now + _config.SuppressionDuration; return;
                }
                if (animatedHigh && (_context.Combat.NeedsReload || _context.Combat.IsReloading ||
                    _context.Combat.Ammo <= _context.Config.Combat.MagazineSize * _config.ReloadAmmoFraction))
                {
                    // Return before requesting reload; a long entry animation must not
                    // consume the entire reload while the rifleman is still exposed.
                    _squad.ReleaseShooter(_member); _context.Combat.PauseAim();
                    CoverPhase = EnemyCoverActionPhase.MovingIn; _phaseUntil = now + _config.PositionTimeout; return;
                }
                Engage(now, deltaTime);
                if (now >= _phaseUntil || _context.Combat.NeedsReload || _context.Combat.IsReloading)
                { _squad.ReleaseShooter(_member); CoverPhase = _cover.Kind == EnemyCoverKind.High ? EnemyCoverActionPhase.MovingIn : EnemyCoverActionPhase.Protected; _phaseUntil = now + (_cover.Kind == EnemyCoverKind.High ? _config.PositionTimeout : 1.2f); }
            }
        }
        private bool MaintainHighMover(float now, Vector3 destination)
        {
            bool pressure = _squad.HasPressure(_member, now);
            if (_hadPressure && !pressure && _current.Intent != EnemyTacticalIntent.Fallback)
            {
                _context.Navigation.Stop(); Pause(); _nextDecision = now + _config.DecisionInterval;
                LastDecisionReason = TacticalReason.SharedPressure; return false;
            }
            _hadPressure |= pressure;
            if (!_covers.PositionAvailable(destination, _context, _config.PreferredSpacing) ||
                !_squad.TryMover(_member, destination, 0, _config.PreferredSpacing, now, true))
            {
                if (CoverPhase == EnemyCoverActionPhase.TakingHighCover || CoverPhase == EnemyCoverActionPhase.LeavingHighCover)
                { _context.Navigation.Stop(); Pause(); _nextDecision = now + _config.DecisionInterval; }
                return false;
            }
            _member.Role = EnemySquadRole.Mover; return true;
        }
        private void StandForHighCover() { if (_stance != null && _stance.IsCrouching) _stance.SetCrouching(false); }
        private void ApproachHighEntry(float now, float deltaTime)
        {
            StandForHighCover();
            Vector3 entry = _highMotion.EntryPosition(_cover);
            if (PlanarDistance(_context.Actor.transform.position, entry) > HighCoverArrivalDistance)
            { Move(entry, _coverReached ? EnemyMovePace.Walk : EnemyMovePace.Run, 0, now, deltaTime); return; }
            _context.Navigation.Stop();
            _context.Navigation.Face(_context.Actor.transform.position + _cover.Normal * 5f, deltaTime);
            if (_context.Navigation.IsTurning || Vector3.Angle(_context.Actor.transform.forward, _cover.Normal) > 3f) return;
            BeginHighCoverAction(true, now);
        }
        private void BeginHighCoverAction(bool taking, float now)
        {
            if (_highMotion != null && _highMotion.CanDriveMotion && !MaintainHighMover(now, taking ? _cover.ProtectedPosition : _cover.ExposurePosition)) return;
            CoverPhase = taking ? EnemyCoverActionPhase.TakingHighCover : EnemyCoverActionPhase.LeavingHighCover;
            _highActionStarted = now;
            if (_highMotion != null && _highMotion.CanDriveMotion) _highMotion.Begin(_cover, taking);
            _phaseUntil = now + _config.PositionTimeout + (taking ? _presentation.Config.HighCoverTakeDuration : _presentation.Config.HighCoverLeaveDuration);
            _squad.ReleaseShooter(_member); _context.Combat.PauseAim();
        }
        private bool HighCoverActionFinished(bool taking, float now)
        {
            float duration = taking ? _presentation.Config.HighCoverTakeDuration : _presentation.Config.HighCoverLeaveDuration;
            // Missing/disabled presentation cannot hold gameplay indefinitely. The
            // navigation timeout still rejects unreachable or obstructed cover travel.
            return _presentation.HighCoverActionComplete(taking) || now - _highActionStarted >= duration + .5f;
        }
        private static float PlanarDistance(Vector3 a, Vector3 b) { a.y = b.y; return Vector3.Distance(a, b); }
        private void Fired() { if (_member != null) _member.LastShotAt = Time.time; }
        private static bool IsMoving(EnemyTacticalIntent intent) => intent == EnemyTacticalIntent.Advance || intent == EnemyTacticalIntent.Reposition || intent == EnemyTacticalIntent.FlankLeft || intent == EnemyTacticalIntent.FlankRight || intent == EnemyTacticalIntent.Fallback || intent == EnemyTacticalIntent.Search;
        private const float HighCoverExitFacingStart = .35f;
        private const float HighCoverExitFacingReady = .85f;
        private const float ShortMovementDistance = 3f;
        private const float UrgentFallbackDistance = 6f;
        private const float LongFlankDistance = 14f;
        private const float LongAdvanceDistance = 18f;
        private Vector3 _intentMoveStart;
        public static EnemyMovePace MovementPace(EnemyTacticalIntent intent, float distance)
        {
            if (intent == EnemyTacticalIntent.Search || distance < ShortMovementDistance && intent != EnemyTacticalIntent.Fallback) return EnemyMovePace.Walk;
            bool urgent = intent == EnemyTacticalIntent.Fallback && distance >= UrgentFallbackDistance;
            bool longFlank = (intent == EnemyTacticalIntent.FlankLeft || intent == EnemyTacticalIntent.FlankRight) && distance >= LongFlankDistance;
            bool longClosure = intent == EnemyTacticalIntent.Advance && distance >= LongAdvanceDistance;
            return urgent || longFlank || longClosure ? EnemyMovePace.Sprint : EnemyMovePace.Run;
        }
        private static int FlankSide(EnemyTacticalIntent intent) => intent == EnemyTacticalIntent.FlankLeft ? -1 : intent == EnemyTacticalIntent.FlankRight ? 1 : 0;
        public void Dispose()
        { if (_disposed) return; Suspend(); _context.Combat.Fired -= Fired; _disposed = true; }
    }
}
