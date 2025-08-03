using ff16.gameplay.combo_meter.Configuration;
using ff16.gameplay.combo_meter.Template;
using FF16Framework.Interfaces.Nex;
using FF16Framework.Interfaces.Nex.Structures;
using FF16Tools.Files.Nex;
using FF16Tools.Files.Nex.Entities;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.Interfaces;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using System.Diagnostics;
using System.Text;
using static ff16.gameplay.combo_meter.Mod;
using IReloadedHooks = Reloaded.Hooks.ReloadedII.Interfaces.IReloadedHooks;

namespace ff16.gameplay.combo_meter;

/// <summary>
/// Your mod logic goes here.
/// </summary>
public class Mod : ModBase // <= Do not Remove.
{
    /// <summary>
    /// Provides access to the mod loader API.
    /// </summary>
    private readonly IModLoader _modLoader;

    /// <summary>
    /// Provides access to the Reloaded.Hooks API.
    /// </summary>
    /// <remarks>This is null if you remove dependency on Reloaded.SharedLib.Hooks in your mod.</remarks>
    private readonly IReloadedHooks? _hooks;

    /// <summary>
    /// Provides access to the Reloaded logger.
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Entry point into the mod, instance that created this class.
    /// </summary>
    private readonly IMod _owner;

    /// <summary>
    /// Provides access to this mod's configuration.
    /// </summary>
    private Config _configuration;

    /// <summary>
    /// The configuration of the currently executing mod.
    /// </summary>
    private readonly IModConfig _modConfig;

    public unsafe delegate long OnHitDelegate(long* bnpcRow, long R15, long a3, long a4);
    private IHook<OnHitDelegate> _onHit;

    public delegate long GetOrCreateEntityDelegate(long entityManager, out long outEntityInfo, long entityIdPtr);
    private GetOrCreateEntityDelegate _getOrCreateEntity;

    public delegate long GetBnpcIdFromEntityDelegate(long entity);
    private GetBnpcIdFromEntityDelegate _getBnpcIdFromEntity;

    public delegate char CheckHasSkillUnlockedDelegate(uint SkillId);
    private CheckHasSkillUnlockedDelegate _hasSkillUnlocked;

    public unsafe delegate char StartPlayerMode(long a1, uint playerMode, long a3);
    private IHook<StartPlayerMode> _startPlayerMode;

    public unsafe delegate char OnPerfectDodgeDelegate(long a1, long a2, double a3, double a4);
    private IHook<OnPerfectDodgeDelegate> _onPerfectDodge;

    public delegate void ApplyHitStopDelegate(long a1, uint hitType, float a3, float a4, char a5);
    private IHook<ApplyHitStopDelegate> _applyHitStop;

    public delegate char OnBattleTechniqueDelegate(long a1, uint techId, char a3);
    private IHook<OnBattleTechniqueDelegate> _onBattleTechnique;

    public delegate long OnLevelLoad(long a1, double a2, double a3, double a4);
    private IHook<OnLevelLoad> _onLevelLoad;

    private long globalUnk;
    private long globalEntityManagerPtr;

    private long modeA1;
    private LimitedList<int> usedAttacks = new(4);

    private static uint atkDmgColOffset = (uint)TableMappingReader.ReadTableLayout("attackparam", new Version(1, 0, 3)).Columns["AttackValue"].Offset;
    private WeakReference<INextExcelDBApiManaged> managedNexApi;
    private INexTable battleScoreTable;

    NexTableLayout attackParamLayout = TableMappingReader.ReadTableLayout("attackparam", new Version(1, 0, 3));
    NexTableLayout soundLayout = TableMappingReader.ReadTableLayout("uisound", new Version(1, 0, 3));
    NexTableLayout battleScoreLayout = TableMappingReader.ReadTableLayout("battlescorebonuslevel", new Version(1, 0, 3));
    NexTableLayout uiLayout = TableMappingReader.ReadTableLayout("ui", new Version(1, 0, 3));

    public WeakReference<INextExcelDBApi> _rawNexApi;

    private System.Timers.Timer gagueTimer = new System.Timers.Timer(100)
    {
        AutoReset = true,
        Enabled = true
    };

    private static CappedStopwatch drainStopwatch = new CappedStopwatch(TimeSpan.FromMilliseconds(3000));
    private static CappedStopwatch comboResetStopwatch = new CappedStopwatch(TimeSpan.FromMilliseconds(10000));
    private static CappedStopwatch buffResetStopwatch = new CappedStopwatch(TimeSpan.FromMilliseconds(4000));

