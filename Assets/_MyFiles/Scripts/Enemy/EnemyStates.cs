namespace Brawler.Enemy
{
    /// <summary>One instance of each enemy state, allocated once per enemy and reused.</summary>
    public sealed class EnemyStates
    {
        // ============ Construction ============

        /// <summary>Creates all state instances.</summary>
        public EnemyStates()
        {
            Idle = new EnemyIdleState();
            Chase = new EnemyChaseState();
            Attack = new EnemyAttackState();
            Hitstun = new EnemyHitstunState();
            Knockdown = new EnemyKnockdownState();
            Dead = new EnemyDeadState();
            Air = new EnemyAirState();
        }

        // ============ States ============

        /// <summary>Waiting for the target to come within aggro range.</summary>
        public EnemyIdleState Idle { get; }

        /// <summary>Closing in / lining up / hanging back while others attack.</summary>
        public EnemyChaseState Chase { get; }

        /// <summary>Telegraphed swing.</summary>
        public EnemyAttackState Attack { get; }

        /// <summary>Flinch from a normal hit.</summary>
        public EnemyHitstunState Hitstun { get; }

        /// <summary>Launched, lying down, getting up.</summary>
        public EnemyKnockdownState Knockdown { get; }

        /// <summary>Defeated.</summary>
        public EnemyDeadState Dead { get; }

        /// <summary>Jumping or falling.</summary>
        public EnemyAirState Air { get; }
    }
}
