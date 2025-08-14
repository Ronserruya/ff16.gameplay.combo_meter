using System.ComponentModel;
using ff16.gameplay.combo_meter.Template.Configuration;
using Reloaded.Mod.Interfaces.Structs;

namespace ff16.gameplay.combo_meter.Configuration;

public class Config : Configurable<Config>
{
    [DisplayName("Apply Berserker mode at max rank?")]
    [Description("Whether to apply berserker mode buff at max rank")]
    [DefaultValue(true)]
    public bool maxRankBerserker { get; set; } = true;

    [DisplayName("Apply cooldown buff at max rank?")]
    [Description("Whether to apply cooldown mode buff at max rank")]
    [DefaultValue(true)]
    public bool maxRankCooldown { get; set; } = true;

    [DisplayName("Style gain factor")]
    [Description("Make it easier (bigger number) or harder (smaller number) to gain style")]
    [DefaultValue(1f)]
    public float styleMultiplier { get; set; } = 1f;

    [DisplayName("Extra ranks mode")]
    [Description("Add two extra style ranks (SS & SSS)")]
    [DefaultValue(false)]
    public bool extraRanks { get; set; } = false;

    [DisplayName("Must style mode")]
    [Description("Enemies only take damage at max rank")]
    [DefaultValue(false)]
    public bool mustStyle { get; set; } = false;

    [DisplayName("Must style min rank")]
    [Description("Minimum rank to deal damage in must style mode, *use -1 for max rank*")]
    [DefaultValue(-1)]
    public int mustStyleMinRank { get; set; } = -1;

    [DisplayName("Hide battle score?")]
    [Description("Whether to hide the game's built-in battle score ui")]
    [DefaultValue(true)]
    public bool hideBattleScore { get; set; } = true;

    [DisplayName("Place Gauge at the top?")]
    [Description("Whether the gauge should be high or low on the screen")]
    [DefaultValue(true)]
    public bool gaugePosHigh { get; set; } = true;

    [DisplayName("Rank 1 Title")]
    [Description("Rank 1 Title")]
    [DefaultValue("Dominated")]
    public string Rank1Name { get; set; } = "Dominated";

    [DisplayName("Rank 2 Title")]
    [Description("Rank 2 Title")]
    [DefaultValue("Cursed")]
    public string Rank2Name { get; set; } = "Cursed";

    [DisplayName("Rank 3 Title")]
    [Description("Rank 3 Title")]
    [DefaultValue("Bearer")]
    public string Rank3Name { get; set; } = "Bearer";

    [DisplayName("Rank 4 Title")]
    [Description("Rank 4 Title")]
    [DefaultValue("Ascended")]
    public string Rank4Name { get; set; } = "Ascended";

    [DisplayName("Rank 5 Title")]
    [Description("Rank 5 Title")]
    [DefaultValue("Sinner!")]
    public string Rank5Name { get; set; } = "Sinner!";

    [DisplayName("Rank 6 Title")]
    [Description("Rank 6 Title")]
    [DefaultValue("SSinner!!")]
    public string Rank6Name { get; set; } = "SSinner!!";

    [DisplayName("Rank 7 Title")]
    [Description("Rank 7 Title")]
    [DefaultValue("SSSinner!!!")]
    public string Rank7Name { get; set; } = "SSSinner!!!";
}

/// <summary>
/// Allows you to override certain aspects of the configuration creation process (e.g. create multiple configurations).
/// Override elements in <see cref="ConfiguratorMixinBase"/> for finer control.
/// </summary>
public class ConfiguratorMixin : ConfiguratorMixinBase
{
    // 
}
