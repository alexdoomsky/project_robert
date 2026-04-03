using UnityEngine;

public class AsteroidObstacleV2 : MonoBehaviour, IDamageableV2
{
    [SerializeField] private int hp = 6;

    public HexCellV2 CurrentCell { get; private set; }
    public bool IsAlive => hp > 0;

    public bool PlaceOnCell(HexCellV2 cell)
    {
        if (cell == null) return false;
        if (!cell.walkable) return false;
        if (cell.OccupantBlocksMovement) return false;

        CurrentCell = cell;
        // астероид блокирует движение
        if (!cell.TrySetOccupant(this, true))
            return false;

        transform.position = cell.transform.position;
        return true;
    }

    public void TakeDamage(int damage, UnitV2 attacker = null)
    {
        if (!IsAlive) return;
        hp -= damage;

        if (hp <= 0)
        {
            hp = 0;
            if (CurrentCell != null)
                CurrentCell.ClearOccupant(this);

            Destroy(gameObject);
        }
    }
}
