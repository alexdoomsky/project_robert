using UnityEngine;

[CreateAssetMenu(menuName = "Status/StatusDefV2")]
public class StatusDefV2 : ScriptableObject
{
    public StatusId id;
    public Sprite icon;
    public string displayName;

    [TextArea] public string descriptionTemplate;

    // Пример шаблона:
    // "юнит занят наведением. не может двигаться и атаковать. осталось ходов: {rounds}"
    // "щит активен. hp: {a}/{b}. осталось ходов: {rounds}"
    // "рывок активен. осталось ходов: {rounds}"
}
