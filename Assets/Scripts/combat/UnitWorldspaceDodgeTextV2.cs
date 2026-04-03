using System.Collections;
using TMPro;
using UnityEngine;

public class UnitWorldspaceDodgeTextV2 : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private UnitV2 unit;
    [SerializeField] private TMP_Text dodgeText;

    [Header("Settings")]
    [Tooltip("—колько секунд показывать текст уворота")]
    [SerializeField] private float visibleTime = 1.0f;

    private Coroutine _showRoutine;

    private void Awake()
    {
        if (unit == null)
            unit = GetComponentInParent<UnitV2>();

        if (dodgeText != null)
            dodgeText.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (unit == null) return;
        unit.OnDodged += HandleDodged;
    }

    private void OnDisable()
    {
        if (unit == null) return;
        unit.OnDodged -= HandleDodged;
    }

    private void HandleDodged(UnitV2 u)
    {
        if (dodgeText == null) return;

        // если увороты идут подр€д Ч перезапускаем таймер
        if (_showRoutine != null)
            StopCoroutine(_showRoutine);

        _showRoutine = StartCoroutine(ShowTemporarily());
    }

    private IEnumerator ShowTemporarily()
    {
        dodgeText.gameObject.SetActive(true);

        yield return new WaitForSeconds(visibleTime);

        dodgeText.gameObject.SetActive(false);
        _showRoutine = null;
    }
}
