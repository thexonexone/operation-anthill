using Anthill.Core.Common;

namespace Anthill.Core.Domain;

public sealed class AntDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public string ModelRoute { get; set; } = "";
    public List<string> AllowedTools { get; set; } = new();
    public bool AutoSpawned { get; set; } = false;
    public bool Enabled { get; set; } = true;
    public string CreatedAt { get; set; } = AnthillTime.NowUtc().ToIso();
    public string UpdatedAt { get; set; } = AnthillTime.NowUtc().ToIso();

    public Dictionary<string, object?> ToDict() => new()
    {
        ["id"] = Id, ["name"] = Name, ["display_name"] = DisplayName,
        ["description"] = Description, ["system_prompt"] = SystemPrompt,
        ["model_route"] = ModelRoute, ["allowed_tools"] = AllowedTools,
        ["auto_spawned"] = AutoSpawned, ["enabled"] = Enabled,
        ["built_in"] = false,
        ["created_at"] = CreatedAt, ["updated_at"] = UpdatedAt,
    };
}

public sealed class PheromoneConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceAnt { get; set; } = "";
    public string TargetAnt { get; set; } = "";
    public string Label { get; set; } = "";
    public double Strength { get; set; } = 1.0;
    public string CreatedAt { get; set; } = AnthillTime.NowUtc().ToIso();

    public Dictionary<string, object?> ToDict() => new()
    {
        ["id"] = Id, ["source_ant"] = SourceAnt, ["target_ant"] = TargetAnt,
        ["label"] = Label, ["strength"] = Strength, ["created_at"] = CreatedAt,
    };
}
