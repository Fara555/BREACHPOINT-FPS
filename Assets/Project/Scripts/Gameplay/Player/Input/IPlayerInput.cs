using UnityEngine;

namespace Breachpoint.Gameplay.Player.Input
{
    public interface IPlayerInput
    {
        Vector2 Move { get; }
        Vector2 Look { get; }

        bool IsSprintHeld { get; }
        bool IsCrouchHeld { get; }
        bool IsJumpHeld { get; }

        bool WasJumpPressed { get; }
        bool WasJumpReleased { get; }
    }
}