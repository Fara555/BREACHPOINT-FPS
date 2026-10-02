using System.Collections;
using System.Linq;
using Breachpoint.Composition;
using Breachpoint.Gameplay.AI;
using Breachpoint.Gameplay.Combat;
using Breachpoint.Gameplay.Weapons;
using Breachpoint.Gameplay.Weapons.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Breachpoint.Editor.Enemies
{
    public static partial class AdamPresentationIntegration
    {
        [MenuItem("Breachpoint/Enemies/Weapon effects/Validate in Play Mode")]
        public static void ValidateWeaponEffects() => ValidateStage(60);

        private static IEnumerator WeaponEffectsTests(GameLifetimeScope scope, EnemyWorld world)
        {
            EnemyWeaponEffectsSetup.Validate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath));
            EnemyWeaponEffectsSetup.Validate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Enemies/Prefabs/Melee.prefab"));
            PlayCheck(true, "Both enemy prefabs contain no legacy particles/static tracer or missing scripts");
            GameObject enemy;
            using (LifetimeScope.EnqueueParent(scope))
                enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RiflemanPath), new Vector3(0f, 0f, -14f), Quaternion.identity);
            GameObject player = new GameObject("Weapon effects validation target");
            player.layer = 6;
            player.transform.position = new Vector3(0f, 0f, -8f);
            CapsuleCollider body = player.AddComponent<CapsuleCollider>();
            body.center = Vector3.up; body.height = 2f; body.radius = 0.45f;
            Health targetHealth = player.AddComponent<Health>();
            targetHealth.Configure(10000f, false);
            PerceptionTarget target = player.AddComponent<PerceptionTarget>();
            target.Initialize(world, Faction.Player);
            yield return null;
            EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
            brain.enabled = false;
            EnemyActor actor = enemy.GetComponent<EnemyActor>();
            actor.Navigation.Stop();
            WeaponMuzzleFlash flash = enemy.GetComponentInChildren<WeaponMuzzleFlash>(true);
            WeaponProjectileTracerPool pool = enemy.GetComponentInChildren<WeaponProjectileTracerPool>(true);
            // Awake builds renderers once; subsequent shots must reuse exactly these instances.
            LineRenderer[] lines = pool.GetComponentsInChildren<LineRenderer>(true);
            MeshRenderer flare = flash.GetComponentInChildren<MeshRenderer>(true);
            VisualEffect sparks = flash.GetComponentInChildren<VisualEffect>(true);
            Light light = flash.GetComponentInChildren<Light>(true);
            EnemyHitscanWeapon weapon = enemy.GetComponent<EnemyHitscanWeapon>();
            WeaponShotResult shot = default;
            int shots = 0;
            weapon.Attacked += result => { shot = result; shots++; };
            PlayCheck(lines.Length == 16 && lines.All(line => !line.enabled), "Existing 16-entry tracer pool starts hidden");
            PlayCheck(!flare.enabled && sparks != null && light != null && !light.enabled, "Existing muzzle flare, sparks graph and light start idle");
            PlayCheck(flash.transform.parent == actor.Muzzle && flash.gameObject.layer == enemy.layer, "Muzzle effect is attached to SCIFIAR muzzle on the enemy rendering layer");
            Physics.SyncTransforms();
            float healthBefore = targetHealth.CurrentHealth;
            PlayCheck(weapon.Attack(target, 0f), "Real enemy hitscan attack succeeds");
            PlayCheck(shots == 1 && targetHealth.CurrentHealth < healthBefore, "One real shot delivers damage and one presentation event");
            PlayCheck(flare.enabled && light.enabled && sparks.enabled, "Real shot activates existing flare/light/VFX Graph");
            CapturePresentation(enemy, "weapon-effects-shot");
            LineRenderer active = lines.Single(line => line.enabled);
            PlayCheck(Vector3.Distance(active.GetPosition(0), shot.Origin) < 0.001f &&
                Vector3.Dot(active.GetPosition(1) - shot.Origin, shot.EndPoint - shot.Origin) > 0f, "Trace starts at actual muzzle and follows hitscan result");
            foreach (var wait in WaitEnumerable(0.15f)) yield return wait;
            PlayCheck(lines.All(line => !line.enabled) && !flare.enabled && !light.enabled, "Short shot trace and muzzle light/flare expire");
            for (int i = 0; i < 40; i++) weapon.Attack(target, 0f);
            PlayCheck(shots == 41 && pool.GetComponentsInChildren<LineRenderer>(true).Length == 16, "Repeated shots reuse bounded pool without creating renderers");
            brain.ResetForSpawn();
            PlayCheck(lines.All(line => !line.enabled) && !flare.enabled && !light.enabled, "Active enemy reset clears all transient effects");
            Physics.SyncTransforms();
            weapon.Attack(target, 0f);
            actor.Health.TakeDamage(new DamageInfo(1f, actor.Eyes.position, Vector3.back, player));
            PlayCheck(enemy.GetComponentsInChildren<ParticleSystem>(true).Length == 0, "Nonlethal enemy damage creates no hit particles");
            actor.Health.TakeDamage(new DamageInfo(100000f, actor.Eyes.position, Vector3.back, player));
            PlayCheck(lines.All(line => !line.enabled) && !flare.enabled && !light.enabled, "Death clears outstanding shot effects");
            enemy.SetActive(false);
            enemy.SetActive(true);
            yield return null;
            brain.ResetForSpawn();
            brain.enabled = false;
            Physics.SyncTransforms();
            int beforeReuse = shots;
            PlayCheck(weapon.Attack(target, 0f), "Pooled enemy fires again after death/reset");
            PlayCheck(shots == beforeReuse + 1 && lines.Count(line => line.enabled) == 1, "Re-enable restores a single subscription and one trace per shot");
            enemy.SetActive(false);
            PlayCheck(lines.All(line => !line.enabled) && !flare.enabled && !light.enabled, "Disable immediately hides pooled effects");
            enemy.SetActive(true);
            yield return null;
            brain.ResetForSpawn();
            brain.enabled = false;
            // Optional effects must not affect weapon damage or attack availability.
            var presenter = new SerializedObject(enemy.GetComponent<EnemyVfxPresenter>());
            presenter.FindProperty("_muzzleFlashEffect").objectReferenceValue = null;
            presenter.FindProperty("_tracerPool").objectReferenceValue = null;
            presenter.ApplyModifiedPropertiesWithoutUndo();
            Physics.SyncTransforms();
            healthBefore = targetHealth.CurrentHealth;
            PlayCheck(weapon.Attack(target, 0f) && targetHealth.CurrentHealth < healthBefore, "Missing optional VFX references preserve hitscan damage");
            // The same shared components remain compatible with the player's existing presentation.
            flash.Play();
            pool.Play(Vector3.zero, Vector3.forward * 10f);
            flash.Clear(); pool.Clear();
            PlayCheck(lines.All(line => !line.enabled) && !flare.enabled && !light.enabled, "Shared effects retain direct Play/Clear behavior");
            Object.Destroy(enemy);
            Object.Destroy(player);
            yield return null;
            PlayCheck(enemy == null && player == null, "Scene fixture and effect pools destroy cleanly");
            AppendResult("Weapon effect assertions complete; no scene or Animator assets were saved.");
        }
    }
}
