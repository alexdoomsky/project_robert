using TMPro;
using UnityEngine;

public class TurnDebugUIV2 : MonoBehaviour
{
    [SerializeField] private TurnManagerV2 turnManager;
    [SerializeField] private UnitMovementControllerV2 movementController;

    [Header("UI")]
    [SerializeField] private TMP_Text phaseText;
    [SerializeField] private TMP_Text selectedText;

    private void Start()
    {
        if (turnManager == null) turnManager = FindObjectOfType<TurnManagerV2>();
        if (movementController == null) movementController = FindObjectOfType<UnitMovementControllerV2>();
    }

    private void Update()
    {
        if (turnManager != null && phaseText != null)
            phaseText.text = $"Phase: {turnManager.CurrentPhase}";

        if (movementController != null && selectedText != null)
        {
            var u = movementController.SelectedUnit;
            if (u == null)
            {
                selectedText.text = "Selected: none";
            }
            else
            {
                selectedText.text =
                    $"Selected: {u.name}\nHP: {u.HP}\nMP: {u.MovementLeft}\nActions: {u.ActionsLeft}\nTeam: {u.Team}";
            }
        }
    }
}
