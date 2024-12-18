using System.Numerics;
using System.Text.Json;
using AllaganAssistantNode.Data;
using AllaganAssistantNode.Helpers;
using ECommons.DalamudServices;
using ECommons.ExcelServices;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using OpenAI.Assistants;

namespace AllaganAssistantNode.Controllers;

public class FunctionToolController
{
    public string DoRunWork(RequiredAction requiredactions)
    {
        string result = string.Empty;
        switch (requiredactions.FunctionName)
        {
            case nameof(GetPlayerData):
                result = GetPlayerData();
                break;
            case nameof(GetActiveFates):
                result = GetActiveFates();
                break;
            case nameof(MoveCharacterToVector):
                result = MoveCharacterToVector(requiredactions.FunctionArguments);
                break;
            case nameof(GetNearbyGameObjects):
                result = GetNearbyGameObjects();
                break;
            case nameof(GetDataForSpecificGameObject):
                result = GetDataForSpecificGameObject(requiredactions.FunctionArguments);
                break;
            default:
                result = "ERROR: Unknown function";
                break;
        }

        Svc.Log.Debug($"Successfully executed {requiredactions.FunctionName} for {requiredactions.ToolCallId}");
        Svc.Log.Verbose(result);
        return result;
    }

    private string GetPlayerData()
    {
        if (!Svc.ClientState.IsLoggedIn)
            return "{The player has not logged in yet}";
        var json = JsonConvert.SerializeObject(new
        {
            Name = Player.Name,
            Interactable = Player.Interactable,
            CurrentWorld = Player.CurrentWorld,
            HomeWorld = Player.HomeWorld,
            GrandCompany = Player.GrandCompany.ToString(),
            Level = Player.Level,
            Job = Player.Job.ToString(),
            Territory = ExcelTerritoryHelper.GetName(Player.Territory).ToString(),
        }, Formatting.Indented);
        return json;
    }

    private string MoveCharacterToVector(string argsJson)
    {
        using JsonDocument argumentsJson = JsonDocument.Parse(argsJson);
        bool hasX = argumentsJson.RootElement.TryGetProperty("x", out JsonElement xElement);
        bool hasY = argumentsJson.RootElement.TryGetProperty("y", out JsonElement yElement);
        bool hasZ = argumentsJson.RootElement.TryGetProperty("z", out JsonElement zElement);
        if (!hasX || !hasY || !hasZ)
        {
            Svc.Log.Info($"Arguments are missing from JSON: {argsJson}");
            return "ERROR: Arguments are missing from JSON";
        }
        var vector = new Vector3(xElement.GetSingle(), yElement.GetSingle(), zElement.GetSingle());
        IpcSubscribers.VNavmesh_IPCSubscriber.SimpleMove_PathfindAndMoveTo(vector, true);
        return "Movement request successfully queued";
    }

    private string GetActiveFates()
    {
        var fates = Svc.Fates.Where(f => true);

        List<object> activeFates = new();
        foreach (var fate in fates)
        {
            var obj = new
            {
                Name = fate.Name.ToString(),
                Description = fate.Description.ToString(),
                Level = fate.Level,
                TimeRemaining = fate.TimeRemaining,
                Duration = fate.Duration,
                Objective = fate.Objective.ToString(),
                State = fate.State.ToString(),
                HasTwistOfFateBonus = fate.HasBonus,
                MaxLevel = fate.MaxLevel,
                WorldPosition = fate.Position,
            };
            activeFates.Add(obj);
        }

        return JsonConvert.SerializeObject(activeFates, Formatting.Indented);
    }

    private string GetNearbyGameObjects()
    {
        var objs = Svc.Objects.Where(o => true);
        List<object> nearbyGameObjects = new();
        foreach (var obj in objs)
        {
            if (!obj.IsTargetable)
                continue;
            var aiObj = new
            {
                Name = obj.Name.TextValue,
                DistanceToPlayer = Player.DistanceTo(obj.Position),
                Type = obj.ObjectKind.ToString(),
                Position = obj.Position,
                GameObjectId = obj.GameObjectId
            };
            nearbyGameObjects.Add(aiObj);
        }
        var json = JsonConvert.SerializeObject(nearbyGameObjects, Formatting.Indented);
        return json;
    }

    private string GetDataForSpecificGameObject(string argsJson)
    {
        using JsonDocument argumentsJson = JsonDocument.Parse(argsJson);
        var hasId = argumentsJson.RootElement.TryGetProperty("gameObjectId", out JsonElement id);
        if (!hasId)
        {
            Svc.Log.Error($"Arguments are missing from JSON: {argsJson}");
            return "ERROR: Arguments are missing from JSON";
        }

        var gameObjectId = (ulong)id.GetInt64();
        var gameObject = Svc.Objects.FirstOrDefault(o => o.GameObjectId == gameObjectId);
        if (gameObject == null)
        {
            Svc.Log.Error($"Failed to find game object with id: {gameObjectId}");
            return "ERROR: Failed to find game object with id: " + gameObjectId;
        }

        var jsonObj = new
        {
            Name = gameObject.Name.TextValue //TODO: Get real data by game object type
        };
        var json = JsonConvert.SerializeObject(jsonObj, Formatting.Indented);
        return json;
    }

    public readonly ToolDefinition ActiveFateTool = new FunctionToolDefinition()
    {
        FunctionName = nameof(GetActiveFates),
        Description = "Get the active FATEs for the player that are in the current zone.",
    };

    public readonly ToolDefinition PlayerDataTool = new FunctionToolDefinition()
    {
        FunctionName = nameof(GetPlayerData),
        Description = "Retrieve a JSON object containing basic player information.",
    };

    public readonly ToolDefinition MoveToLocationTool = new FunctionToolDefinition()
    {
        FunctionName = nameof(MoveCharacterToVector),
        Description = "Move the player character to the specified position using advanced navigation logic.",
        Parameters = BinaryData.FromString("""
                                           {
                                               "type": "object",
                                               "properties": {
                                                   "x": {
                                                       "type": "number",
                                                       "description": "The X coordinate of the position to move to"
                                                   },
                                                   "y": {
                                                       "type": "number",
                                                       "description": "The Y coordinate of the position to move to"
                                                   },
                                                   "z": {
                                                        "type": "number",
                                                        "description": "The Z coordinate of the position to move to"
                                                   }
                                               },
                                               "required": [ "x", "y", "z" ]
                                           }
                                           """)
    };
    public readonly ToolDefinition SpecificGameObjectDataTool = new FunctionToolDefinition()
    {
        FunctionName = nameof(GetDataForSpecificGameObject),
        Description = "Get more details about a specific game object that is nearby to the player",
        Parameters = BinaryData.FromString("""
                                           {
                                               "type": "object",
                                               "properties": {
                                                   "gameObjectId": {
                                                       "type": "number",
                                                       "description": "The ID of the game object to get more details about. Will be provided by the GetNearbyGameObjects function."
                                                   }
                                               },
                                               "required": [ "gameObjectId" ]
                                           }
                                           """)
    };

    public readonly ToolDefinition GetNearbyGameObjectsTool = new FunctionToolDefinition()
    {
        FunctionName = nameof(GetNearbyGameObjects),
        Description = "Retrieve a list of game objects that are nearby to the player",
    };
}