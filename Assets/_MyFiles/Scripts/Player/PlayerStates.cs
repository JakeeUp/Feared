namespace Brawler.Player
{
    /// <summary>
    /// Holds exactly one instance of each player state (allocated once, reused forever — no per-transition allocation).
    /// States reach each other through <c>context.States</c>, e.g. <c>ctx.Machine.ChangeState(ctx.States.Dash)</c>.
    /// </summary>
    public sealed class PlayerStates
    {
        // ============ Construction ============

        /// <summary>Creates all state instances.</summary>
        public PlayerStates()
        {
            Idle = new IdleState();
            Move = new MoveState();
            Attack = new AttackState();
            Dash = new DashState();
            Hitstun = new HitstunState();
        }

        // ============ States ============

        /// <summary>Standing still; accepts Dash/Attack/Move.</summary>
        public IdleState Idle { get; }

        /// <summary>Running on X/Z; accepts Dash/Attack/Idle.</summary>
        public MoveState Move { get; }

        /// <summary>Playing the current combo link (re-entered for each link).</summary>
        public AttackState Attack { get; }

        /// <summary>Dashing with i-frames.</summary>
        public DashState Dash { get; }

        /// <summary>Hit reaction (re-entered on re-hit to refresh).</summary>
        public HitstunState Hitstun { get; }
    }
}