    private readonly HashSet<uint> cliveIds = new HashSet<uint>()
    {
        1, 2, 3, 4, 6, 8, 9, 10
    };

    private ComboGague comboGague;

    public Mod(ModContext context)
    {
        _modLoader = context.ModLoader;
        _hooks = context.Hooks;
        _logger = context.Logger;
        _owner = context.Owner;
        _configuration = context.Configuration;
        _modConfig = context.ModConfig;

#if DEBUG
        Debugger.Launch();
#endif

        comboGague = new ComboGague(
            maxLevel: _configuration.extraRanks ? 6 : 4,
            unitsPerLevel: new()
                {
                        { 1, 900 },
                        { 2, 1200 },
                        { 3, 1800 },
                        { 4, 2100 },
                        { 5, 2100 },
                        { 6, 2100 }
                },
            gagueTypePerLevel: new()
                {
                        { 1, (char)4 },
                        { 2, (char)3 },
                        { 3, (char)1 },
                        { 4, (char)2 },
                        { 5, (char)2 },
                        { 6, (char)2 },
                },
            gagueElementUiId: (uint)(_configuration.gaugePosHigh ? 301 : 302),
            levelToUiStringId: new()
                {
                    { 1, 3800 },
                    { 2, 3801 },
                    { 3, 3802 },
                    { 4, 3803 },
                    { 5, 3804 },
                    { 6, 3805 },
                    { 7, 3806 }
                },
            hideBattleScore: _configuration.hideBattleScore,
            onPause: () => { drainStopwatch.Stop(); comboResetStopwatch.Stop(); buffResetStopwatch.Stop(); },
            onResume: () => { drainStopwatch.Start(); comboResetStopwatch.Start(); buffResetStopwatch.Start(); }
        );

        _logger.WriteLine($"[{_modConfig.ModId}] Initializing...", _logger.ColorGreen);

        managedNexApi = _modLoader.GetController<INextExcelDBApiManaged>();
        _rawNexApi = _modLoader.GetController<INextExcelDBApi>();
        if (!managedNexApi.TryGetTarget(out INextExcelDBApiManaged managedNextExcelDBApi))
        {
            throw new Exception($"[{_modConfig.ModId}] Could not get INextExcelDBApi. Is the FFXVI Mod Framework installed/loaded?");
        }

        managedNextExcelDBApi.OnNexLoaded += NextExcelDBApi_OnNexLoaded;

        var scansController = _modLoader.GetController<IStartupScanner>();
        if (scansController == null || !scansController.TryGetTarget(out IStartupScanner scans))
        {
            throw new Exception($"[{_modConfig.ModId}] Unable to get ISharedScans!");
        }
        SetupScans(scans);

        var baseAddress = Process.GetCurrentProcess().MainModule!.BaseAddress;
        globalUnk = baseAddress + 0x1816608;
        globalEntityManagerPtr = baseAddress + 0x1816CD0;

        gagueTimer.Elapsed += (sender, e) =>
        {
            PeriodicUpdate();
        };
    }

    private unsafe void NextExcelDBApi_OnNexLoaded()
    {
        SetupNex();
        _logger.WriteLine($"[{_modConfig.ModId}] Finished initialization.", _logger.ColorGreen);
    }

    private unsafe void SetupNex()
    {
        // Set up all the nex stuff
        managedNexApi.TryGetTarget(out INextExcelDBApiManaged managedNextExcelDBApi);
        
        INexTable attackParamTable = managedNextExcelDBApi!.GetTable(NexTableIds.attackparam)!;
        INexTable soundTable = managedNextExcelDBApi!.GetTable(NexTableIds.uisound)!;
        battleScoreTable = managedNextExcelDBApi!.GetTable(NexTableIds.battlescorebonuslevel)!;

        // Wait for the nex table to be loaded since the framework can fire the event too early.
        var counter = 0;
        while (attackParamTable.GetRow(1, 0) == null)
        {
            Thread.Sleep(100);
            counter++;
            if (counter > 20)
            {
                throw new Exception($"[{_modConfig.ModId}] Failed initialization due to nex loading timeout!");
            }
        }

        comboGague.setNexRows(
            ramuhSound1: soundTable.GetRow(164)!,
            ramuhSound2: soundTable.GetRow(165)!
        );

        _rawNexApi.TryGetTarget(out INextExcelDBApi rawNexApi);
        var rawUiTable = rawNexApi.GetTable(NexTableIds.ui);
        List<string> levelStrings = new List<string>()
        {
            _configuration.Rank1Name,
            _configuration.Rank2Name,
            _configuration.Rank3Name,
            _configuration.Rank4Name,
            _configuration.Rank5Name,
            _configuration.Rank6Name,
            _configuration.Rank7Name,
        };

        foreach (var kv in comboGague.LevelToUiStringId) {
            byte* row = rawNexApi.GetRowData(rawNexApi.SearchRow(rawUiTable, (int)kv.Value));
            SetStringField(row, uiLayout.Columns["Label"].Offset, levelStrings[kv.Key - 1]);
        }
    }

