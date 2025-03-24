using UnityEngine;
using Unity.Netcode;
using UnityEngine.UI;

public class NetworkSessionManager : MonoBehaviour
{
    public Button hostButton;
    public Button joinButton;
    public Button leaveButton;

    private void Start()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
        leaveButton.onClick.AddListener(OnLeaveClicked);
    }

    public void OnHostClicked()
    {
        NetworkManager.Singleton.StartHost();
        Debug.Log("Starting Host");
    }

    public void OnJoinClicked()
    {
        NetworkManager.Singleton.StartClient();
        Debug.Log("Attempting to Join");
    }

    public void OnLeaveClicked()
    {
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.Shutdown();
            Debug.Log("Left session");
        }
    }
}
