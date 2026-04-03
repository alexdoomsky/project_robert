using UnityEngine;
using UnityEngine.UI;

public sealed class ExpeditionPanelV2 : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (root == null) root = gameObject;

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        Hide();
    }

    public void Show() => root.SetActive(true);
    public void Hide() => root.SetActive(false);
}
