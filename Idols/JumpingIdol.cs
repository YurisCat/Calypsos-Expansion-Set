using UnityEngine.InputSystem;

namespace CalypsoExpansionSet.Idols;

internal sealed class JumpingIdol : HeldIdolEffect
{
    internal static IdolDefinition Definition => new(
        "jumping", "Jumping Idol", 0x4D49000000000001UL, 0x4D490001U,
        7331884207844971565UL, IdolEffectAuthority.OwnerNativeGameplay, () => new JumpingIdol())
    {
        // Score supplies the plain appearance; Slippery identifies ordinary idol pools.
        PoolTemplateId = 13601636613369722279UL,
        ContentVersion = 3
    };

    private InputAction.CallbackContext? _jumpContext;

    public override void Start(HeldIdolContext context) => context.Holder.CliSetCurrentStamina(0f);

    public override void Tick(HeldIdolContext context)
    {
        if (!context.GameplayReady)
            return;
        var movement = context.Holder.PlayerMovement;
        var controller = context.Holder.LocomotionController;
        if (movement == null || controller == null || !movement._isGrounded
            || movement._jumpRequested || controller.IsJumping)
            return;
        // The native callback ignores ctx, but its IL2CPP wrapper requires a boxed value.
        _jumpContext ??= new InputAction.CallbackContext();
        controller.OnJumpInputPerformed_OnceState(_jumpContext);
    }

    public override void Dispose() => _jumpContext = null;
}
