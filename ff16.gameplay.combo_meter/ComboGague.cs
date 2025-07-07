using FF16Tools.Files.Nex.Entities;
using FF16Tools.Files.Nex;
using Reloaded.Hooks.Definitions;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using FF16Framework.Interfaces.Nex;
using Reloaded.Mod.Interfaces;

namespace ff16.gameplay.combo_meter;

class ComboGague
{
    private readonly NexTableLayout soundLayout = TableMappingReader.ReadTableLayout("uisound", new Version(1, 0, 3));

    public delegate void ShowGaugeDelegate(long a1, char eikon);
    private IHook<ShowGaugeDelegate> _showGauge;

    public delegate void BahamutTickGaugeDelegate(long a1, long currentUnits, long unitsPerLevel, int currentLevel, int maxLevel, char isActive);
    private BahamutTickGaugeDelegate _bahamutTick;

    public delegate void RamuhTickGaugeDelegate(long a1, long currentUnits, long maxUnits, double something);
    private RamuhTickGaugeDelegate _ramuhTick;

    public delegate void SetUiElementHiddenDelegate(long a1, char a2);
    private IHook<SetUiElementHiddenDelegate> _setUiElementHidden;

    public delegate void SetUiElementHiddenOffDelegate(long a1, char a2);
    private IHook<SetUiElementHiddenOffDelegate> _setUiElementHiddenOff;

    public delegate char SetUiAddonDelegate(long a1, uint a2);
    private SetUiAddonDelegate _setUiAddon;

    public delegate long CreateGagueElementDelegate(long a1);
    private IHook<CreateGagueElementDelegate> _createGagueElement;

    public delegate long UpdateGagueBarDelegate(long a1, int a2, byte a3);
    private IHook<UpdateGagueBarDelegate> _updateGagueBar;

    public delegate char InitUiElementDelegate(long a1, long a2, long a3);
    private IHook<InitUiElementDelegate> _initUiElement;


    public long GaguePointer { get; set; }
    public long OriginalGaguePointer { get; set; }

    public uint GagueElementUiId { get; set; }
    public bool hideBattleScore { get; set; } = true;
    public int CurrentUnits { get; set; } = 0;
    public int Level { get; set; } = 1;
    public int MaxLevel { get; set; }
    public int MaxUnits { get; set; }
    public Dictionary<int, int> UnitsPerLevel { get; set; }
    public Dictionary<int, char> GagueTypePerLevel { get; set; }
    public int ComboCounter { get; set; } = 0;
    public bool IsShown { get; set; } = false;

    public bool uiSetupFinished = false;
     
    public  INexRow ramuhSound1;
    public INexRow ramuhSound2;

    public Action OnPause;
    public Action OnResume;

    public Dictionary<int, uint> LevelToUiStringId;

    private long sizeXAddress;
    private long sizeYAddress;

    private long battleScorePtrAddress;
    private long battleScoreTechBonusPtrAddress;

    public ComboGague(int maxLevel, Dictionary<int, int> unitsPerLevel, Dictionary<int, char> gagueTypePerLevel, uint gagueElementUiId, Action onPause, Action onResume, Dictionary<int, uint> levelToUiStringId, bool hideBattleScore)
    {
        MaxLevel = maxLevel;
        MaxUnits = unitsPerLevel.Where(kv => kv.Key <= maxLevel).Select(kv => kv.Value).Sum();
        GagueElementUiId = gagueElementUiId;
        UnitsPerLevel = unitsPerLevel;
        GagueTypePerLevel = gagueTypePerLevel;
        OnPause = onPause;
        OnResume = onResume;
        LevelToUiStringId = levelToUiStringId;
        this.hideBattleScore = hideBattleScore;
    }

    public void setNexRows(INexRow ramuhSound1, INexRow ramuhSound2)
    {
        this.ramuhSound1 = ramuhSound1;
        this.ramuhSound2 = ramuhSound2;
    }

    public bool IsMaxLevel()
    {
        return CurrentUnits >= MaxUnits;
    }

