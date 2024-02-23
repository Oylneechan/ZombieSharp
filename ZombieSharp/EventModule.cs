namespace ZombieSharp
{
    public partial class ZombieSharp
    {
        public void EventInitialize()
        {
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventRoundFreezeEnd>(OnRoundFreezeEnd);
            RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
            RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterEventHandler<EventPlayerJump>(OnPlayerJump);
            RegisterEventHandler<EventCsPreRestart>(OnPreRestart);

            RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnected);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
        }

        private void OnClientPutInServer(int client)
        {
            var player = Utilities.GetPlayerFromSlot(client);

            int clientindex = player.Slot;

            ClientSpawnDatas.Add(clientindex, new ClientSpawnData());

            ZombiePlayers.Add(clientindex, new ZombiePlayer());

            ZombiePlayers[clientindex].IsZombie = false;
            ZombiePlayers[clientindex].MotherZombieStatus = MotherZombieFlags.NONE;

            PlayerDeathTime.Add(clientindex, 0.0f);

            RegenTimer.Add(clientindex, null);

            PlayerSettingsOnPutInServer(player);

            WeaponOnClientPutInServer(clientindex);
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            var client = @event.Userid;

            PlayerSettingsAuthorized(client).Wait();
            return HookResult.Continue;
        }

        private void OnClientDisconnected(int client)
        {
            var player = Utilities.GetPlayerFromSlot(client);

            int clientindex = player.Slot;

            ClientSpawnDatas.Remove(clientindex);
            ZombiePlayers.Remove(clientindex);
            ClientPlayerClass.Remove(clientindex);
            PlayerDeathTime.Remove(clientindex);

            RegenTimerStop(player);
            RegenTimer.Remove(clientindex);

            WeaponOnClientDisconnect(clientindex);
        }

        private void OnMapStart(string mapname)
        {
            WeaponInitialize();
            bool load = SettingsIntialize(mapname);
            bool classes = PlayerClassIntialize();

            if (!load)
                ConfigSettings = new GameSettings();

            if (classes)
                PrecachePlayerModel();

            hitgroupLoad = HitGroupIntialize();
            RepeatKillerOnMapStart();
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            RemoveRoundObjective();
            RespawnTogglerSetup();

            Server.PrintToChatAll($" {ChatColors.Green}[Z:Sharp]{ChatColors.Default} The current game mode is the Human vs. Zombie, the zombie goal is to infect all human before time is running out.");

            return HookResult.Continue;
        }

        private HookResult OnRoundFreezeEnd(EventRoundFreezeEnd @event, GameEventInfo info)
        {
            bool warmup = GetGameRules().WarmupPeriod;

            if (warmup && !ConfigSettings.EnableOnWarmup)
                Server.PrintToChatAll($" {ChatColors.Green}[Z:Sharp]{ChatColors.Default} The current server has disabled infection in warmup round.");

            if (!warmup || ConfigSettings.EnableOnWarmup)
                InfectOnRoundFreezeEnd();

            return HookResult.Continue;
        }

        private HookResult OnPreRestart(EventCsPreRestart @event, GameEventInfo info)
        {
            bool warmup = GetGameRules().WarmupPeriod;

            if (!warmup || ConfigSettings.EnableOnWarmup)
            {
                ToggleRespawn(true, true);

                AddTimer(0.1f, () =>
                {
                    List<CCSPlayerController> clientlist = Utilities.GetPlayers();

                    foreach (var client in clientlist)
                    {
                        if (client.IsValid && IsPlayerAlive(client))
                        {
                            HumanizeClient(client);
                        }
                    }
                });
            }
            return HookResult.Continue;
        }

        private HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
        {
            bool warmup = GetGameRules().WarmupPeriod;

            if (!warmup || ConfigSettings.EnableOnWarmup)
            {
                // Reset Client Status
                AddTimer(0.2f, () =>
                {
                    // Reset Zombie Spawned here.
                    ZombieSpawned = false;

                    // avoiding zombie status glitch on human class like in zombie:reloaded
                    List<CCSPlayerController> clientlist = Utilities.GetPlayers();

                    // Reset Client Status
                    foreach (var client in clientlist)
                    {
                        if (!client.IsValid)
                            continue;

                        // Reset Client Status.
                        ZombiePlayers[client.Slot].IsZombie = false;

                        // if they were chosen as motherzombie then let's make them not to get chosen again.
                        if (ZombiePlayers[client.Slot].MotherZombieStatus == MotherZombieFlags.CHOSEN)
                            ZombiePlayers[client.Slot].MotherZombieStatus = MotherZombieFlags.LAST;
                    }
                });
            }

            return HookResult.Continue;
        }

        private HookResult OnPlayerHurt(EventPlayerHurt @event, GameEventInfo info)
        {
            if (ZombieSpawned)
            {
                var client = @event.Userid;
                var attacker = @event.Attacker;

                var weapon = @event.Weapon;
                var dmgHealth = @event.DmgHealth;
                var hitgroup = @event.Hitgroup;

                if (!attacker.IsValid || !client.IsValid)
                    return HookResult.Continue;

                if (IsClientZombie(attacker) && IsClientHuman(client) && string.Equals(weapon, "knife"))
                {
                    // Server.PrintToChatAll($"{client.PlayerName} Infected by {attacker.PlayerName}");
                    InfectClient(client, attacker);
                }

                if (IsClientZombie(client))
                {
                    if (ConfigSettings.CashOnDamage)
                        DamageCash(attacker, dmgHealth);

                    FindWeaponItemDefinition(attacker.PlayerPawn.Value.WeaponServices.ActiveWeapon, weapon);

                    KnockbackClient(client, attacker, dmgHealth, weapon, hitgroup);
                }
            }

            return HookResult.Continue;
        }

        private HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
        {
            var client = @event.Userid;
            var attacker = @event.Attacker;
            var weapon = @event.Weapon;

            if (ZombieSpawned)
            {
                CheckGameStatus();

                if (RespawnEnable)
                {
                    RespawnPlayer(client);
                    RepeatKillerOnPlayerDeath(client, attacker, weapon);
                }

                RegenTimerStop(client);
            }

            return HookResult.Continue;
        }

        public void RespawnPlayer(CCSPlayerController client)
        {
            if (ConfigSettings.RespawnTimer > 0.0f)
            {
                AddTimer(ConfigSettings.RespawnTimer, () =>
                {
                    // Server.PrintToChatAll($"Player {client.PlayerName} should be respawn here.");
                    RespawnClient(client);
                });
            }
        }

        public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
        {
            var client = @event.Userid;

            bool warmup = GetGameRules().WarmupPeriod;

            if (!warmup || ConfigSettings.EnableOnWarmup)
            {
                AddTimer(0.2f, () =>
                {
                    WeaponOnPlayerSpawn(client.Slot);

                    // if zombie already spawned then they become zombie.
                    if (ZombieSpawned)
                    {
                        // Server.PrintToChatAll($"Infect {client.PlayerName} on Spawn.");
                        InfectClient(client, null, false, false, true);
                    }

                    // else they're human!
                    else
                        HumanizeClient(client);

                    var clientPawn = client.PlayerPawn.Value;
                    var spawnPos = clientPawn.AbsOrigin!;
                    var spawnAngle = clientPawn.AbsRotation!;

                    ZTele_GetClientSpawnPoint(client, spawnPos, spawnAngle);
                });
            }

            return HookResult.Continue;
        }

        public HookResult OnPlayerJump(EventPlayerJump @event, GameEventInfo info)
        {
            var client = @event.Userid;

            var warmup = GetGameRules().WarmupPeriod;

            if (!warmup || ConfigSettings.EnableOnWarmup)
                JumpBoost(client);

            return HookResult.Continue;
        }

        public void JumpBoost(CCSPlayerController client)
        {
            var classData = PlayerClassDatas.PlayerClasses;
            var activeclass = ClientPlayerClass[client.Slot].ActiveClass;

            if (!GetGameRules().WarmupPeriod || ConfigSettings.EnableOnWarmup)
            {
                // if jump boost can apply after client is already jump.
                AddTimer(0.0f, () =>
                {
                    if (activeclass == null)
                    {
                        if (IsClientHuman(client))
                            activeclass = ConfigSettings.Human_Default;

                        else
                            activeclass = ConfigSettings.Zombie_Default;
                    }

                    if (classData.ContainsKey(activeclass))
                    {
                        client.PlayerPawn.Value.AbsVelocity.X *= classData[activeclass].Jump_Distance;
                        client.PlayerPawn.Value.AbsVelocity.Y *= classData[activeclass].Jump_Distance;
                        client.PlayerPawn.Value.AbsVelocity.Z *= classData[activeclass].Jump_Height;
                    }
                });
            }
        }

        private void DamageCash(CCSPlayerController client, int dmgHealth)
        {
            var money = client.InGameMoneyServices.Account;
            client.InGameMoneyServices.Account = money + dmgHealth;
            Utilities.SetStateChanged(client, "CCSPlayerController", "m_pInGameMoneyServices");
        }

        private void RemoveRoundObjective()
        {
            var objectivelist = new List<string>() { "func_bomb_target", "func_hostage_rescue", "hostage_entity", "c4" };

            foreach (string objectivename in objectivelist)
            {
                var entityIndex = Utilities.FindAllEntitiesByDesignerName<CEntityInstance>(objectivename);

                foreach (var entity in entityIndex)
                {
                    entity.Remove();
                }
            }
        }
    }
}