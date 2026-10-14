using UnityEngine.InputSystem;
using UnityEngine;
using Entities.Player.Behaviours;
using HarmonyLib;
using CalypsoExpansionSet.Audio;

namespace CalypsoExpansionSet.Idols;

internal sealed class JumpingIdol : HeldIdolEffect
{
    internal const ulong ConfigId = 0x4D49000000000001UL;
    internal static IdolDefinition Definition => new(
        "jumping", "跳跃雕像", ConfigId, 0x4D490001U,
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
        CreateVisuals = prefab => HeldIdolAudio.CreateAttached(prefab, "jumping",
            ConfigId, "spring_accent", 4, 0.1f),
        ContentVersion = 9
    };

    private const float RequestTimeout = 0.4f;
    private static JumpingIdol? _pending;
    private InputAction.CallbackContext? _jumpContext;
    private HeldIdolContext _context;
    private PlayerMovement? _movement;
    private float _requestStamp, _requestTime;
    private bool _faulted;

    public override void Start(HeldIdolContext context)
    {
        Dispose();
        _context = context;
        _faulted = false;
    }

    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        if (_faulted || !LocalHeldGuard.Ready(context, ConfigId)) { ClearPending(); return; }
        var movement = context.Holder.PlayerMovement;
        var controller = context.Holder.LocomotionController;
        if (_movement != null && (_movement != movement || !movement._jumpRequested
            || movement._jumpRequestTime != _requestStamp || Time.time - _requestTime > RequestTimeout))
            ClearPending();
        if (movement == null || controller == null || !movement.isOwned || !movement._isGrounded
            || movement._jumpRequested || controller.IsJumping || _movement != null)
            return;
        // The native callback ignores ctx, but its IL2CPP wrapper requires a boxed value.
        _jumpContext ??= new InputAction.CallbackContext();
        controller.OnJumpInputPerformed_OnceState(_jumpContext);
        if (!movement._jumpRequested || movement._jumpInitiated) return;
        _movement = movement;
        _requestStamp = movement._jumpRequestTime;
        _requestTime = Time.time;
        _pending = this;
    }

    internal static JumpingIdol? BeforeNativeJump(PlayerMovement movement)
    {
        var effect = _pending;
        if (effect == null) return null;
        try
        {
            if (movement != effect._movement) return null;
            if (effect._faulted || !LocalHeldGuard.Ready(effect._context, ConfigId)
                || effect._context.Holder.PlayerMovement != movement
                || !movement._jumpRequested || movement._jumpRequestTime != effect._requestStamp
                || Time.time - effect._requestTime > RequestTimeout)
            {
                effect.ClearPending();
                return null;
            }
            // Observe acceptance of this queued auto-jump without changing its
            // native force, stamina, eligibility or animation.
            return !movement._jumpInitiated && !movement._jumpConsumed && movement._isGrounded ? effect : null;
        }
        catch (Exception error) { effect.Fault(error); return null; }
    }

    internal static void AfterNativeJump(PlayerMovement movement, JumpingIdol? effect, Exception? error)
    {
        if (effect == null) return;
        try
        {
            if (error != null) { effect.Fault(error); return; }
            if (_pending != effect || movement == null || movement != effect._movement
                || !movement._jumpInitiated && !movement._jumpConsumed) return;
            effect.ClearPending(); // A subsequent native call cannot emit twice.
            if (!LocalHeldGuard.Ready(effect._context, ConfigId)) return;
            HeldIdolAudio.Emit(effect._context);
            if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
                CalypsoExpansionSetPlugin.Instance.Log.LogInfo(
                    $"[Jumping] accepted auto-jump with random spring sound: idol={effect._context.Idol.netId}, holder={effect._context.Holder.netId}.");
        }
        catch (Exception exception) { effect.Fault(exception); }
    }

    private void ClearPending()
    {
        if (_pending == this) _pending = null;
        _movement = null;
    }
    private void Fault(Exception error)
    {
        if (_faulted) return;
        _faulted = true;
        ClearPending();
        CalypsoExpansionSetPlugin.Instance?.Log.LogError($"Jumping effect disabled until released: {error}");
    }
    public override void Dispose()
    {
        ClearPending();
        _jumpContext = null;
        _context = default;
    }
}

[HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.HandleJumpRequest))]
internal static class JumpingAudioPatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out JumpingIdol? __state)
        => __state = JumpingIdol.BeforeNativeJump(__instance);
    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, JumpingIdol? __state, Exception? __exception)
    {
        JumpingIdol.AfterNativeJump(__instance, __state, __exception);
        return __exception;
    }
}