    public void SetupScans(IStartupScanner scans, IReloadedHooks hooks) 
    {

        scans.AddScan("48 89 5C 24 ?? 48 89 7C 24 ?? 4C 89 74 24 ?? 55 48 8B EC 48 83 EC ?? 48 8B D9 0F BE FA", address =>
        {
            _showGauge = hooks!.CreateHook<ShowGaugeDelegate>(ShowGaugeImpl, address).Activate();
        });

        scans.AddScan("48 89 5C 24 ?? 48 89 74 24 ?? 55 57 41 54 41 56 41 57 48 8B EC 48 83 EC ?? 44 8B 65 ?? 44 8B F2", address =>
        {
            _bahamutTick = hooks!.CreateWrapper<BahamutTickGaugeDelegate>(address, out _);
        });

        scans.AddScan("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 80 B9 ?? ?? ?? ?? ?? 41 8B F0", address =>
        {
            _ramuhTick = hooks!.CreateWrapper<RamuhTickGaugeDelegate>(address, out _);
        });

        scans.AddScan("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 33 F6 48 8B F9 48 85 C0 74 ?? 48 8B 48 ?? EB ?? 48 8B CE 48 8B 01 4C 8D 4C 24 ?? BA ?? ?? ?? ?? 48 89 74 24 ?? 41 B8 ?? ?? ?? ?? FF 50 ?? 48 8B D8 48 85 C0 0F 84", address =>
        {
            _createGagueElement = hooks!.CreateHook<CreateGagueElementDelegate>(CreateGagueElementImpl, address).Activate();
        });

        scans.AddScan("48 89 5C 24 ?? 48 89 6C 24 ?? 56 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 44 24 ?? 48 8B F9", address =>
        {
            _setUiAddon = hooks!.CreateWrapper<SetUiAddonDelegate>(address, out _);
        });

        scans.AddScan("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 80 79 ?? ?? 8B FA 48 8B D9 75 ?? 40 F6 C7", address =>
        {
            _setUiElementHidden = hooks!.CreateHook<SetUiElementHiddenDelegate>(SetUiElementHiddenImpl, address).Activate();
        });

        scans.AddScan("48 89 5C 24 ?? 48 89 74 24 ?? 57 48 83 EC ?? 80 79 ?? ?? 8B FA 48 8B D9 74", address =>
        {
            _setUiElementHiddenOff = hooks!.CreateHook<SetUiElementHiddenOffDelegate>(SetUiElementHiddenOffImpl, address).Activate();
        });

        scans.AddScan("48 8B C4 48 89 58 ?? 48 89 68 ?? 89 50 ?? 56 57 41 56 48 83 EC ?? 4C 8D B1 ?? ?? ?? ?? 41 0F B6 F0", address =>
        {
            _updateGagueBar = hooks!.CreateHook<UpdateGagueBarDelegate>(UpdateGagueBarImpl, address).Activate();
        });

        scans.AddScan("48 8B C4 48 89 58 ?? 55 56 57 41 54 41 55 41 56 41 57 48 8D 68 ?? 48 81 EC ?? ?? ?? ?? C5 F8 29 70 ?? C5 F8 29 78 ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 45 ?? 48 8B DA", address =>
        {
            _initUiElement = hooks!.CreateHook<InitUiElementDelegate>(InitUiElementImpl, address).Activate();
        });
    }

    private unsafe char InitUiElementImpl(long a1, long a2, long a3)
    {
        if (*(float*)(a2 + 0x14) == 0.73f && *(float*)(a2 + 0x18) == 0.74f)
        {
            var scaleXAddress = a3 + 80 + 0x68;
            var scaleYAddress = a3 + 84 + 0x68;
            sizeXAddress = scaleXAddress - 0x78;
            sizeYAddress = scaleYAddress - 0x78;

            *(float*)(a2 + 0x14) = 0.8f;
            *(float*)(a2 + 0x18) = 0.8f;
            _initUiElement.Disable();
        }
        return _initUiElement.OriginalFunction(a1, a2, a3);
        
    }

    private long CreateGagueElementImpl(long a1)
    {
        OriginalGaguePointer = _createGagueElement.OriginalFunction(a1);
        GaguePointer = _createGagueElement.OriginalFunction(a1);
        _setUiAddon(GaguePointer, GagueElementUiId);

        battleScorePtrAddress = a1 + 240;
        battleScoreTechBonusPtrAddress = a1 + 248;
        uiSetupFinished = true;
        return OriginalGaguePointer;
    }

    private unsafe long UpdateGagueBarImpl(long a1, int a2, byte a3)
    {
        if (!uiSetupFinished)
            return _updateGagueBar.OriginalFunction(a1, a2, a3);

        var myPointer = *(long*)(GaguePointer + 0x30);
        if (a1 == myPointer)
        {
            long units = CurrentUnits - UnitsPerLevel.Where(kv => kv.Key < Level).Select(kv => kv.Value).Sum();
            *(int*)(myPointer + 644) = UnitsPerLevel[Level];
            return _updateGagueBar.OriginalFunction(a1, (int)units, a3);
        }
        return _updateGagueBar.OriginalFunction(a1, a2, a3);
    }

