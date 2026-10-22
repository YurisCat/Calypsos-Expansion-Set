using BepInEx.Configuration;
using Mirror;

namespace CalypsoExpansionSet.UI;

internal readonly record struct ExpansionSettingsValues(bool Names, bool Advice, bool NaturalSpawning, bool PermanentClock,
    string PingMouseKeyboard, string PingGamepad);

internal sealed class ExpansionSettings
{
    private readonly ConfigFile _config;
    private readonly ConfigEntry<bool> _names, _advice, _natural, _clock;
    private readonly ConfigEntry<string> _pingMouse, _pingGamepad;

    internal ExpansionSettings(ConfigFile config)
    {
        _config = config;
        _names = (ConfigEntry<bool>)config["UI", "ShowIndividualIdolNames"];
        _advice = (ConfigEntry<bool>)config["UI", "ShowIdolAdvice"];
        _natural = (ConfigEntry<bool>)config["Gameplay", "NaturalSpawning"];
        _clock = (ConfigEntry<bool>)config["Gameplay", "PermanentOldClock"];
        _pingMouse = (ConfigEntry<string>)config["QualityOfLife", "PingMouseKeyboard"];
        _pingGamepad = (ConfigEntry<string>)config["QualityOfLife", "PingGamepad"];
    }

    internal ExpansionSettingsValues Current => new(_names.Value, _advice.Value, _natural.Value, _clock.Value,
        _pingMouse.Value, _pingGamepad.Value);

    internal void Apply(ExpansionSettingsValues values)
    {
        if (NetworkServer.active || NetworkClient.active)
            throw new InvalidOperationException("Leave the room before changing expansion settings.");
        var previous = Current;
        var autoSave = _config.SaveOnConfigSet;
        _config.SaveOnConfigSet = false;
        try
        {
            Set(values);
            _config.Save();
        }
        catch
        {
            Set(previous);
            throw;
        }
        finally { _config.SaveOnConfigSet = autoSave; }
    }

    private void Set(ExpansionSettingsValues values)
    {
        _names.Value = values.Names;
        _advice.Value = values.Advice;
        _natural.Value = values.NaturalSpawning;
        _clock.Value = values.PermanentClock;
        _pingMouse.Value = values.PingMouseKeyboard;
        _pingGamepad.Value = values.PingGamepad;
    }
}
