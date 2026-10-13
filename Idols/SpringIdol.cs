using UnityEngine.InputSystem;
using UnityEngine;
using CalypsoExpansionSet.Visuals;
using Entities.Player.Behaviours;
using HarmonyLib;

namespace CalypsoExpansionSet.Idols;

internal sealed class SpringIdol : HeldIdolEffect
{
    internal static IdolDefinition Definition => new(
        "spring", "弹簧雕像", 0x4D49000000000002UL, 0x4D490002U,
        // 第五个参数是 TemplateId：复制 Score 的配置和预制体，再修改自己的副本。
        7331884207844971565UL, IdolEffectAuthority.OwnerNativeGameplay, () => new SpringIdol())
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
        // Both built-in types temporarily reuse the existing artwork, with separate visual resources.
        CreateVisuals = prefab => StickyNoteVisuals.Create(prefab, "jumping"),
        ContentVersion = 1
    };

    // An authored rhythm, not independent client-side random gameplay results.
    // Several light hops lead into a normal bounce, then a slightly higher one.
    private static readonly float[] Rhythm = { 0.58f, 0.58f, 0.58f, 0.58f, 1f, 0.58f, 0.58f, 0.58f, 0.58f, 0.58f, 1.12f };
    private static readonly Dictionary<IntPtr, SpringIdol> Pending = new();
    internal const float SmallTakeoffSpeedLimit = 3.6f;
    internal const float GroundPauseSeconds = 1f;
    internal const float RetrySeconds = 0.25f;
    private const float RequestTimeoutSeconds = 0.4f;
    private InputAction.CallbackContext? _jumpContext;
    private HeldIdolContext _context;
    private PlayerMovement? _pendingMovement;
    private float _requestTime;
    private float _requestStamp;
    private float _remaining;
    private int _beat;
    private bool _faulted;
    private bool _observingFlight;
    private bool _leftGround;
    private float _launchY;
    private float _peakY;
    private float _launchTime;
    private string _flightKind = "";

    public override void Start(HeldIdolContext context)
    {
        Dispose();
        _context = context;
        _remaining = 0.15f;
    }

    public override void Tick(HeldIdolContext context)
    {
        _context = context;
        var movement = context.Holder.PlayerMovement;
        var controller = context.Holder.LocomotionController;
        if (movement == null || controller == null) { ClearPending(); return; }
        ObserveFlight(movement, controller.IsJumping);
        if (_pendingMovement is not null && (!movement._jumpRequested
            || movement != _pendingMovement || Time.time - _requestTime > RequestTimeoutSeconds))
            ClearPending();
        if (_faulted || !context.GameplayReady || !movement.isOwned || _pendingMovement != null
            || !movement._isGrounded || movement._jumpRequested || controller.IsJumping)
            return;
        _remaining -= Mathf.Clamp(Time.deltaTime, 0f, 0.1f);
        if (_remaining > 0f)
            return;
        _remaining = RetrySeconds;
        // The native callback ignores ctx, but its IL2CPP wrapper requires a boxed value.
        _jumpContext ??= new InputAction.CallbackContext();
        controller.OnJumpInputPerformed_OnceState(_jumpContext);
        if (!movement._jumpRequested || movement._jumpInitiated) return;
        // The callback queues the jump; its impulse is applied in native FixedUpdate.
        // Do not keep an altered movement setting between these two calls.
        _pendingMovement = movement;
        _requestTime = Time.time;
        _requestStamp = movement._jumpRequestTime;
        Pending[movement.Pointer] = this;
    }

    internal readonly record struct ImpulseScope(SpringIdol? Effect, float OriginalForce, float AppliedForce);

    internal static ImpulseScope BeforeNativeJump(PlayerMovement movement)
    {
        if (!Pending.TryGetValue(movement.Pointer, out var effect)) return default;
        var holder = effect._context.Holder;
        var idol = effect._context.Idol;
        if (movement != effect._pendingMovement || !movement.isOwned || idol == null || holder == null
            || holder.CurrentHealthSync <= 0 || holder.PlayerMovement != movement
            || idol.ItemOwner != holder || holder.InventoryNew == null || holder.InventoryNew.InHand != idol
            || !movement._jumpRequested || movement._jumpRequestTime != effect._requestStamp
            || Time.time - effect._requestTime > RequestTimeoutSeconds)
        {
            effect.ClearPending();
            return default;
        }
        if (movement._jumpInitiated || movement._jumpConsumed || !movement._isGrounded) return default;
        var original = movement.jumpForce;
        var multiplier = movement.JumpForceMultiplier;
        if (!float.IsFinite(original) || original <= 0f || !float.IsFinite(multiplier) || multiplier <= 0f)
        {
            effect.ClearPending();
            return default;
        }
        var gain = Rhythm[effect._beat];
        var applied = original * gain;
        if (gain < 1f) applied = Mathf.Min(applied, SmallTakeoffSpeedLimit / multiplier);
        movement.jumpForce = applied;
        return new ImpulseScope(effect, original, applied);
    }

    internal static void AfterNativeJump(PlayerMovement movement, ImpulseScope scope, Exception? exception)
    {
        if (scope.Effect == null) return;
        // Restore even if the native call fails. A concurrent mod's different value
        // is its property: don't overwrite it with our earlier snapshot.
        if (movement != null && movement.jumpForce == scope.AppliedForce)
            movement.jumpForce = scope.OriginalForce;
        var effect = scope.Effect;
        if (exception != null)
        {
            effect.ClearPending();
            effect._faulted = true;
            return;
        }
        if (movement == null || (!movement._jumpInitiated && !movement._jumpConsumed)) return;
        var gain = Rhythm[effect._beat];
        effect._flightKind = gain < 1f ? "small" : gain == 1f ? "normal" : "accent";
        effect._beat = (effect._beat + 1) % Rhythm.Length;
        effect._remaining = GroundPauseSeconds;
        effect.ClearPending();
        if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled == true)
        {
            effect._launchY = effect._peakY = movement.transform.position.y;
            effect._launchTime = Time.time;
            effect._leftGround = false;
            effect._observingFlight = true;
            CalypsoExpansionSetPlugin.Instance.Log.LogInfo($"[Spring] spring impulse: idol={effect._context.Idol.netId}, "
                + $"holder={effect._context.Holder.netId}, kind={effect._flightKind}, force={scope.AppliedForce:F3}, "
                + $"nativeForce={scope.OriginalForce:F3}, nativeMultiplier={movement.JumpForceMultiplier:F3}, time={Time.time:F3}s.");
        }
    }

    private void ObserveFlight(PlayerMovement movement, bool jumping)
    {
        if (CalypsoExpansionSetPlugin.Instance?.DebugTools.Enabled != true)
        {
            _observingFlight = false;
            return;
        }
        if (!_observingFlight) return;
        _peakY = Mathf.Max(_peakY, movement.transform.position.y);
        _leftGround |= !movement._isGrounded;
        if (!_leftGround || !movement._isGrounded || jumping) return;
        _observingFlight = false;
        CalypsoExpansionSetPlugin.Instance?.Log.LogInfo($"[Spring] spring landed: idol={_context.Idol.netId}, "
            + $"kind={_flightKind}, observedRise={_peakY - _launchY:F3}m, elapsed={Time.time - _launchTime:F3}s, time={Time.time:F3}s.");
    }

    private void ClearPending()
    {
        if (_pendingMovement is not null && Pending.TryGetValue(_pendingMovement.Pointer, out var effect) && effect == this)
            Pending.Remove(_pendingMovement.Pointer);
        _pendingMovement = null;
    }

    public override void Dispose()
    {
        ClearPending();
        _jumpContext = null;
        _remaining = 0.15f;
        _beat = 0;
        _faulted = _observingFlight = _leftGround = false;
    }
}

// Only an accepted auto-request belonging to this effect receives a temporary
// impulse setting. Unrelated/manual jumps and remote players pass unchanged.
[HarmonyPatch(typeof(PlayerMovement), nameof(PlayerMovement.HandleJumpRequest))]
internal static class SpringJumpImpulsePatch
{
    [HarmonyPrefix]
    private static void Prefix(PlayerMovement __instance, out SpringIdol.ImpulseScope __state)
        => __state = SpringIdol.BeforeNativeJump(__instance);

    [HarmonyFinalizer]
    private static Exception? Finalizer(PlayerMovement __instance, SpringIdol.ImpulseScope __state, Exception? __exception)
    {
        SpringIdol.AfterNativeJump(__instance, __state, __exception);
        return __exception;
    }
}
