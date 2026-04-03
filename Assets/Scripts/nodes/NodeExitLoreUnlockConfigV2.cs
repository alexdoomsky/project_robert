using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Rob/Compendium/Node Exit Unlock Config V2", fileName = "NodeExitLoreUnlockConfigV2")]
public sealed class NodeExitLoreUnlockConfigV2 : ScriptableObject
{
    [Serializable]
    public sealed class Rule
    {
        public NodeTypeV2 nodeType = NodeTypeV2.Planet;
        public List<string> entryIds = new List<string>();
    }

    public List<Rule> rules = new List<Rule>();

    public List<string> GetPool(NodeTypeV2 type)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            var r = rules[i];
            if (r != null && r.nodeType == type)
                return r.entryIds;
        }
        return null;
    }
}
