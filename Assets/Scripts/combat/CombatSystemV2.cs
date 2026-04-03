using UnityEngine;

public static class CombatSystemV2
{
    public static int HexDistance(HexCellV2 a, HexCellV2 b)
    {
        if (a == null || b == null) return int.MaxValue;
        return HexDistance(a.Col, a.Row, b.Col, b.Row);
    }

    // odd-r -> cube -> distance
    public static int HexDistance(int c1, int r1, int c2, int r2)
    {
        var A = OddRToCube(c1, r1);
        var B = OddRToCube(c2, r2);
        return (Mathf.Abs(A.x - B.x) + Mathf.Abs(A.y - B.y) + Mathf.Abs(A.z - B.z)) / 2;
    }

    private static Vector3Int OddRToCube(int col, int row)
    {
        int x = col - ((row - (row & 1)) / 2);
        int z = row;
        int y = -x - z;
        return new Vector3Int(x, y, z);
    }

    public static bool TryPerformAttack(UnitV2 attacker, IDamageableV2 target, HexCellV2 targetCell, out string reason)
    {
        reason = "";

        if (attacker == null) { reason = "attacker null"; return false; }
        if (target == null) { reason = "target null"; return false; }
        if (!attacker.IsAlive) { reason = "attacker dead"; return false; }
        if (!target.IsAlive) { reason = "target dead"; return false; }
        if (!attacker.CanAct) { reason = "no actions"; return false; }
        if (attacker.CurrentCell == null || targetCell == null) { reason = "no cell"; return false; }

        int dist = HexDistance(attacker.CurrentCell, targetCell);
        if (dist > attacker.AttackRange) { reason = $"out of range ({dist}>{attacker.AttackRange})"; return false; }

        // Turn attacker towards the target cell (visualRoot inside UnitV2)
        attacker.FaceCell(targetCell);

        // Simple visual feedback: tracer to the center of the target cell.
        // (If manager not present in scene, it's a no-op.)
        AttackVFXManagerV2.Instance?.PlayTracer(attacker, targetCell);

        int dmg = attacker.Damage;

        attacker.ConsumeAction();

        // Evade only for UnitV2 (not abilities; this is a basic attack)
        if (target is UnitV2 u)
        {
            if (u.TryEvadeAttack())
            {
                Debug.Log($"[Combat] {u.name} evaded attack from {attacker.name}");
                return true;
            }
        }

        target.TakeDamage(dmg, attacker);
        Debug.Log($"[Combat] {attacker.name} attacked {target} for {dmg} (dist {dist})");
        return true;
    }
}
