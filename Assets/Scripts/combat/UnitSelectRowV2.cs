using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitSelectRowV2 : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text countText;     // выбранные в бой: X / cap
    [SerializeField] private TMP_Text ownedText;     // инвентарь: сколько есть у игрока
    [SerializeField] private Image cardImage;        // картинка карточки (Sprite)

    [SerializeField] private Button plusButton;
    [SerializeField] private Button minusButton;

    private Action _onPlus;
    private Action _onMinus;

    // Старый интерфейс оставляем, чтобы ничего не отвалилось
    public void Bind(string title, Func<string> getCountText, Action onPlus, Action onMinus)
    {
        Bind(title, null, getCountText, null, onPlus, onMinus);
    }

    // Новый интерфейс: карточка + "в наличии"
    public void Bind(
        string title,
        Sprite cardSprite,
        Func<string> getCountText,
        Func<string> getOwnedText,
        Action onPlus,
        Action onMinus)
    {
        if (titleText != null) titleText.text = title;

        if (cardImage != null)
        {
            cardImage.sprite = cardSprite;
            // если спрайта нет — просто прячем картинку, чтобы не было пустой рамки
            cardImage.gameObject.SetActive(cardSprite != null);
        }

        _onPlus = onPlus;
        _onMinus = onMinus;

        if (plusButton != null)
        {
            plusButton.onClick.RemoveAllListeners();
            plusButton.onClick.AddListener(() => _onPlus?.Invoke());
        }

        if (minusButton != null)
        {
            minusButton.onClick.RemoveAllListeners();
            minusButton.onClick.AddListener(() => _onMinus?.Invoke());
        }

        Refresh(getCountText, getOwnedText);
    }

    public void Refresh(Func<string> getCountText, Func<string> getOwnedText = null)
    {
        if (countText != null && getCountText != null)
            countText.text = getCountText();

        if (ownedText != null)
        {
            if (getOwnedText != null)
            {
                ownedText.text = getOwnedText();
                ownedText.gameObject.SetActive(true);
            }
            else
            {
                ownedText.gameObject.SetActive(false);
            }
        }
    }
}