    private unsafe void SetUiElementHiddenImpl(long a1, char a2)
    {
        _setUiElementHidden.OriginalFunction(a1, a2);
        if (uiSetupFinished && a1 == *(long*)(OriginalGaguePointer + 24))
        {
            _setUiElementHidden.OriginalFunction(*(long*)(GaguePointer + 24), a2);
            OnPause();
        }
            
    }

    private unsafe void SetUiElementHiddenOffImpl(long a1, char a2)
    {
        if (uiSetupFinished)
        {
            long ptr1 = *(long*)(*(long*)battleScorePtrAddress + 24);
            long ptr2 = *(long*)(*(long*)battleScoreTechBonusPtrAddress + 24);

            if (hideBattleScore && (a1 == ptr1 || a1 == ptr2))
                return;
        }
        
        _setUiElementHiddenOff.OriginalFunction(a1, a2);
        if (uiSetupFinished && a1 == *(long*)(OriginalGaguePointer + 24))
        {
            _setUiElementHiddenOff.OriginalFunction(*(long*)(GaguePointer + 24), a2);
            if (IsShown)
                OnResume();
        }
    }

    private unsafe void ShowGaugeImpl(long a1, char eikon)
    {
        if (a1 == GaguePointer)
        {
            if (eikon == 0)
                _showGauge.OriginalFunction(a1, eikon);
        }
        else
            _showGauge.OriginalFunction(a1, eikon);
    }

    private int GetUnitsForGague()
    {
        return CurrentUnits - UnitsPerLevel.Where(kv => kv.Key < Level).Select(kv => kv.Value).Sum();
    }

    public unsafe void SetRankSize(int x, int y)
    {
        *(int*)sizeXAddress = x;
        *(int*)sizeYAddress = y;
    }

    public unsafe void UpdateGauge(bool isIncrease = true, bool forceUpdate = false)
    {
        if (forceUpdate)
        {
            // Force and update by changing the counter and restoring it, used when we only update the bar and want to force max level changes
            ComboCounter++;
            UpdateGauge(isIncrease, forceUpdate: false);
            ComboCounter--;
        }

        if (ComboCounter == 0) {
            SetRankSize(70, 70);
        }
        else if (ComboCounter >= 100)
        {
            SetRankSize(60, 60);
        }

        if (CurrentUnits == 0)
        {
            _showGauge.OriginalFunction(GaguePointer, (char)0);
            IsShown = false;
            SetRankSize(70, 70);
            return;
        }

        if (!IsShown || forceUpdate)
        {
            _showGauge.OriginalFunction(GaguePointer, GagueTypePerLevel[Level]);
            long ptr = *(long*)(GaguePointer + 88);
            *(uint*)(ptr + 248) = LevelToUiStringId[Level];
        }
            

        IsShown = true;

        CurrentUnits = Math.Min(CurrentUnits, MaxUnits);

        int newLevel = 1;
        var cumsum = 0;
        foreach (var kv in UnitsPerLevel)
        {
            cumsum += kv.Value;
            if (CurrentUnits > cumsum)
                newLevel++;
            else
                break;
        }

        if (newLevel != Level)
        {
            _showGauge.OriginalFunction(GaguePointer, GagueTypePerLevel[newLevel]);
            long ptr = *(long*)(GaguePointer + 88);
            *(uint*)(ptr + 248) = LevelToUiStringId[newLevel];
        }
        Level = newLevel;

        var units = GetUnitsForGague();

        if (IsMaxLevel())
        {
            long ptr = *(long*)(GaguePointer + 88);
            if (*(uint*)(ptr + 248) != LevelToUiStringId[MaxLevel + 1])
            {
                _showGauge.OriginalFunction(GaguePointer, GagueTypePerLevel[4]);
                *(uint*)(ptr + 248) = LevelToUiStringId[MaxLevel + 1];
            }
            
        }

        if (isIncrease)
        {
            ramuhSound1.SetInt16((uint)soundLayout.Columns["Unk3"].Offset, 0);
            ramuhSound2.SetInt16((uint)soundLayout.Columns["Unk3"].Offset, 0);

            _ramuhTick(GaguePointer, ComboCounter, IsMaxLevel() ? 1 : 9999, 0.0);

            ramuhSound1.SetInt16((uint)soundLayout.Columns["Unk3"].Offset, 164);
            ramuhSound2.SetInt16((uint)soundLayout.Columns["Unk3"].Offset, 165);
        }
        else
        {
            _bahamutTick(GaguePointer, units, UnitsPerLevel[Level], ComboCounter, IsMaxLevel() ? 1 : 9999, (char)(isIncrease ? 1 : 0));
        }
    }
}
