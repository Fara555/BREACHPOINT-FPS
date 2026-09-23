using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System.Collections;
using Breachpoint.Gameplay.Weapons;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class EnemyGameplaySetup
    {
        public static void DisableArenaPlayer()
        {
            foreach (var player in Object.FindObjectsByType<Breachpoint.Gameplay.Player.Composition.PlayerLifetimeScope>())
                player.gameObject.SetActive(false);
        }
        public static IEnumerator ValidatePlayerCombat(LifetimeScope root, EnemyBrain enemy)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/Player.prefab");
            // Add the adapter before the scope builds, exactly as on the saved arena instance.
            var staging = new GameObject("Inactive player staging"); staging.SetActive(false);
            var player = Object.Instantiate(prefab, staging.transform); Connect(player, false);
            player.transform.SetPositionAndRotation(new Vector3(0, 2, -8), Quaternion.Euler(0, 180, 0));
            using (LifetimeScope.EnqueueParent(root)) player.transform.SetParent(null, true);
            yield return null;
            var weapon = player.GetComponentInChildren<HitscanWeapon>(true);
            if (weapon == null) throw new System.InvalidOperationException("Player missing original HitscanWeapon.");
            var serialized = new SerializedObject(weapon);
            var camera = (Camera)serialized.FindProperty("_aimCamera").objectReferenceValue;
            camera.transform.LookAt(enemy.GetComponent<EnemyActor>().Target.AimPosition);
            Physics.SyncTransforms(); float before = enemy.GetComponent<Health>().CurrentHealth;
            weapon.Fire(true);
            if (enemy.GetComponent<Health>().CurrentHealth >= before) throw new System.InvalidOperationException("Original player hitscan did not damage enemy.");
            if (!enemy.Memory.HasNoise) throw new System.InvalidOperationException("Player gunshot did not emit a noise stimulus.");
            var target = player.GetComponent<PerceptionTarget>(); var enemyWeapon = enemy.GetComponent<IEnemyWeapon>();
            enemy.transform.LookAt(new Vector3(player.transform.position.x, enemy.transform.position.y, player.transform.position.z));
            Physics.SyncTransforms(); float playerBefore = target.Health.CurrentHealth;
            for (int i = 0; i < 8 && target.Health.CurrentHealth == playerBefore; i++) enemyWeapon.Attack(target, 0f);
            if (target.Health.CurrentHealth >= playerBefore) throw new System.InvalidOperationException("Enemy could not damage the original player.");
            player.SetActive(false); Object.Destroy(player); Object.Destroy(staging);
        }
        [MenuItem("Breachpoint/Enemies/Connect selected player")]
        public static void ConnectSelectedPlayer()
        {
            GameObject player = Selection.activeGameObject;
            if (player == null || player.GetComponent<Breachpoint.Gameplay.Player.Composition.PlayerLifetimeScope>() == null)
            { Debug.LogError("Select the PlayerLifetimeScope root in the scene."); return; }
            Connect(player, true);
            EditorSceneManager.MarkSceneDirty(player.scene);
        }
        public static void Connect(GameObject player, bool undo)
        {
            if (player.GetComponent<Health>() == null) { if (undo) Undo.AddComponent<Health>(player); else player.AddComponent<Health>(); }
            if (player.GetComponent<PerceptionTarget>() == null) { if (undo) Undo.AddComponent<PerceptionTarget>(player); else player.AddComponent<PerceptionTarget>(); }
            if (player.GetComponent<PlayerEnemyBridge>() == null) { if (undo) Undo.AddComponent<PlayerEnemyBridge>(player); else player.AddComponent<PlayerEnemyBridge>(); }
        }
        [MenuItem("Breachpoint/Enemies/Add player to demo arena")]
        public static void AddPlayerToArena()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/Player.prefab");
            if (prefab == null) throw new System.InvalidOperationException("Player prefab not found.");
            Scene original = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(EnemyAssetBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                bool hasPlayer = false;
                foreach (GameObject root in scene.GetRootGameObjects())
                    if (root.GetComponent<Breachpoint.Gameplay.Player.Composition.PlayerLifetimeScope>() != null) hasPlayer = true;
                if (!hasPlayer)
                {
                    var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); player.transform.position = new Vector3(0, 2f, -12f);
                    Connect(player, false);
                }
                foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == "Overview camera") root.SetActive(false);
                ConfigureLighting(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (original.IsValid()) SceneManager.SetActiveScene(original); }
        }

        private static void ConfigureLighting(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Light sun = root.GetComponent<Light>();
                if (sun == null || sun.type != LightType.Directional) continue;
                if (sun.GetComponent<HDAdditionalLightData>() == null) sun.gameObject.AddComponent<HDAdditionalLightData>();
                sun.lightUnit = LightUnit.Lux; sun.intensity = 12000f;
            }
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(EnemyAssetBuilder.Root + "/Presentation/ArenaVolume.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, EnemyAssetBuilder.Root + "/Presentation/ArenaVolume.asset");
                var exposure = profile.Add<Exposure>(true); exposure.mode.Override(ExposureMode.Fixed); exposure.fixedExposure.Override(9f);
                AssetDatabase.AddObjectToAsset(exposure, profile); EditorUtility.SetDirty(profile);
            }
            Volume volume = null;
            foreach (GameObject root in scene.GetRootGameObjects()) if (root.name == "Arena exposure") volume = root.GetComponent<Volume>();
            if (volume == null) volume = new GameObject("Arena exposure").AddComponent<Volume>();
            volume.isGlobal = true; volume.sharedProfile = profile;
            AssetDatabase.SaveAssets();
        }

        public static void RenderPreviewBatch()
        {
            Scene scene = EditorSceneManager.OpenScene(EnemyAssetBuilder.ScenePath);
            ConfigureLighting(scene); EditorSceneManager.SaveScene(scene);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.GetComponent<Breachpoint.Gameplay.Player.Composition.PlayerLifetimeScope>() != null) root.SetActive(false);
                var enemy = root.GetComponent<EnemyActor>();
                if (enemy != null) { root.transform.position = new Vector3(root.name.Contains("Melee") ? 1.1f : -1.1f, 0, -6f); root.transform.rotation = Quaternion.identity; }
            }
            var camera = new GameObject("Preview camera").AddComponent<Camera>();
            var data = camera.gameObject.AddComponent<HDAdditionalCameraData>(); data.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            data.backgroundColorHDR = new Color(0.025f, 0.035f, 0.05f);
            camera.transform.position = new Vector3(4, 2.8f, 0); camera.transform.LookAt(new Vector3(0, 1f, -6f));
            camera.fieldOfView = 34f;
            var texture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); texture.Create();
            var request = new RenderPipeline.StandardRequest { destination = texture };
            ShaderUtil.allowAsyncCompilation = false;
            for (int i = 0; i < 4; i++) RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture previous = RenderTexture.active; RenderTexture.active = texture;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            System.IO.Directory.CreateDirectory("Logs"); System.IO.File.WriteAllBytes("Logs/EnemyPreview.png", image.EncodeToPNG());
            RenderTexture.active = previous; Object.DestroyImmediate(image); texture.Release(); Object.DestroyImmediate(texture);
            EditorApplication.Exit(0);
        }
    }
}
