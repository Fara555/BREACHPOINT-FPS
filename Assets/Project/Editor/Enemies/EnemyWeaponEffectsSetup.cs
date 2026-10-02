using System;
using System.IO;
using System.Linq;
using System.Text;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Weapons.Effects;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static class EnemyWeaponEffectsSetup
    {
        private const string PlayerPath = "Assets/Project/Prefabs/Player.prefab";
        private const string PrefabFolder = "Assets/Project/Enemies/Prefabs";
        private const string ReportFolder = "Docs/AI/EnemyEffects";

        [MenuItem("Breachpoint/Enemies/Weapon effects/Audit")]
        public static void Audit()
        {
            var report = new StringBuilder();
            report.AppendLine("Enemy weapon effects | " + DateTime.UtcNow.ToString("O"));
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            foreach (WeaponMuzzleFlash flash in player.GetComponentsInChildren<WeaponMuzzleFlash>(true))
            {
                report.AppendLine("Player muzzle: " + AnimationUtility.CalculateTransformPath(flash.transform, player.transform));
                report.AppendLine("Scale: " + flash.transform.localScale + " | " + EditorJsonUtility.ToJson(flash));
                foreach (Transform child in flash.GetComponentsInChildren<Transform>(true))
                    report.AppendLine("  " + child.name + " | " + string.Join(", ", child.GetComponents<Component>().Select(c => c.GetType().Name)));
            }
            foreach (WeaponProjectileTracerPool tracer in player.GetComponentsInChildren<WeaponProjectileTracerPool>(true))
                report.AppendLine("Player tracer: " + EditorJsonUtility.ToJson(tracer));
            foreach (string path in PrefabPaths())
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                report.AppendLine(path);
                foreach (Component effect in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (effect is ParticleSystem || effect is LineRenderer || effect is WeaponMuzzleFlash || effect is WeaponProjectileTracerPool)
                        report.AppendLine("  " + AnimationUtility.CalculateTransformPath(effect.transform, prefab.transform) + " | " + effect.GetType().Name);
                }
                report.AppendLine(EditorJsonUtility.ToJson(prefab.GetComponent<EnemyVfxPresenter>()));
            }
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllText(ReportFolder + "/asset-audit.txt", report.ToString());
        }

        internal static void Validate(GameObject root)
        {
            if (root.GetComponentsInChildren<ParticleSystem>(true).Length != 0)
                throw new InvalidOperationException("Legacy hit/muzzle particles remain: " + root.name);
            if (root.GetComponentsInChildren<LineRenderer>(true).Length != 0)
                throw new InvalidOperationException("Legacy static tracer remains: " + root.name);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                    throw new InvalidOperationException("Missing script: " + child.name);
            if (root.GetComponent<EnemyHitscanWeapon>() == null) return;
            var presenter = new SerializedObject(root.GetComponent<EnemyVfxPresenter>());
            var flash = presenter.FindProperty("_muzzleFlashEffect").objectReferenceValue as WeaponMuzzleFlash;
            var tracer = presenter.FindProperty("_tracerPool").objectReferenceValue as WeaponProjectileTracerPool;
            if (flash == null || tracer == null || flash.transform.parent != root.GetComponent<EnemyActor>().Muzzle)
                throw new InvalidOperationException("Enemy effects are not wired to the actual muzzle.");
            if (new SerializedObject(flash).FindProperty("_sparksAsset").objectReferenceValue == null ||
                new SerializedObject(flash).FindProperty("_flareMaterial").objectReferenceValue == null ||
                new SerializedObject(tracer).FindProperty("_tracerMaterial").objectReferenceValue == null)
                throw new InvalidOperationException("An existing VFX asset/material was not assigned.");
        }

        private static string[] PrefabPaths() => AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<EnemyVfxPresenter>() != null).ToArray();
    }
}
