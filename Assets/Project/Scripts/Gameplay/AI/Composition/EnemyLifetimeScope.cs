using System;
using Breachpoint.Composition;
using UnityEngine;
using VContainer;
using VContainer.Unity;
namespace Breachpoint.Gameplay.AI
{
    [RequireComponent(typeof(EnemyActor), typeof(EnemyBrain))]
    public sealed class EnemyLifetimeScope : LifetimeScope
    {
        [SerializeField] private EnemyArchetypeConfig _archetype;
        public EnemyArchetypeConfig Archetype => _archetype;
        private void Reset() => parentReference = ParentReference.Create<GameLifetimeScope>();
        protected override void Configure(IContainerBuilder builder)
        {
            if (_archetype == null || !_archetype.IsValid) throw new InvalidOperationException($"{name}: incomplete enemy archetype.");
            EnemyActor actor = GetComponent<EnemyActor>();
            IEnemyWeapon weapon = GetComponent<IEnemyWeapon>();
            if (weapon == null) throw new InvalidOperationException($"{name}: an IEnemyWeapon component is required.");
            builder.RegisterInstance(_archetype);
            builder.Register<EnemyBlackboard>(Lifetime.Scoped);
            builder.Register<EnemyContext>(resolver =>
            {
                actor.Initialize(_archetype, resolver.Resolve<EnemyWorld>());
                weapon.Initialize(actor, _archetype.Combat);
                var context = new EnemyContext(actor, _archetype, resolver.Resolve<EnemyBlackboard>());
                context.Combat = new EnemyCombat(context, weapon, resolver.Resolve<EnemyWorld>());
                return context;
            }, Lifetime.Scoped);
            builder.Register<EnemyPerception>(Lifetime.Scoped);
            builder.Register<EnemyDecisionPolicy>(Lifetime.Scoped);
            builder.Register<EnemyStateMachine>(Lifetime.Scoped);
            builder.RegisterComponent(GetComponent<EnemyBrain>());
        }
    }
}
