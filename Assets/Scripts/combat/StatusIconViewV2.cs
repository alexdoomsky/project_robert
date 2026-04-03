using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class StatusIconViewV2 : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text smallText; // опционально: например roundsLeft маленьким числом

    private StatusDefV2 _def;
    private StatusInfoV2 _info;

    public void Bind(StatusDefV2 def, StatusInfoV2 info)
    {
        _def = def;
        _info = info;

        if (icon != null) icon.sprite = def.icon;

        if (smallText != null)
        {
            // показывать roundsLeft, если есть смысл
            if (info.roundsLeft > 0) smallText.text = info.roundsLeft.ToString();
            else smallText.text = "";
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_def == null) return;

        string body = BuildDescription(_def.descriptionTemplate, _info);
        TooltipSystemV2.Show(_def.displayName, body);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipSystemV2.Hide();
    }

    private static string BuildDescription(string template, StatusInfoV2 info)
    {
        if (string.IsNullOrEmpty(template))
            return "";

        // {rounds} - roundsLeft
        // {a}/{b} - extraA/extraB
        return template
            .Replace("{rounds}", info.roundsLeft.ToString())
            .Replace("{a}", info.extraA.ToString())
            .Replace("{b}", info.extraB.ToString());
    }
}
