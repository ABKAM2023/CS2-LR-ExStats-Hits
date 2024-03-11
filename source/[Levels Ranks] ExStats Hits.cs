using System;
using MySqlConnector;
using Dapper;
using System.Threading.Tasks;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using LevelsRanks.API;

namespace LevelsRanksExStatsHits;

[MinimumApiVersion(80)]
public class LevelsRanksExStatsHits : BasePlugin
{
    public override string ModuleName => "[LR] ExStats Hits";
    public override string ModuleVersion => "1.0";
    public override string ModuleAuthor => "ABKAM designed by RoadSide Romeo & Wend4r";
    public override string ModuleDescription => "A plugin for tracking damage statistics.";

    private readonly PluginCapability<IPointsManager> _pointsManagerCapability = new("levelsranks");
    private IPointsManager? _pointsManager;

    public override void Load(bool hotReload)
    {
        base.Load(hotReload);

        _pointsManager = _pointsManagerCapability.Get();
        
        if (_pointsManager == null)
        {
            Console.WriteLine("PointsManager is not initialized. Exiting Load method.");
            return;
        }

        CreateDbTableIfNotExists();
        RegisterEventHandler<EventPlayerHurt>(OnPlayerHurt);
    }

    private HookResult OnPlayerHurt(EventPlayerHurt hurtEvent, GameEventInfo info)
    {
        if (hurtEvent.Attacker != null && !hurtEvent.Attacker.IsBot)
        {
            var attackerSteamId2 = hurtEvent.Attacker.SteamID.ToString();
            var attackerSteamId = ConvertSteamID64ToSteamID(attackerSteamId2);
            var hitGroup = hurtEvent.Hitgroup; 
            var dmgHealth = hurtEvent.DmgHealth;
            var dmgArmor = hurtEvent.DmgArmor;

            var columnName = HitGroupToColumnName(hitGroup);
            
            if (!string.IsNullOrEmpty(columnName))
            {
                UpdatePlayerStatsAsync(attackerSteamId, hitGroup, dmgHealth, dmgArmor);
            }
        }
        return HookResult.Continue;
    }

    private string HitGroupToColumnName(int hitGroup)
    {
        switch (hitGroup)
        {
            case 1: return "Head";
            case 2: return "Chest";
            case 3: return "Belly";
            case 4: return "LeftArm";
            case 5: return "RightArm";
            case 6: return "LeftLeg";
            case 7: return "RightLeg";
            case 8: return "Neak";
            default: return null; 
        }
    }

    private async Task UpdatePlayerStatsAsync(string steamId, int hitGroup, int dmgHealth, int dmgArmor)
    {
        if (_pointsManager == null)
        {
            Console.WriteLine("PointsManager is not initialized. Cannot update player stats.");
            return;
        }

        var connectionString = _pointsManager.GetConnectionString();
        var dbConfig = _pointsManager.GetDatabaseConfig();

        using (var connection = new MySqlConnection(connectionString))
        {
            await connection.OpenAsync();

            var tableName = $"{dbConfig.Name}_hits";

            string columnName = HitGroupToColumnName(hitGroup);

            var query = $@"
INSERT INTO `{tableName}` 
(SteamID, DmgHealth, DmgArmor, {columnName})
VALUES (@SteamID, @DmgHealth, @DmgArmor, IF(@ColumnName IS NULL, 0, 1))
ON DUPLICATE KEY UPDATE 
    DmgHealth = DmgHealth + @DmgHealth, 
    DmgArmor = DmgArmor + @DmgArmor,
    {columnName} = IF(@ColumnName IS NULL, {columnName}, {columnName} + 1);";

            var parameters = new 
            {
                SteamID = steamId,
                DmgHealth = dmgHealth,
                DmgArmor = dmgArmor,
                ColumnName = columnName 
            };

            await connection.ExecuteAsync(query, parameters);
        }
    }

    private void CreateDbTableIfNotExists()
    {
        if (_pointsManager == null)
        {
            Console.WriteLine("PointsManager is not initialized. Cannot create database table.");
            return;
        }

        var connectionString = _pointsManager.GetConnectionString();
        var dbConfig = _pointsManager.GetDatabaseConfig();
        
        using (var connection = new MySqlConnection(connectionString))
        {
            connection.Open();
            
            var tableName = $"{dbConfig.Name}_hits";
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

            connection.Execute(createTableQuery);
        }
    } 
    public static string ConvertSteamID64ToSteamID(string steamId64)
    {
        if (ulong.TryParse(steamId64, out var communityId) && communityId > 76561197960265728)
        {
            var authServer = (communityId - 76561197960265728) % 2;
            var authId = (communityId - 76561197960265728 - authServer) / 2;
            return $"STEAM_1:{authServer}:{authId}";
        }
        return null; 
    }   
}