    private unsafe void SetStringField(byte* row, long fieldOffset, string value)
    {
        int stringOffset = *(int*)(row + fieldOffset);
        byte* stringPos = row + fieldOffset + stringOffset;
        Reloaded.Memory.Memory.Instance.SafeWrite((nuint)stringPos, Encoding.UTF8.GetBytes(value + "\0"));
    }

    private unsafe void SetupScans(IStartupScanner scans)
    {
        scans.AddScan("48 89 5C 24 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D AC 24 ?? ?? ?? ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 44 8B 82", address =>
        {
            _onHit = _hooks!.CreateHook<OnHitDelegate>(OnHitImpl, address).Activate();
        });

        scans.AddScan("85 D2 0F 84 ?? ?? ?? ?? 48 8B C4 48 89 58 ?? 48 89 68 ?? 48 89 70 ?? 48 89 78 ?? 41 55 41 56 41 57 48 83 EC ?? 48 8D 79", address =>
        {
            _startPlayerMode = _hooks!.CreateHook<StartPlayerMode>(StartPlayerModeImpl, address).Activate();
        });

        scans.AddScan("48 8B C4 48 89 58 ?? 48 89 68 ?? 48 89 70 ?? 57 41 54 41 55 41 56 41 57 48 83 EC ?? 45 33 ED 4C 8B FA", address =>
        {
            _onPerfectDodge = _hooks!.CreateHook<OnPerfectDodgeDelegate>(OnPerfectDodgeImpl, address).Activate();
        });

        scans.AddScan("40 55 53 56 57 41 54 41 56 41 57 48 8B EC 48 81 EC ?? ?? ?? ?? C5 F8 29 74 24", address =>
        {
            _applyHitStop = _hooks!.CreateHook<ApplyHitStopDelegate>(ApplyHitStopImpl, address).Activate();
        });

        scans.AddScan("48 89 5C 24 ?? 48 89 6C 24 ?? 44 89 44 24 ?? 56 57 41 54 41 56 41 57 48 83 EC ?? 45 33 E4", address =>
        {
            _getOrCreateEntity = _hooks!.CreateWrapper<GetOrCreateEntityDelegate>(address, out _);
        });

        scans.AddScan("48 83 EC ?? E8 ?? ?? ?? ?? 48 85 C0 74 ?? 8B 40", address =>
        {
            _getBnpcIdFromEntity = _hooks!.CreateWrapper<GetBnpcIdFromEntityDelegate>(address, out _);
        });

        scans.AddScan("48 89 5C 24 ?? 57 48 83 EC ?? 8B D9 E8", address =>
        {
            _hasSkillUnlocked = _hooks!.CreateWrapper<CheckHasSkillUnlockedDelegate>(address, out _);
        });

        scans.AddScan("48 89 5C 24 ?? 48 89 6C 24 ?? 48 89 74 24 ?? 57 41 56 41 57 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 45 8A F8", address =>
        {
            _onBattleTechnique = _hooks!.CreateHook<OnBattleTechniqueDelegate>(OnBattleTechniqueImpl, address).Activate();
        });

        scans.AddScan("48 89 5C 24 ?? 48 89 74 24 ?? 48 89 7C 24 ?? 55 41 54 41 55 41 56 41 57 48 8D 6C 24 ?? 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 8B 41", address =>
        {
            _onLevelLoad = _hooks!.CreateHook<OnLevelLoad>(OnLevelLoadImpl, address).Activate();
        });

        comboGague.SetupScans(scans, _hooks!);

    }

    private void PeriodicUpdate()
    {
        if (!comboGague.IsShown)
            return;
        if (drainStopwatch.IsRunning && drainStopwatch.isElapsed())
            DrainMeter();
        if (comboResetStopwatch.IsRunning && comboResetStopwatch.isElapsed())
            ResetCombo();
        if (buffResetStopwatch.IsRunning && buffResetStopwatch.isElapsed())
            DisableCooldownBuff();
    }

