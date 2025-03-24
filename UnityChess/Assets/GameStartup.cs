using Unity.Netcode;
using UnityEngine;

public class GameStartup : MonoBehaviour
{
    private void Start()
    {
        if (ParrelSync.ClonesManager.IsClone())
        {
            Debug.Log("Starting Client (ParrelSync clone)");
            NetworkManager.Singleton.StartClient();
        }
        else
        {
            Debug.Log("Starting Host (Main project)");
            NetworkManager.Singleton.StartHost();

            // this is critical
            GameManager.Instance.StartNewGame();
        }
    }
}
