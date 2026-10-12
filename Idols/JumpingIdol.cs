using UnityEngine.InputSystem;

namespace CalypsoExpansionSet.Idols;

internal sealed class JumpingIdol : HeldIdolEffect
{
    internal static IdolDefinition Definition => new(
        "jumping", "Jumping Idol", 0x4D49000000000001UL, 0x4D490001U,
        // 第五个参数是 TemplateId：复制 Score 的配置和预制体，再修改自己的副本。
        7331884207844971565UL, IdolEffectAuthority.OwnerNativeGameplay, () => new JumpingIdol())
    {
        // Slippery 只用于自然池匹配，基础属性和外观仍来自上面的 Score 模板。
        PoolTemplateId = 13601636613369722279UL,

        // 目前全部继承模板。需要调整时取消相应示例的注释，只保留要覆盖的字段。
        // InitialWeight = 100f, // 初始相对权重；未指定时沿用原版 100，选中后仍衰减。
        // ConfigureData = data =>
        // {
        //     data.scoreBasePoint = 1; // 基础分值，不是固定售价；实际价值为实例 CurrentValue。
        //     data.difficultyRange = new Items.SOItem.FloatRange { min = 1, max = 5 };
        //     data.twoHandedSlowPower = 1f; // 双手持有减速参数，不是刚体质量或速度百分比。
        //     data.unequippedScale = 1f; // 放下后的缩放。
        // },
        // ConfigurePrefab = prefab =>
        // {
        //     var body = prefab.GetComponent<UnityEngine.Rigidbody>();
        //     if (body != null) body.mass = 5f; // 模板刚体质量；修改自己的预制体副本。
        // },

        // 实际改变属性、玩法或外观后提升此版本，重新编译并让所有联机端更新。
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
