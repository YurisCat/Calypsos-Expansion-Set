using Entities.Monster;
using FMODUnity;
using UnityEngine;
using _200_Dev.Entities.Monsters.MoneyGuy;

namespace CalypsoExpansionSet.Idols;

// Read native resources only. Never run a monster controller or animation event.
internal sealed class MonsterAttackProfile
{
    internal const float MaximumDuration = 15f, MinimumInterval = 0.75f, Recovery = 0.15f;
    internal readonly MonsterEntity Template;
    internal readonly AnimationClip? Clip;
    internal readonly EventReference Sound;
    internal readonly float Duration;
    internal bool Available => Clip != null || !Sound.IsNull;
    internal float Interval => Math.Max(MinimumInterval, Duration + Recovery);

    internal MonsterAttackProfile(MonsterEntity template)
    {
        Template = template;
        var animator = template.GetComponentInChildren<Animator>(true);
        var clips = animator?.runtimeAnimatorController?.animationClips;
        var best = 0;
        if (clips != null && clips.Length <= 512)
            foreach (var clip in clips)
            {
                if (clip == null || !ValidDuration(clip.length)) continue;
                var score = ClipScore(clip.name, HasAttackEvent(clip));
                if (score > best || score > 0 && score == best
                    && string.CompareOrdinal(clip.name, Clip?.name) < 0)
                { Clip = clip; best = score; }
            }
        // MoneyGuy's vocal attack cue is separate from its swing/hit sounds.
        var moneyGuy = template.TryCast<MoneyGuyMonsterEntity>();
        var candidate = moneyGuy != null ? moneyGuy._attackGruntEventReference : template.attackSound;
        if (UsableSound(candidate)) Sound = candidate;
        else if (moneyGuy != null && UsableSound(template.attackSound)) Sound = template.attackSound;
        Duration = Clip != null ? Clip.length : 0;
    }

    internal static bool ValidDuration(float seconds) => float.IsFinite(seconds) && seconds is > 0 and <= MaximumDuration;

    private static bool HasAttackEvent(AnimationClip clip)
    {
        var markers = clip.events;
        if (markers == null) return false;
        foreach (var marker in markers)
            if (marker != null && marker.functionName is "TryExecuteAttack" or "TryExecuteAttackGrunt" or "TryExecuteRifleEffects")
                return true;
        return false;
    }

    internal static int ClipScore(string name, bool attackEvent)
    {
        var text = name.ToLowerInvariant();
        // An attack-related pose, recovery or death clip is not an attack.
        foreach (var excluded in new[] { "idle", "walk", "run", "locomotion", "aim", "pose", "loop", "reload",
            "death", "dead", "stun", "post", "recover", "pre_attack", "pre-attack", "preattack" })
            if (text.Contains(excluded, StringComparison.Ordinal)) return 0;
        if (attackEvent) return 100;
        return text.Contains("attack", StringComparison.Ordinal) ? 50 : 0;
    }

    private static bool UsableSound(EventReference candidate)
    {
        if (candidate.IsNull) return false;
        try
        {
            var description = RuntimeManager.GetEventDescription(candidate);
            if (!description.isValid() || description.isOneshot(out var oneShot) != FMOD.RESULT.OK || !oneShot
                || description.is3D(out var spatial) != FMOD.RESULT.OK || !spatial) return false;
            return true;
        }
        catch (Exception error)
        {
            CalypsoExpansionSetPlugin.Instance?.Log.LogWarning($"[Monster attack] optional sound unavailable: {error.Message}");
            return false;
        }
    }
}
