using UnityEngine;
using UnityEngine.UI;

public class DebugToolsUI : MonoBehaviour
{
    public Button saveButton;
    public Button loadButton;
    public Button dlcButton;

    private void Start()
    {
        saveButton.onClick.AddListener(SaveToFirebase);
        loadButton.onClick.AddListener(LoadFromFirebase);
        dlcButton.onClick.AddListener(SimulateDLCPurchase);
    }

    private void SaveToFirebase()
    {
        string userId = SystemInfo.deviceUniqueIdentifier;
        GameStateSaver.Instance.SaveGameStateToFirebase(userId);
    }

    private void LoadFromFirebase()
    {
        string userId = SystemInfo.deviceUniqueIdentifier;
        GameManager.Instance.StartNewGame(); // Optional reset before load
        GameStateSaver.Instance.LoadMostRecentGameState(userId).ContinueWith(task =>
        {
            if (!string.IsNullOrEmpty(task.Result))
            {
                GameManager.Instance.LoadGame(task.Result);
                Debug.Log("[DebugUI] Game restored from Firebase.");
            }
        });
    }

    private void SimulateDLCPurchase()
    {
        AnalyticsLogger.LogDLCPurchase("CoolHatPack");
    }
}
