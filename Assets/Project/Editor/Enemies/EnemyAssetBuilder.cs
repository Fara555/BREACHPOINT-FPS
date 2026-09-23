using System;
using System.IO;
using Breachpoint.Audio;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class EnemyAssetBuilder
    {
        public const string Root = "Assets/Project/Enemies";
        public const string ScenePath = Root + "/EnemyArena.unity";

        [MenuItem("Breachpoint/Enemies/Create demo assets")]
        public static void CreateAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            { Debug.Log("Enemy demo already exists; assets were preserved."); return; }
            Directory.CreateDirectory(Root + "/Configs"); Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Presentation"); AssetDatabase.Refresh();
            var movement = Asset<EnemyMovementConfig>("Configs/Movement");
            var perception = Asset<EnemyPerceptionConfig>("Configs/Perception");
            var decision = Asset<EnemyDecisionConfig>("Configs/Decision");
            var rifleCombat = Asset<EnemyCombatConfig>("Configs/RifleCombat");
            var meleeCombat = Asset<EnemyCombatConfig>("Configs/MeleeCombat");
            Set(meleeCombat, "Range", 2.2f); Set(meleeCombat, "PreferredRange", 1.5f); Set(meleeCombat, "MinimumRange", 0f);
            Set(meleeCombat, "Damage", 18f); Set(meleeCombat, "FireInterval", 1f); Set(meleeCombat, "BurstLength", 1);
            Set(meleeCombat, "ReactionTime", 0.6f);
            var rifle = Archetype("Rifleman", movement, perception, decision, rifleCombat, 100f);
            var meleeMovement = Asset<EnemyMovementConfig>("Configs/MeleeMovement"); Set(meleeMovement, "RunSpeed", 6f);
            var melee = Archetype("Melee", meleeMovement, perception, decision, meleeCombat, 150f);
            Material rifleMaterial = Material("RifleArmor", new Color(0.12f, 0.27f, 0.33f));
            Material meleeMaterial = Material("MeleeArmor", new Color(0.55f, 0.18f, 0.08f));
            Material darkMaterial = Material("Joints", new Color(0.04f, 0.05f, 0.06f));
            Material glowMaterial = Material("Visor", new Color(0.1f, 1f, 0.85f), true);
            AnimatorController controller = AnimationController();
            GameObject riflePrefab = EnemyPrefab("Rifleman", rifle, rifleMaterial, darkMaterial, glowMaterial, controller, false);
            GameObject meleePrefab = EnemyPrefab("Melee", melee, meleeMaterial, darkMaterial, glowMaterial, controller, true);
            BuildArena(riflePrefab, meleePrefab, darkMaterial);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("Enemy assets created: " + ScenePath);
        }

        private static T Asset<T>(string name) where T : ScriptableObject
        {
            string path = Root + "/" + name + ".asset";
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            T asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        private static EnemyArchetypeConfig Archetype(string name, EnemyMovementConfig move, EnemyPerceptionConfig perception,
            EnemyDecisionConfig decision, EnemyCombatConfig combat, float health)
        {
            var asset = Asset<EnemyArchetypeConfig>("Configs/" + name);
            Set(asset, "Id", name.ToLowerInvariant()); Set(asset, "MaximumHealth", health);
            Set(asset, "Movement", move); Set(asset, "Perception", perception); Set(asset, "Decision", decision); Set(asset, "Combat", combat);
            return asset;
        }
        public static void Set(Object obj, string name, object value)
        {
            var serialized = new SerializedObject(obj);
            SerializedProperty property = serialized.FindProperty(name) ?? serialized.FindProperty("<" + name + ">k__BackingField");
            if (property == null) throw new InvalidOperationException(obj.GetType().Name + "." + name);
            if (value is Object reference) property.objectReferenceValue = reference;
            else if (value is float number) property.floatValue = number;
            else if (value is int integer) property.intValue = integer;
            else if (value is bool boolean) property.boolValue = boolean;
            else if (value is string text) property.stringValue = text;
            else if (value == null) property.objectReferenceValue = null;
            else throw new ArgumentException(name);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Material Material(string name, Color color, bool emissive = false)
        {
            string path = Root + "/Presentation/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path); if (existing != null) return existing;
            Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (emissive)
            {
                if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", color * 3f);
                if (material.HasProperty("_EmissionColor")) { material.SetColor("_EmissionColor", color * 3f); material.EnableKeyword("_EMISSION"); }
            }
            AssetDatabase.CreateAsset(material, path); return material;
        }
        private static GameObject Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collision = false)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }
        private static Transform Pivot(string name, Transform parent, Vector3 position)
        {
            var pivot = new GameObject(name).transform; pivot.SetParent(parent, false); pivot.localPosition = position; return pivot;
        }
        private static GameObject EnemyPrefab(string name, EnemyArchetypeConfig config, Material armor, Material joint, Material glow,
            AnimatorController controller, bool melee)
        {
            string path = Root + "/Prefabs/" + name + ".prefab";
            var root = new GameObject(name); root.SetActive(false); root.layer = 8;
            var capsule = root.AddComponent<CapsuleCollider>(); capsule.center = Vector3.up; capsule.height = 2f; capsule.radius = 0.35f;
            var health = root.AddComponent<Health>(); Set(health, "_maximumHealth", config.MaximumHealth);
            var target = root.AddComponent<PerceptionTarget>(); Set(target, "_faction", (int)Faction.Hostile);
            var agent = root.AddComponent<NavMeshAgent>(); agent.height = 2f; agent.radius = 0.35f; agent.baseOffset = 0f;
            root.AddComponent<EnemyNavigation>(); var actor = root.AddComponent<EnemyActor>(); root.AddComponent<EnemyBrain>();
            if (melee) root.AddComponent<EnemyMeleeWeapon>(); else root.AddComponent<EnemyHitscanWeapon>();
            var scope = root.AddComponent<EnemyLifetimeScope>(); scope.parentReference = ParentReference.Create<GameLifetimeScope>(); Set(scope, "_archetype", config);
            Transform eyes = Pivot("Eyes", root.transform, new Vector3(0, 1.7f, 0.12f));
            Transform muzzle = Pivot("Muzzle", root.transform, new Vector3(0.24f, 1.35f, 0.85f));
            Set(actor, "Eyes", eyes); Set(actor, "Muzzle", muzzle); Set(target, "_aimPoint", eyes);
            Transform model = Pivot("Model", root.transform, Vector3.zero);
            Part("Torso", model, new Vector3(0, 1.25f, 0), new Vector3(0.68f, 0.6f, 0.38f), armor);
            Part("Head", model, new Vector3(0, 1.8f, 0), new Vector3(0.4f, 0.38f, 0.36f), armor);
            Part("Visor", model, new Vector3(0, 1.84f, 0.19f), new Vector3(0.32f, 0.08f, 0.025f), glow);
            Part("Belt", model, new Vector3(0, 0.9f, 0), new Vector3(0.5f, 0.2f, 0.32f), joint);
            foreach (int sign in new[] { -1, 1 })
            {
                string side = sign < 0 ? "Left" : "Right";
                Transform leg = Pivot(side + "Leg", model, new Vector3(sign * 0.19f, 0.85f, 0));
                Part("Armor", leg, new Vector3(0, -0.35f, 0), new Vector3(0.25f, 0.65f, 0.28f), armor);
                Part("Foot", leg, new Vector3(0, -0.75f, 0.07f), new Vector3(0.26f, 0.15f, 0.4f), joint);
                Transform arm = Pivot(side + "Arm", model, new Vector3(sign * 0.48f, 1.52f, 0));
                Part("Armor", arm, new Vector3(0, -0.3f, 0.08f), new Vector3(0.23f, 0.6f, 0.28f), armor);
            }
            if (!melee) Part("Rifle", model, new Vector3(0.24f, 1.3f, 0.5f), new Vector3(0.13f, 0.2f, 0.7f), joint);
            var animator = model.gameObject.AddComponent<Animator>(); animator.runtimeAnimatorController = controller;
            var animation = root.AddComponent<EnemyAnimationBridge>(); Set(animation, "_animator", animator);
            var vfx = root.AddComponent<EnemyVfxPresenter>(); var audio = root.AddComponent<AudioSource>();
            audio.spatialBlend = 1f; audio.playOnAwake = false; Set(vfx, "_audioSource", audio);
            ParticleSystem flash = Effect("MuzzleFlash", muzzle, new Color(1f, 0.7f, 0.1f), 0.07f, 0.1f, glow);
            ParticleSystem hit = Effect("Hit", root.transform, new Color(1f, 0.3f, 0.08f), 0.07f, 0.25f, glow);
            Set(vfx, "_hitEffect", hit);
            if (!melee)
            {
                Set(vfx, "_muzzleFlash", flash);
                var line = new GameObject("Tracer"); line.transform.SetParent(root.transform, false);
                var tracer = line.AddComponent<LineRenderer>(); tracer.sharedMaterial = glow; tracer.widthMultiplier = 0.015f;
                tracer.useWorldSpace = true; tracer.enabled = false; tracer.positionCount = 2; Set(vfx, "_tracer", tracer);
            }
            root.AddComponent<EnemyDebugView>();
            root.SetActive(true);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return prefab;
        }
        private static ParticleSystem Effect(string name, Transform parent, Color color, float size, float lifetime, Material material)
        {
            Transform child = Pivot(name, parent, Vector3.zero); var effect = child.gameObject.AddComponent<ParticleSystem>();
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = effect.main; main.loop = false; main.playOnAwake = false; main.duration = 0.15f;
            main.startLifetime = lifetime; main.startSpeed = 2f; main.startSize = size; main.startColor = color; main.maxParticles = 24;
            var emission = effect.emission; emission.rateOverTime = 0; emission.SetBursts(new[] { new ParticleSystem.Burst(0, 10) });
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = material; return effect;
        }
        private static AnimatorController AnimationController()
        {
            string path = Root + "/Presentation/Enemy.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path); if (existing != null) return existing;
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float); controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Reloading", AnimatorControllerParameterType.Bool); controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var idle = machine.AddState("Idle"); var walk = machine.AddState("Locomotion"); var dead = machine.AddState("Dead");
            var attack = machine.AddState("Attack"); var reload = machine.AddState("Reload"); machine.defaultState = idle;
            idle.motion = Clip("Idle", "", "localPosition.y", new[] { 0f, 0.025f, 0f }, 1.6f, true);
            var walkClip = Clip("Walk", "LeftLeg", "localEulerAnglesRaw.x", new[] { -25f, 25f, -25f }, 0.6f, true);
            walkClip.SetCurve("RightLeg", typeof(Transform), "localEulerAnglesRaw.x", Curve(new[] { 25f, -25f, 25f }, 0.6f));
            walk.motion = walkClip;
            dead.motion = Clip("Death", "", "localEulerAnglesRaw.x", new[] { 0f, -45f, -90f }, 0.55f, false);
            attack.motion = Clip("Attack", "RightArm", "localEulerAnglesRaw.x", new[] { 0f, -45f, 0f }, 0.3f, false);
            reload.motion = Clip("Reload", "LeftArm", "localEulerAnglesRaw.x", new[] { 0f, -60f, 0f }, 1f, true);
            Transition(idle, walk, "Speed", AnimatorConditionMode.Greater, 0.1f);
            Transition(walk, idle, "Speed", AnimatorConditionMode.Less, 0.1f);
            var deathTransition = machine.AddAnyStateTransition(dead); deathTransition.hasExitTime = false; deathTransition.duration = 0.05f;
            deathTransition.canTransitionToSelf = false; deathTransition.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            var attackTransition = machine.AddAnyStateTransition(attack); attackTransition.hasExitTime = false; attackTransition.duration = 0.04f;
            attackTransition.canTransitionToSelf = false; attackTransition.AddCondition(AnimatorConditionMode.If, 0, "Attack"); attackTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            var finish = attack.AddTransition(idle); finish.hasExitTime = true; finish.exitTime = 1f; finish.duration = 0.1f;
            var reloadTransition = machine.AddAnyStateTransition(reload); reloadTransition.hasExitTime = false; reloadTransition.duration = 0.1f;
            reloadTransition.canTransitionToSelf = false; reloadTransition.AddCondition(AnimatorConditionMode.If, 0, "Reloading"); reloadTransition.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            Transition(reload, idle, "Reloading", AnimatorConditionMode.IfNot, 0);
            return controller;
        }
        private static void Transition(AnimatorState from, AnimatorState to, string parameter, AnimatorConditionMode mode, float threshold)
        { var t = from.AddTransition(to); t.hasExitTime = false; t.duration = 0.1f; t.AddCondition(mode, threshold, parameter); }
        private static AnimationCurve Curve(float[] values, float duration)
        {
            var keys = new Keyframe[values.Length]; for (int i = 0; i < values.Length; i++) keys[i] = new Keyframe(duration * i / (values.Length - 1), values[i]);
            return new AnimationCurve(keys);
        }
        private static AnimationClip Clip(string name, string path, string property, float[] values, float duration, bool loop)
        {
            var clip = new AnimationClip { name = name, frameRate = 30 };
            clip.SetCurve(path, typeof(Transform), property, Curve(values, duration));
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = loop; AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.CreateAsset(clip, Root + "/Presentation/" + name + ".anim"); return clip;
        }
        private static void BuildArena(GameObject rifle, GameObject melee, Material material)
        {
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                Application.isBatchMode && string.IsNullOrEmpty(original.path) ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var surface = new GameObject("Navigation").AddComponent<NavMeshSurface>(); surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.collectObjects = CollectObjects.Children;
            Part("Arena floor", surface.transform, new Vector3(0, -0.25f, 0), new Vector3(36, 0.5f, 36), material, true);
            Part("Sight blocker", surface.transform, new Vector3(0, 1.5f, 1), new Vector3(5, 3, 0.8f), material, true);
            Part("Cover", surface.transform, new Vector3(-7, 0.6f, 0), new Vector3(2, 1.2f, 4), material, true);
            surface.layerMask = 1; surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData, Root + "/ArenaNavMesh.asset");
            var game = new GameObject("GameLifetimeScope"); game.SetActive(false);
            var audio = game.AddComponent<AudioService>(); var scope = game.AddComponent<GameLifetimeScope>(); Set(scope, "_audioService", audio); game.SetActive(true);
            var route = new GameObject("PatrolRoute").AddComponent<PatrolRoute>();
            var routeSerialized = new SerializedObject(route); var points = routeSerialized.FindProperty("_points"); points.arraySize = 4;
            Vector3[] positions = { new Vector3(-8, 0, 7), new Vector3(8, 0, 7), new Vector3(8, 0, -7), new Vector3(-8, 0, -7) };
            for (int i = 0; i < positions.Length; i++) points.GetArrayElementAtIndex(i).objectReferenceValue = Pivot("Point " + i, route.transform, positions[i]);
            routeSerialized.ApplyModifiedPropertiesWithoutUndo();
            GameObject a = (GameObject)PrefabUtility.InstantiatePrefab(rifle, scene); a.transform.position = new Vector3(-8, 0, 7); a.GetComponent<EnemyActor>().Route = route;
            GameObject b = (GameObject)PrefabUtility.InstantiatePrefab(melee, scene); b.transform.position = new Vector3(8, 0, 7); b.GetComponent<EnemyActor>().Route = route;
            var camera = new GameObject("Overview camera").AddComponent<Camera>(); camera.transform.position = new Vector3(18, 20, -22); camera.transform.LookAt(Vector3.zero);
            var light = new GameObject("Sun").AddComponent<Light>(); light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(45, -35, 0); light.intensity = 2f;
            EditorSceneManager.SaveScene(scene, ScenePath);
            if (original.IsValid() && original.isLoaded) { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(original); }
        }
    }
}
