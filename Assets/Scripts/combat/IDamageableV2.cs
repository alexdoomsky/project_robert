public interface IDamageableV2
{
    bool IsAlive { get; }
    void TakeDamage(int damage, UnitV2 attacker = null);
}
