using TMPro;
using UnityEngine;

public sealed class InteractPromptUIV2 : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text label;

    private void Awake()
    {
        if (root == null) root = gameObject;
        Hide();
    }

    public void Show(string text)
    {
        if (label != null) label.text = text;
        if (root != null && !root.activeSelf) root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null && root.activeSelf) root.SetActive(false);
    }
}
