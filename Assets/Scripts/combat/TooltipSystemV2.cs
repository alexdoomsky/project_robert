using UnityEngine;
using TMPro;

public class TooltipSystemV2 : MonoBehaviour
{
    private static TooltipSystemV2 _inst;

    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;

    private void Awake()
    {
        _inst = this;
        if (root != null) root.SetActive(false);
    }

    public static void Show(string title, string body)
    {
        if (_inst == null) return;

        if (_inst.root != null) _inst.root.SetActive(true);
        if (_inst.titleText != null) _inst.titleText.text = title ?? "";
        if (_inst.bodyText != null) _inst.bodyText.text = body ?? "";
    }

    public static void Hide()
    {
        if (_inst == null) return;
        if (_inst.root != null) _inst.root.SetActive(false);
    }
}