    private void ResetCombo()
    {
        comboGague.ComboCounter = 0;
        comboGague.CurrentUnits = 0;
        comboGague.UpdateGauge(forceUpdate: true);

        comboResetStopwatch.Reset();
        drainStopwatch.Reset();

        enemyAffected.Clear();
        usedAttacks.Clear();
    }

    private long OnLevelLoadImpl(long a1, double a2, double a3, double a4)
    {
        ResetCombo();
        return _onLevelLoad.OriginalFunction(a1, a2, a3, a4);
    }

    private char OnBattleTechniqueImpl(long a1, uint techId, char a3)
    {
        if (10000 <= techId && techId <= 20000) // Ifrit moves
            return _onBattleTechnique.OriginalFunction(a1, techId, a3);

        INexRow row = battleScoreTable.GetRow(techId)!;
        int score = row.GetInt32((uint)battleScoreLayout.Columns["Score"].Offset);
        int techDifficulty = row.GetInt32((uint)battleScoreLayout.Columns["BattleScoreAchievedDifficultyId"].Offset);
        int increase = score / techDifficulty;

        var wasMaxLevel = comboGague.IsMaxLevel();
        comboGague.CurrentUnits += (int)(increase * _configuration.styleMultiplier);
        comboGague.UpdateGauge(forceUpdate: !wasMaxLevel && comboGague.IsMaxLevel());

        return _onBattleTechnique.OriginalFunction(a1, techId, a3);
    }

    private void OnParryOrDodge()
    {
        var wasMaxLevel = comboGague.IsMaxLevel();
        comboGague.CurrentUnits += (int)(400 * _configuration.styleMultiplier);
        comboGague.UpdateGauge(forceUpdate: true);

        var drainDelay = comboGague.IsMaxLevel() ? TimeSpan.FromMilliseconds(6000) : TimeSpan.FromMilliseconds(comboGague.Level switch
        {
            1 => 3000,
            2 => 2200,
            3 => 1600,
            4 => 1000,
            5 => 1000,
            6 => 1000
        });

        drainStopwatch.Restart(drainDelay);
        comboResetStopwatch.Restart();

        if (comboGague.IsMaxLevel())
            EnterBerserkMode();
    }

    private void ApplyHitStopImpl(long a1, uint hitType, float a3, float a4, char a5)
    {
        _applyHitStop.OriginalFunction(a1, hitType, a3, a4, a5);
        if (hitType == 5)
            OnParryOrDodge();
    }

    private unsafe char OnPerfectDodgeImpl(long a1, long a2, double a3, double a4)
    {
        OnParryOrDodge();
        return _onPerfectDodge.OriginalFunction(a1, a2, a3, a4);
    }

    private unsafe char StartPlayerModeImpl(long a1, uint playerMode, long a3)
    {
        modeA1 = a1;
        _startPlayerMode.Disable();
        return _startPlayerMode.OriginalFunction(a1, playerMode, a3);
    }

    private unsafe void DrainMeter()
    {
        bool wasMaxLevel = comboGague.IsMaxLevel();
        comboGague.CurrentUnits -= 25;
        if (comboGague.CurrentUnits <= 0)
            ResetCombo();
        else
        { 
            comboGague.UpdateGauge(isIncrease: false, forceUpdate: wasMaxLevel);
        }
    }

    private unsafe void EnterBerserkMode()
    {
        // Cooldown

        if (_configuration.maxRankCooldown)
        {
            long globalAddress = *(long*)globalUnk;
            long escapmentBuffFlagAddress = globalAddress + 0x1B574;
            long escapmentBuffRateAddress = escapmentBuffFlagAddress + 4;

            *(long*)escapmentBuffFlagAddress = 1;
            *(int*)escapmentBuffRateAddress = 200;
            buffResetStopwatch.Restart();
        }

        //Berserker

        // If limit break unlocked
        if (_configuration.maxRankBerserker && (_hasSkillUnlocked(28) == (char)1))
            _startPlayerMode.OriginalFunction(modeA1, 41, 0);
    }

    private unsafe void DisableCooldownBuff()
    {
        if (!_configuration.maxRankCooldown)
            return;

        long globalAddress = *(long*)globalUnk;
        long escapmentBuffFlagAddress = globalAddress + 0x1B574;
        long escapmentBuffRateAddress = escapmentBuffFlagAddress + 4;
        *(long*)escapmentBuffFlagAddress = 0;
        *(int*)escapmentBuffRateAddress = 100;
        buffResetStopwatch.Reset();
    }

