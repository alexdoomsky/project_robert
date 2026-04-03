using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CompendiumUIV2 : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button closeButton;

    [Header("Timed Notification")]
    [Tooltip("If > 0, you can show timed notifications that auto-hide.")]
    [SerializeField] private float notificationDuration = 5f;

    [Tooltip("Optional. Image type should be Filled. FillAmount will show remaining time.")]
    [SerializeField] private Image progressFillImage;

    [Tooltip("Use unscaled time (recommended for UI).")]
    [SerializeField] private bool useUnscaledTime = true;

    private Coroutine _autoHideRoutine;

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

    public void Show(string title, string body)
    {
        StopAutoHide();

        if (titleText != null) titleText.text = title ?? "";
        if (bodyText != null) bodyText.text = body ?? "";

        if (progressFillImage != null)
            progressFillImage.fillAmount = 0f;

        if (root != null) root.SetActive(true);
    }

    /// <summary>
    /// Shows a notification that auto-hides after durationSeconds.
    /// If durationSeconds <= 0, uses notificationDuration from inspector.
    /// </summary>
    public void ShowNotificationTimed(string title, string body, float durationSeconds = -1f)
    {
        StopAutoHide();

        if (durationSeconds <= 0f)
            durationSeconds = notificationDuration;

        if (durationSeconds <= 0f)
        {
            // fallback: behave like normal show
            Show(title, body);
            return;
        }

        if (titleText != null) titleText.text = title ?? "";
        if (bodyText != null) bodyText.text = body ?? "";

        if (root != null) root.SetActive(true);

        _autoHideRoutine = StartCoroutine(AutoHideRoutine(durationSeconds));
    }

    public void Hide()
    {
        StopAutoHide();
        if (root != null) root.SetActive(false);
    }

    private void StopAutoHide()
    {
        if (_autoHideRoutine != null)
        {
            StopCoroutine(_autoHideRoutine);
            _autoHideRoutine = null;
        }
    }

    private IEnumerator AutoHideRoutine(float seconds)
    {
        float t = 0f;

        if (progressFillImage != null)
            progressFillImage.fillAmount = 1f;

        while (t < seconds)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt;

            if (progressFillImage != null)
            {
                float k = Mathf.Clamp01(t / seconds);
                progressFillImage.fillAmount = 1f - k;
            }

            yield return null;
        }

        Hide();
    }
}
