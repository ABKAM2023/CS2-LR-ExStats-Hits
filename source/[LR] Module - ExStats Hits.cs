using System;
using MySqlConnector;
using Dapper;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using LevelsRanksApi;

namespace LevelsRanksExStatsHits;

[MinimumApiVersion(80)]
public class LevelsRanksExStatsHits : BasePlugin
{
    public override string ModuleName => "[LR] Module - ExStats Hits";
    public override string ModuleVersion => "1.1.0";
    public override string ModuleAuthor => "ABKAM designed by RoadSide Romeo & Wend4r";
    public override string ModuleDescription => "A plugin for tracking damage statistics.";

    private readonly PluginCapability<ILevelsRanksApi> _levelsRanksApiCapability = new("levels_ranks");
    private ILevelsRanksApi? _levelsRanksApi;

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        base.OnAllPluginsLoaded(hotReload);

        _levelsRanksApi = _levelsRanksApiCapability.Get();
        
        if (_levelsRanksApi == null)
        {
            Console.WriteLine("LevelsRanksApi is not initialized. Exiting Load method.");
            return;
        }

        CreateDbTableIfNotExists();
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
    }

    private HookResult OnPlayerHurt(EventPlayerHurt hurtEvent, GameEventInfo info)
    {
        try
        {
            if (hurtEvent != null && 
                hurtEvent.Attacker != null && 
                hurtEvent.Attacker.Entity != null)
            {
                bool isBot = false;
                try
                {
                    isBot = hurtEvent.Attacker.IsBot;
                }
                catch (System.ArgumentNullException)
                {
 
                }
            
                if (!isBot && hurtEvent.DmgHealth > 0) 
                {
                    var attackerSteamId64 = hurtEvent.Attacker.SteamID.ToString();
                    var attackerSteamId = _levelsRanksApi.ConvertToSteamId(ulong.Parse(attackerSteamId64));
                    if (string.IsNullOrEmpty(attackerSteamId))
                    {
                        return HookResult.Continue;
                    }

                    var hitGroup = hurtEvent.Hitgroup; 
                    var dmgHealth = hurtEvent.DmgHealth;
                    var dmgArmor = hurtEvent.DmgArmor;

                    var columnName = HitGroupToColumnName(hitGroup);
        
                    if (!string.IsNullOrEmpty(columnName))
                    {
                        Task.Run(() => UpdatePlayerStatsAsync(attackerSteamId, columnName, dmgHealth, dmgArmor));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            
        }
        return HookResult.Continue;
    }

    private string HitGroupToColumnName(int hitGroup)
    {
        return hitGroup switch
        {
            1 => "Head",
            2 => "Chest",
            3 => "Belly",
            4 => "LeftArm",
            5 => "RightArm",
            6 => "LeftLeg",
            7 => "RightLeg",
            8 => "Neak",
            _ => null,
        };
    }

    private async Task UpdatePlayerStatsAsync(string steamId, string columnName, int dmgHealth, int dmgArmor)
    {
        var connectionString = _levelsRanksApi.DbConnectionString;
        var tableName = $"{_levelsRanksApi.TableName}_hits";

        using (var connection = new MySqlConnection(connectionString))
        {
            await connection.OpenAsync();

            var query = $@"
INSERT INTO `{tableName}` 
(SteamID, DmgHealth, DmgArmor, {columnName})
VALUES (@SteamID, @DmgHealth, @DmgArmor, 1)
ON DUPLICATE KEY UPDATE 
    DmgHealth = DmgHealth + @DmgHealth, 
    DmgArmor = DmgArmor + @DmgArmor,
    {columnName} = {columnName} + 1;";

            var parameters = new 
            {
                SteamID = steamId,
                DmgHealth = dmgHealth,
                DmgArmor = dmgArmor
            };

            await connection.ExecuteAsync(query, parameters);
        }
    }

    private void CreateDbTableIfNotExists()
    {
        if (_levelsRanksApi == null)
        {
            Console.WriteLine("LevelsRanksApi is not initialized. Cannot create database table.");
            return;
        }

        var connectionString = _levelsRanksApi.DbConnectionString;
        var tableName = $"{_levelsRanksApi.TableName}_hits";
        var tableCharacterSet = "CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";
        var createTableQuery = $@"
            CREATE TABLE IF NOT EXISTS `{tableName}` 
            (
                `SteamID` varchar(32) NOT NULL PRIMARY KEY DEFAULT '', 
                `DmgHealth` int NOT NULL DEFAULT 0, 
                `DmgArmor` int NOT NULL DEFAULT 0, 
                `Head` int NOT NULL DEFAULT 0, 
                `Chest` int NOT NULL DEFAULT 0, 
                `Belly` int NOT NULL DEFAULT 0, 
                `LeftArm` int NOT NULL DEFAULT 0, 
                `RightArm` int NOT NULL DEFAULT 0, 
                `LeftLeg` int NOT NULL DEFAULT 0, 
                `RightLeg` int NOT NULL DEFAULT 0, 
                `Neak` int NOT NULL DEFAULT 0
            ) {tableCharacterSet};";

        using (var connection = new MySqlConnection(connectionString))
        {
            connection.Open();
            connection.Execute(createTableQuery);
        }
    } 
}
