public struct StatusInfoV2
{
    public StatusId id;

    // если статус ходовый
    public int roundsLeft;

    // дополнительные числа (для барьера: extraA=currentHP, extraB=maxHP)
    public int extraA;
    public int extraB;
}
