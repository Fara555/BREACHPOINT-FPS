using System;
using System.Collections.Generic;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;
namespace Breachpoint.Editor.Enemies
{
    internal sealed class RiflemanTacticalFixture : IDisposable
    {
        private readonly List<Object> _configs = new List<Object>();
        private readonly List<EnemyCoverPoint> _points = new List<EnemyCoverPoint>();
        private readonly Action[] _fireHandlers;
        public GameObject Root { get; }
        public EnemyBrain[] Brains { get; }
        public EnemyActor[] Actors { get; }
        public EnemyContext[] Contexts { get; }
        public PerceptionTarget Target { get; }
        public EnemyCoverService Covers { get; }
        public int Shots { get; private set; }
        public bool PressureMovementOverlap { get; private set; }
        public bool SawMover { get; private set; }
        public bool SawFlank { get; private set; }
        public bool SawProtectedLow { get; private set; }
        public bool SawProtectedHigh { get; private set; }
        public bool SawExposed { get; private set; }
        public bool ProtectedShot { get; private set; }
        public bool CoverExposedShot { get; private set; }
        public int MaximumShooters { get; private set; }
        public int MaximumMovers { get; private set; }
        public int MaximumReservations { get; private set; }
        public RiflemanTacticalFixture(GameLifetimeScope scope, EnemyWorld world, int count, string id, int maximumShooters = 2, int shortMagazineIndex = -1)
        {
            Root = new GameObject("Runtime tactical scenario " + id); Root.SetActive(false);
            Brains = new EnemyBrain[count]; Actors = new EnemyActor[count]; Contexts = new EnemyContext[count]; _fireHandlers = new Action[count];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AdamPresentationIntegration.RiflemanPath);
            var archetype = Object.Instantiate(prefab.GetComponent<EnemyLifetimeScope>().Archetype); _configs.Add(archetype);
            var tactics = Object.Instantiate(archetype.Tactics); _configs.Add(tactics);
            var squad = Object.Instantiate(tactics.Squad); _configs.Add(squad);
            var serialized = new SerializedObject(squad); serialized.FindProperty("<MaximumShooters>k__BackingField").intValue = maximumShooters; serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized = new SerializedObject(tactics); serialized.FindProperty("<Squad>k__BackingField").objectReferenceValue = squad; serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized = new SerializedObject(archetype); serialized.FindProperty("<Tactics>k__BackingField").objectReferenceValue = tactics; serialized.ApplyModifiedPropertiesWithoutUndo();
            for (int i = 0; i < count; i++)
            {
                Vector3 position = count <= 4 ? new Vector3(i * 4f, 0, -8f) : new Vector3(-12f + i % 7 * 4f, 0, -14f + i / 7 * 2f);
                var instance = Object.Instantiate(prefab, position, Quaternion.identity, Root.transform); instance.name = id + "_Rifleman_" + (i + 1);
                Actors[i] = instance.GetComponent<EnemyActor>(); Actors[i].ConfigureSquad(id);
                Brains[i] = instance.GetComponent<EnemyBrain>();
                serialized = new SerializedObject(instance.GetComponent<EnemyLifetimeScope>()); var actorArchetype = archetype;
                if (i == shortMagazineIndex)
                {
                    actorArchetype = Object.Instantiate(archetype); _configs.Add(actorArchetype);
                    var combat = Object.Instantiate(archetype.Combat); _configs.Add(combat);
                    var combatData = new SerializedObject(combat); combatData.FindProperty("<MagazineSize>k__BackingField").intValue = 3; combatData.ApplyModifiedPropertiesWithoutUndo();
                    var actorData = new SerializedObject(actorArchetype); actorData.FindProperty("<Combat>k__BackingField").objectReferenceValue = combat; actorData.ApplyModifiedPropertiesWithoutUndo();
                }
                serialized.FindProperty("_archetype").objectReferenceValue = actorArchetype; serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var targetObject = new GameObject("Tactical scenario player"); targetObject.layer = 6; targetObject.transform.SetParent(Root.transform); targetObject.transform.position = new Vector3(4,0,2);
            var body = targetObject.AddComponent<CapsuleCollider>(); body.height = 2f; body.radius = 0.4f; body.center = Vector3.up;
            targetObject.AddComponent<Health>().Configure(1000000f, false); Target = targetObject.AddComponent<PerceptionTarget>(); Target.Initialize(world, Faction.Player);
            Covers = scope.Container.Resolve<EnemyCoverService>();
            using (LifetimeScope.EnqueueParent(scope)) Root.SetActive(true);
            for (int i = 0; i < count; i++)
            {
                Contexts[i] = Brains[i].GetComponent<EnemyLifetimeScope>().Container.Resolve<EnemyContext>();
                int actorIndex = i; _fireHandlers[i] = () => Fired(actorIndex); Brains[i].Combat.Fired += _fireHandlers[i];
                Actors[i].Route = null;
            }
        }
        private void Fired(int index)
        {
            Shots++; var tactics = Brains[index].Tactics;
            if (tactics.CurrentCover != null && tactics.CoverPhase == EnemyCoverActionPhase.Protected) ProtectedShot = true;
            CoverExposedShot |= tactics.CurrentCover != null && tactics.CoverPhase == EnemyCoverActionPhase.Exposed;
            for (int i = 0; i < Brains.Length; i++)
                if (i != index && Brains[i].Tactics.Member != null && Brains[i].Tactics.Member.Mover && Actors[i].Navigation.Velocity.sqrMagnitude > 0.1f && tactics.Member != null && tactics.Member.Shooter) PressureMovementOverlap = true;
        }
        public void Observe()
        {
            MaximumReservations = Mathf.Max(MaximumReservations, Covers.ReservationCount);
            foreach (var brain in Brains)
            {
                if (!brain.gameObject.activeInHierarchy || brain.Tactics == null || brain.Tactics.Member == null) continue;
                var tactics = brain.Tactics;
                MaximumShooters = Mathf.Max(MaximumShooters, tactics.Squad.ActiveShooters); MaximumMovers = Mathf.Max(MaximumMovers, tactics.Squad.ActiveMovers);
                SawMover |= tactics.Member.Mover && brain.GetComponent<EnemyActor>().Navigation.Velocity.sqrMagnitude > 0.1f;
                SawFlank |= tactics.Member.FlankSide != 0 && tactics.Member.Mover;
                SawProtectedLow |= tactics.CurrentCover != null && tactics.CurrentCover.Kind == EnemyCoverKind.Low && tactics.CoverPhase == EnemyCoverActionPhase.Protected && brain.GetComponent<EnemyStance>().IsCrouching;
                SawProtectedHigh |= tactics.CurrentCover != null && tactics.CurrentCover.Kind == EnemyCoverKind.High && tactics.CoverPhase == EnemyCoverActionPhase.Protected;
                SawExposed |= tactics.CurrentCover != null && tactics.CoverPhase == EnemyCoverActionPhase.Exposed;
            }
        }
        public EnemyCoverPoint AddCover(EnemyCoverKind kind, float x, float z)
        {
            var obstacle = Wall(new Vector3(x, kind == EnemyCoverKind.Low ? 0.625f : 1.5f, z + 0.9f), new Vector3(2f, kind == EnemyCoverKind.Low ? 1.25f : 3f, 0.6f), false);
            var pointObject = new GameObject(kind + " protected point"); pointObject.transform.SetParent(Root.transform); pointObject.transform.position = new Vector3(x,0,z);
            Transform exposure = null;
            if (kind == EnemyCoverKind.High) { var exposed = new GameObject("High exposure point"); exposed.transform.SetParent(Root.transform); exposed.transform.position = new Vector3(x + 3.2f,0,z); exposure = exposed.transform; }
            var point = pointObject.AddComponent<EnemyCoverPoint>(); point.Configure(kind, obstacle.GetComponent<Collider>(), exposure); _points.Add(point); Covers.Register(point); return point;
        }
        public GameObject Wall(Vector3 position, Vector3 scale, bool carve)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "Runtime tactical barrier"; wall.transform.SetParent(Root.transform); wall.transform.position = position; wall.transform.localScale = scale;
            if (carve) { var obstacle = wall.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one; obstacle.carving = true; obstacle.carvingTimeToStationary = 0.1f; }
            return wall;
        }
        public GameObject FriendlyBlocker(EnemyWorld world, Vector3 position)
        {
            var friendly = new GameObject("Runtime friendly lane blocker"); friendly.transform.SetParent(Root.transform); friendly.layer = 8; friendly.transform.position = position;
            friendly.AddComponent<BoxCollider>().size = Vector3.one * 1.5f; friendly.AddComponent<Health>(); friendly.AddComponent<PerceptionTarget>().Initialize(world, Faction.Hostile); return friendly;
        }
        public void Dispose()
        {
            Root.SetActive(false);
            for (int i = 0; i < Brains.Length; i++) Brains[i].Combat.Fired -= _fireHandlers[i];
            foreach (var point in _points) Covers.Unregister(point);
            Object.Destroy(Root); foreach (var config in _configs) Object.Destroy(config);
        }
    }
}
