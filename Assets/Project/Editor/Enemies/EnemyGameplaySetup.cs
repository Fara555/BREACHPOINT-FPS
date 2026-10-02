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
    }
}
