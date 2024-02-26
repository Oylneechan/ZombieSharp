using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Cvars.Validators;
using Microsoft.Extensions.Logging;

namespace ZombieSharp
{
    public partial class ZombieSharp
    {
        public FakeConVar<float> CVAR_RespawnTimer = new("zs_respawn_delay", "Respawn Delaying after player death, Set to 0 will disable it.", 5.0f, ConVarFlags.FCVAR_NONE, new RangeValidator<float>(0, 60));
        public FakeConVar<float> CVAR_FirstInfectionTimer = new("zs_infect_first_infection_delay", "Specify How long before first mother zombie will spawn after round freeze end.", 15.0f, ConVarFlags.FCVAR_NONE, new RangeValidator<float>(15, 60));
        public FakeConVar<float> CVAR_MotherZombieRatio = new("zs_infect_mzombie_ratio", "Mother Zombie Ratio", 7.0f, ConVarFlags.FCVAR_NONE, new RangeValidator<float>(1, 64));
        public FakeConVar<bool> CVAR_TeleportMotherZombie = new("zs_infect_mzombie_respawn", "Teleport Mother Zombie Back to respawn.", true);
        public FakeConVar<bool> CVAR_EnableOnWarmup = new("zs_infect_warmup_enable", "Enable Infection during warmup, not recommend to enable as it possibly corrupt the memory", false);
        public FakeConVar<float> CVAR_RepeatKillerThreshold = new("zs_repetkiller_threshold", "Death Ratio before disable spawning entirely in that round.", 0.0f, ConVarFlags.FCVAR_NONE, new RangeValidator<float>(0, 64));
        public FakeConVar<int> CVAR_ZombieDrop = new("zs_infect_drop_mode", "Weapon Drop Method for Zombie when get infected, [0 = Strip Weapon | 1 = Force Drop]", 0, ConVarFlags.FCVAR_NONE, new RangeValidator<int>(0, 1));
        public FakeConVar<bool> CVAR_CashOnDamage = new("zs_damage_cash", "Allow player to earn money from damage zombie or not.", true);

        public FakeConVar<string> CVAR_Human_Default = new("zs_classes_human_default", "Default Human Class", "human_default");
        public FakeConVar<string> CVAR_Zombie_Default = new("zs_classes_zombie_default", "Default Zombie Class", "zombie_default");
        public FakeConVar<string> CVAR_Mother_Zombie = new("zs_classes_motherzombie_default", "Default Mother Zombie Class", "motherzombie");

        public void SettingsOnLoad()
        {
            RegisterFakeConVars(typeof(ConVar));
        }

        public bool SettingsIntialize(string mapname)
        {
            var configFolder = Path.Combine(Server.GameDirectory, "csgo/cfg/zombiesharp/");

            if (!Directory.Exists(configFolder))
            {
                Logger.LogError($"[Z:Sharp] Couldn't find directory {configFolder}");
                return false;
            }

            var configPath = Path.Combine(configFolder, "zombiesharp.cfg");

            if (!File.Exists(configPath))
            {
                CreateAutoExecCFG(configPath);
                Logger.LogInformation($"[Z:Sharp] Creating {configPath}");
            }

            Server.ExecuteCommand("exec cfg/zombiesharp/zombiesharp.cfg");

            var mapConfig = Path.Combine(configFolder, mapname + ".cfg");

            if (File.Exists(mapConfig))
            {
                Logger.LogInformation($"[Z:Sharp] Found Map cfg file loading {mapConfig}");
            }

            return true;
        }

        public void CreateAutoExecCFG(string path)
        {
            StreamWriter execCfg = File.CreateText(path);

            execCfg.WriteLine("zs_infect_spawntime \"15.0\"");
            execCfg.WriteLine("zs_infect_mzombie_ratio \"7.0\"");
            execCfg.WriteLine("zs_infect_mzombie_min \"1\"");
            execCfg.WriteLine("zs_infect_mzombie_respawn \"1\"");
            execCfg.WriteLine("zs_infect_enable_warmup \"0\"");
            execCfg.WriteLine("zs_infect_drop_mode \"0\"");
            execCfg.WriteLine("zs_infect_cash_damage \"1\"");

            execCfg.WriteLine("zs_classes_human_default \"human_default\"");
            execCfg.WriteLine("zs_classes_zombie_default \"zombie_default\"");
            execCfg.WriteLine("zs_classes_mother_default \"motherzombie\"");

            execCfg.Close();
        }
    }
}
