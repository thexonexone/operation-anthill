using System.Text.Json;
using Anthill.Core.Common;
using Anthill.Core.Domain;

namespace Anthill.Core.Memory;

public sealed partial class SqliteMemory
{
    public void SaveAntDefinition(AntDefinition def)
    {
        lock (_writeLock)
        {
            using var conn = Connect();
            NonQuery(conn, null,
                "INSERT OR REPLACE INTO ant_definitions " +
                "(id, name, display_name, description, system_prompt, model_route, allowed_tools_json, auto_spawned, enabled, created_at, updated_at) " +
                "VALUES (@id, @name, @dn, @desc, @sp, @mr, @tools, @auto, @en, @ca, @ua)",
                ("@id", def.Id), ("@name", def.Name), ("@dn", def.DisplayName),
                ("@desc", def.Description), ("@sp", def.SystemPrompt), ("@mr", def.ModelRoute),
                ("@tools", JsonSerializer.Serialize(def.AllowedTools)),
                ("@auto", def.AutoSpawned ? 1 : 0), ("@en", def.Enabled ? 1 : 0),
                ("@ca", def.CreatedAt), ("@ua", def.UpdatedAt));
            InvalidateCache();
        }
    }

    public AntDefinition? GetAntDefinition(string name)
    {
        var rows = Query("SELECT * FROM ant_definitions WHERE name = @n", ("@n", name));
        return rows.Count == 0 ? null : RowToAntDef(rows[0]);
    }

    public List<AntDefinition> ListAntDefinitions()
    {
        var rows = Query("SELECT * FROM ant_definitions ORDER BY created_at");
        return rows.Select(RowToAntDef).ToList();
    }

    public bool DeleteAntDefinition(string name)
    {
        lock (_writeLock)
        {
            using var conn = Connect();
            NonQuery(conn, null, "DELETE FROM ant_definitions WHERE name = @n", ("@n", name));
            InvalidateCache();
        }
        var changed = Scalar("SELECT changes()");
        return changed is long c && c > 0;
    }

    public void SavePheromoneConnection(PheromoneConnection pc)
    {
        lock (_writeLock)
        {
            using var conn = Connect();
            NonQuery(conn, null,
                "INSERT OR REPLACE INTO pheromone_connections (id, source_ant, target_ant, label, strength, created_at) " +
                "VALUES (@id, @src, @tgt, @lbl, @str, @ca)",
                ("@id", pc.Id), ("@src", pc.SourceAnt), ("@tgt", pc.TargetAnt),
                ("@lbl", pc.Label), ("@str", pc.Strength), ("@ca", pc.CreatedAt));
            InvalidateCache();
        }
    }

    public List<PheromoneConnection> ListPheromoneConnections()
    {
        var rows = Query("SELECT * FROM pheromone_connections ORDER BY created_at");
        return rows.Select(r => new PheromoneConnection
        {
            Id = r["id"]?.ToString() ?? "",
            SourceAnt = r["source_ant"]?.ToString() ?? "",
            TargetAnt = r["target_ant"]?.ToString() ?? "",
            Label = r["label"]?.ToString() ?? "",
            Strength = r.TryGetValue("strength", out var s) && s is double d ? d : 1.0,
            CreatedAt = r["created_at"]?.ToString() ?? "",
        }).ToList();
    }

    public bool DeletePheromoneConnection(string id)
    {
        lock (_writeLock)
        {
            using var conn = Connect();
            NonQuery(conn, null, "DELETE FROM pheromone_connections WHERE id = @id", ("@id", id));
            InvalidateCache();
        }
        var changed = Scalar("SELECT changes()");
        return changed is long c && c > 0;
    }

    private static AntDefinition RowToAntDef(Dictionary<string, object?> r)
    {
        var tools = new List<string>();
        try { tools = JsonSerializer.Deserialize<List<string>>(r.GetValueOrDefault("allowed_tools_json")?.ToString() ?? "[]") ?? new(); }
        catch { /* malformed JSON — keep empty */ }
        return new AntDefinition
        {
            Id = r.GetValueOrDefault("id")?.ToString() ?? "",
            Name = r.GetValueOrDefault("name")?.ToString() ?? "",
            DisplayName = r.GetValueOrDefault("display_name")?.ToString() ?? "",
            Description = r.GetValueOrDefault("description")?.ToString() ?? "",
            SystemPrompt = r.GetValueOrDefault("system_prompt")?.ToString() ?? "",
            ModelRoute = r.GetValueOrDefault("model_route")?.ToString() ?? "",
            AllowedTools = tools,
            AutoSpawned = AsLong(r.GetValueOrDefault("auto_spawned")) == 1,
            Enabled = AsLong(r.GetValueOrDefault("enabled")) != 0,
            CreatedAt = r.GetValueOrDefault("created_at")?.ToString() ?? "",
            UpdatedAt = r.GetValueOrDefault("updated_at")?.ToString() ?? "",
        };
    }
}