    struct AtkInfo
    {
        public int Dmg;
        public int ZanGain;
        public int actionId;
        public long targetId;
        public bool isCliveAttack;
        public bool isCliveTarget;
        public bool isHealOrEffect;
        public bool isRangedMagic;
        public bool isLimitBreakAttack;

        public override string ToString()
        {
            return $"Dmg: {Dmg},\n" +
                   $"ZanGain: {ZanGain},\n" +
                   $"ActionId: {actionId},\n" +
                   $"isCliveAttack: {isCliveAttack},\n" +
                   $"isCliveTarget: {isCliveTarget},\n" +
                   $"isHealOrEffect: {isHealOrEffect},\n" +
                   $"isRangedMagic: {isRangedMagic},\n" +
                   $"isLimitBreakAttack: {isLimitBreakAttack}";
        }
    }

    private unsafe uint GetAtkSource(long R15)
    { 
        long v6 = *(long*)(R15 + 136);
        _getOrCreateEntity(*(long*)globalEntityManagerPtr, out long entityUnk, v6);
        var res = (uint)_getBnpcIdFromEntity(entityUnk);
        return res;
    }

    private unsafe AtkInfo ParseAttackInfo(long* bnpcRow, long R15)
    {
        _rawNexApi.TryGetTarget(out var rawDBApi);

        var a = *(long*)((byte*)bnpcRow + 0x20);
        var b = *(long*)((byte*)a + 0x7298);
        uint attackTarget = *(uint*)((byte*)b + 0x38);

        int actionId = *(int*)(R15 + 0xB0);
        int rawDmg = *(int*)(R15 + 0x174);

        uint atkSource = GetAtkSource(R15);

        NexRowInstance** atkParamPtr = (NexRowInstance**)(R15 + 216);
        byte* atkParamRowPtr = rawDBApi!.GetRowData(*atkParamPtr);

        int Dmg = *(int*)(atkParamRowPtr + attackParamLayout.Columns["AttackValue"].Offset);

        short LimitGain = *(short*)(atkParamRowPtr + attackParamLayout.Columns["LimitGain"].Offset);
        short ZanGain = *(short*)(atkParamRowPtr + attackParamLayout.Columns["ZantetsukenGain"].Offset);
        
        int AtkType = *(int*)(atkParamRowPtr + attackParamLayout.Columns["AtkType"].Offset);
        bool isLimitBreakAttack = (11 <= AtkType && AtkType <= 14);
        bool isRangedMagic = *(int*)(atkParamRowPtr + attackParamLayout.Columns["Unk9"].Offset) == 19;

        return new AtkInfo
        {
            Dmg = Dmg,
            ZanGain = ZanGain,
            actionId = actionId,
            targetId = (long)bnpcRow, // not actually any internal id, but this pointer is unique per enemy ¯\_(ツ)_/¯
            isCliveAttack = cliveIds.Contains(atkSource) || (atkSource == 100 && attackTarget != 1), // Clive and torgal attacks, igornig torgal's heals
            isCliveTarget = cliveIds.Contains(attackTarget),
            isHealOrEffect = attackTarget == 1 && rawDmg <= 0,
            isRangedMagic = isRangedMagic,
            isLimitBreakAttack = isLimitBreakAttack
        };

    }

    private HashSet<long> enemyAffected = new();

