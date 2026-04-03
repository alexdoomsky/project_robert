using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Status/StatusDatabaseV2")]
public class StatusDatabaseV2 : ScriptableObject
{
    public List<StatusDefV2> defs = new();

    private Dictionary<StatusId, StatusDefV2> _map;

    public StatusDefV2 Get(StatusId id)
    {
        _map ??= Build();
        _map.TryGetValue(id, out var def);
        return def;
    }

    private Dictionary<StatusId, StatusDefV2> Build()
    {
        var d = new Dictionary<StatusId, StatusDefV2>();
        foreach (var def in defs)
        {
            if (def == null) continue;
            d[def.id] = def;
        }
        return d;
    }
}
