using OWML.Common;
using OWML.ModHelper.Menus.NewMenuSystem;
using UnityEngine;

namespace ErnestoChase.Debug;

internal class DebugMenu
{
    private static DebugMenu _instance;
    private static DebugMenu Instance => _instance ??= new DebugMenu();

    private GameObject _pauseMenuObject;

    private DebugMenu()
    {
    }

    public static void Initialize()
    {
        if (Instance._pauseMenuObject) return;
        Instance.CreateMenu();
    }

    private void CreateMenu()
    {
        var pauseMenuManager = StartupPopupPatches.menuManager.PauseMenuManager;
        var debugMenu = pauseMenuManager.MakePauseListMenu("ERNESTO CHASE // DEBUG MENU");
        _pauseMenuObject = debugMenu.gameObject;
        pauseMenuManager.MakeMenuOpenButton("ERNESTO CHASE // DEBUG MENU", debugMenu, 0, false);

        debugMenu.AddButton("test debug action", () => ErnestoChase.WriteDebugMessage(":3"));
    }
}

internal static class DebugExtensions
{
    public static void AddButton(this Menu menu, string title, SubmitAction.SubmitActionEvent submitAction) =>
        StartupPopupPatches
            .menuManager
            .PauseMenuManager
            .MakeSimpleButton(title, 0, false, menu)
            .OnSubmitAction += submitAction;
}