    private unsafe long OnHitImpl(long* bnpcRow, long R15, long a3, long a4)
    {
        AtkInfo info = ParseAttackInfo(bnpcRow, R15);

        if (info.isCliveTarget)
        {
            if (!info.isHealOrEffect)
            {
                comboGague.ComboCounter = 0;
                comboGague.CurrentUnits = (int)(comboGague.CurrentUnits / comboGague.Level >= 4 ? 2.5 : 2);
                comboGague.UpdateGauge(isIncrease: false);
                enemyAffected.Clear();
                usedAttacks.Clear();
            }

            return _onHit.OriginalFunction(bnpcRow, R15, a3, a4);
        }

        if (info.isCliveAttack)
        {
            var gainValue = Math.Max(info.Dmg, info.ZanGain);
            bool isNewAttack = info.actionId != usedAttacks.Last();

            if (isNewAttack)
                enemyAffected.Clear();

            enemyAffected.Add(info.targetId);

            if (!isNewAttack)
            {
                if (!info.isLimitBreakAttack)
                {
                    if (enemyAffected.Count == 1)
                        gainValue /= 3;
                    else if (enemyAffected.Count == 2)
                        gainValue /= 5;
                    else
                        gainValue /= 7;
                }
            }
            else if (usedAttacks.Contains(info.actionId))
                gainValue = (int)(gainValue / 1.5f);

            usedAttacks.Add(info.actionId);

            comboGague.CurrentUnits += (int)(Math.Min(gainValue, 350) * _configuration.styleMultiplier);

            var drainDelay = comboGague.IsMaxLevel() ? TimeSpan.FromMilliseconds(6000) : TimeSpan.FromMilliseconds(comboGague.Level switch
            {
                1 => 3000,
                2 => 2200,
                3 => 1600,
                4 => 1000,
                5 => 1000,
                6 => 1000
            });

            if (info.isRangedMagic)
                drainDelay /= 1.5; // For ranged magic attacks, pause drain for less time.

            drainStopwatch.Restart(drainDelay);
            comboResetStopwatch.Restart();

            comboGague.ComboCounter++;
            comboGague.UpdateGauge();

            if (comboGague.IsMaxLevel())
                EnterBerserkMode();

            if (_configuration.mustStyle && !comboGague.IsMaxLevel())
                *(int*)(R15 + 372) = 1;
        }

        return _onHit.OriginalFunction(bnpcRow, R15, a3, a4);


        //managedNexApi.TryGetTarget(out INextExcelDBApiManaged managedNextExcelDBApi);
        //INexTable equipItemTable = managedNextExcelDBApi!.GetTable(NexTableIds.equipitem)!;
        //float reductionPotency = 0f;
        //// LB basic / basic / air basic attacks
        //if (10 <= AtkType && AtkType <= 14) {

        //    //for (uint i = 0; i < 6; i++)
        //    //{
        //    //    _getEquipmentId(0, out uint* eqpId, i, 0);
        //    //    var itemRow = equipItemTable.GetRow(*eqpId);
        //    //    int effectFlag = itemRow!.GetInt32((uint)equipLayout.Columns["EffectFlag"].Offset);
        //    //    if (effectFlag == 50) {
        //    //        reductionPotency = itemRow.GetInt32((uint)equipLayout.Columns["EffectPotency"].Offset) / 1000f;
        //    //        break;
        //    //    }
        //    //}
        //    //counter++;
        //    //reductionPotency = 1.5f;
        //    //var addr = *(long*)(*(long*)(*(long*)(*(long*)uiGlobalThing + 16) + 144) + 136);
        //    //_logger.WriteLine($"[{_modConfig.ModId}] value is {*(long*)(addr + 48):X}");
        //    //_logger.WriteLine($"[{_modConfig.ModId}] Showing gauge at address: {addr:X}");
        //    //if (!gagueShown)
        //    //    _showGauge.OriginalFunction(addr, (char)2);
        //    //gagueShown = true;
        //    ////_something.OriginalFunction(addr);
        //    //currentUnits += Dmg;
        //    //var level = (int)(currentUnits / 1000);
        //    //var units = currentUnits % 1000;
        //    //_tick.OriginalFunction(addr, units, 1000, counter, 250, (char)1);

        //    if (reductionPotency > 0f) {
        //        long globalAddress = *(long*)globalUnk;

        //        long lastNodeAddress = globalAddress + 0x1B540;
        //        long currentNodeAddress = *(long*)lastNodeAddress;

        //        while (currentNodeAddress != lastNodeAddress)
        //        {
        //            var skillData = currentNodeAddress + 0x10;
        //            var currentCooldown = *(float*)(skillData + 0x4);
        //            var totalCooldown = *(float*)(skillData + 0x8);
        //            if (currentCooldown < totalCooldown)
        //                *(float*)(skillData + 0x4) += (1.5f * Dmg / 100);

        //            currentNodeAddress = *(long*)currentNodeAddress;
        //        }
        //    }
        //}
    }


    #region Standard Overrides
    public override void ConfigurationUpdated(Config configuration)
    {
        // Apply settings from configuration.
        // ... your code here.
        _logger.WriteLine($"[{_modConfig.ModId}] Config Updated not supported", _logger.ColorRed);
    }
    #endregion

    #region For Exports, Serialization etc.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    public Mod() { }
#pragma warning restore CS8618
    #endregion
}