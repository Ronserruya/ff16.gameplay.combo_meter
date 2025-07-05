using System.ComponentModel;
using ff16.gameplay.combo_meter.Template.Configuration;
using Reloaded.Mod.Interfaces.Structs;

namespace ff16.gameplay.combo_meter.Configuration;

public class Config : Configurable<Config>
{
    [DisplayName("Apply buffs at max rank?")]
    [Description("Whether to apply berserker mode and cooldown buff at max rank")]
    [DefaultValue(true)]
    public bool maxRankBuffs { get; set; } = true;

    [DisplayName("Hide battle score?")]
    [Description("Whether to hide the game's built-in battle score ui")]
    [DefaultValue(true)]
    public bool hideBattleScore { get; set; } = true;

    [DisplayName("Place Gauge at the top?")]
    [Description("Whether the gauge should be high or low on the screen")]
    [DefaultValue(true)]
    public bool gaugePosHigh { get; set; } = true;
}

/// <summary>
/// Allows you to override certain aspects of the configuration creation process (e.g. create multiple configurations).
/// Override elements in <see cref="ConfiguratorMixinBase"/> for finer control.
/// </summary>
public class ConfiguratorMixin : ConfiguratorMixinBase
{
    // 
}
