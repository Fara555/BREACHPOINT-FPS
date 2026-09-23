namespace Breachpoint.Gameplay.AI
{
    public sealed class EnemyContext
    {
        public EnemyActor Actor { get; }
        public EnemyArchetypeConfig Config { get; }
        public EnemyBlackboard Memory { get; }
        public EnemyNavigation Navigation => Actor.Navigation;
        public EnemyCombat Combat { get; set; }
        public float Now { get; set; }
        public EnemyContext(EnemyActor actor, EnemyArchetypeConfig config, EnemyBlackboard memory)
        { Actor = actor; Config = config; Memory = memory; }
    }
}
