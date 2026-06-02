using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class MenuManager : MonoBehaviour
{
    private bool isSet;

    private SettingsManager settingsManager;
    [SerializeField] private MenuAnimation _animator;
    [SerializeField] private CircleMenuController _circleMenu;

    [SerializeField] private TextMeshProUGUI playerNameText;

    [SerializeField] private List<TextMeshProUGUI> menuItems = new List<TextMeshProUGUI>();
    private int currentMenuIndex = 0;

    private void Update()
    {
        if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
        {
            if (_circleMenu != null) _circleMenu.Move(1);
            else ChangeMenuIndex(-1);
        }
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
        {
            if (_circleMenu != null) _circleMenu.Move(-1);
            else ChangeMenuIndex(1);
        }
        else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            SelectCurrentMenu();
        }
    }

    private void ChangeMenuIndex(int direction)
    {
        if (menuItems.Count == 0) return;

        if (currentMenuIndex >= 0 && currentMenuIndex < menuItems.Count)
            menuItems[currentMenuIndex].fontStyle &= ~FontStyles.Underline;

        currentMenuIndex += direction;
        if (currentMenuIndex < 0) currentMenuIndex = menuItems.Count - 1;
        else if (currentMenuIndex >= menuItems.Count) currentMenuIndex = 0;

        menuItems[currentMenuIndex].fontStyle |= FontStyles.Underline;
    }

    private void SelectCurrentMenu()
    {
        if (!isSet || menuItems.Count == 0) return;

        int index = _circleMenu != null ? _circleMenu.currentIndex : currentMenuIndex;
        SelectMenu(index);
    }

    private void SetPlayerInfos()
    {
        if (playerNameText == null) return;
        string playerName = settingsManager.GetLocalPlayerName();
        playerNameText.text = string.IsNullOrEmpty(playerName) ? "Player" : playerName;
    }

    private void SelectMenu(int index)
    {
        if (!isSet) return;
        isSet = false;

        switch (index)
        {
            case 0:
                _animator.FadeIn(onComplete: () => SceneManager.LoadSceneAsync("Settings"));
                break;
            case 1:
                _animator.FadeIn(onComplete: () => SceneManager.LoadSceneAsync("FreePlay"));
                break;
            case 2:
                _animator.FadeIn(onComplete: () => SceneManager.LoadSceneAsync("Rankings"));
                break;
            default:
                isSet = true;
                break;
        }
    }

    private void Start()
    {
        isSet = true;
        settingsManager = SettingsManager.Instance;
        SetPlayerInfos();
        InitializeMenu();
    }

    private void InitializeMenu()
    {
        if (menuItems.Count == 0) return;

        currentMenuIndex = _circleMenu != null ? _circleMenu.currentIndex : 0;

        foreach (var item in menuItems)
        {
            if (item != null)
                item.fontStyle &= ~FontStyles.Underline;
        }

        menuItems[currentMenuIndex].fontStyle |= FontStyles.Underline;
    }
}
