using UnityEngine;
using Object = UnityEngine.Object;
namespace Prospice.Placement;

internal static class GameIntegration
{
    internal static bool Commit(PlayerInteraction interaction, Rigidbody body, PlacementTarget target)
    {
        if (!target.Valid || target.Support == null || !target.Support || interaction.heldRigidbody != body) return false;
        var inheritedVelocity = target.Support.attachedRigidbody ? target.Support.attachedRigidbody.GetPointVelocity(target.Position) : Vector3.zero;
        // bvu adds cameraForward * argument + characterController.velocity as an impulse.
        // Supply a stationary controller only for this synchronous call so walking causes no placement impulse.
        // The real controller is restored in finally; no field offsets or guessed native pointers are used.
        var stationary = new GameObject("Prospice.Placement.ReleaseVelocity");
        stationary.SetActive(false);
        var zeroVelocity = stationary.AddComponent<CharacterController>();
        var original = interaction.characterController;
        try
        {
            interaction.characterController = zeroVelocity;
            interaction.bvu(0f);
        }
        finally { interaction.characterController = original; Object.Destroy(stationary); }
        if (interaction.heldRigidbody) return false;
        body.position = target.Position;
        body.rotation = target.Rotation;
        body.transform.SetPositionAndRotation(target.Position, target.Rotation);
        body.velocity = inheritedVelocity;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
        interaction.nly = .5f; // Same debounce applied by bvr after a normal drop.
        return true;
    }
}
