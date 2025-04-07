using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Firebase.Storage;
using Firebase.Extensions;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityChess;

/// <summary>
/// Manages the DLC system for chess piece profile pictures
/// </summary>
public class DLCManager : MonoBehaviour
{
    // Singleton instance
    public static DLCManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject dlcStorePanel;
    [SerializeField] private Button closeStoreButton;
    [SerializeField] private TextMeshProUGUI creditsText;
    [SerializeField] private Button storeButton; // Button to open the store
    [SerializeField] private RawImage localPlayerProfileImage; // Profile picture for local player
    [SerializeField] private RawImage remotePlayerProfileImage; // Profile picture for remote player
    
    [Header("Profile Items")]
    [SerializeField] private ProfilePictureUI[] profileItems; // Reference to manually created items

    [Header("DLC Configuration")]
    [SerializeField] private int defaultCredits = 10000;
    
    // Player data
    private int playerCredits;
    private HashSet<string> ownedProfiles = new HashSet<string>();
    private string currentProfileId = "default";
    
    // Storage for available profile pictures
    private List<ProfileData> availableProfiles = new List<ProfileData>();
    
    // Reference to Firebase Storage
    private FirebaseStorage storage;
    private StorageReference storageReference;
    
    // Cache for downloaded profile pictures
    private Dictionary<string, Texture2D> profileTextureCache = new Dictionary<string, Texture2D>();
    
    // Store remote player's profile ID
    private string remotePlayerProfileId = "default";
    
    // Default profile texture
    private Texture2D defaultProfileTexture;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        playerCredits = defaultCredits;
        
        // Create a default profile texture
        defaultProfileTexture = new Texture2D(64, 64);
        Color[] colors = new Color[64 * 64];
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = Color.gray;
        }
        defaultProfileTexture.SetPixels(colors);
        defaultProfileTexture.Apply();
    }

    private void Start()
    {
        // Initialize Firebase
        InitializeFirebase();
        
        // Setup UI buttons
        if (closeStoreButton != null)
            closeStoreButton.onClick.AddListener(CloseDLCStore);
            
        if (storeButton != null)
            storeButton.onClick.AddListener(OpenDLCStore);
            
        // Initially hide the store panel
        if (dlcStorePanel != null)
            dlcStorePanel.SetActive(false);
        
        // Initialize local profile picture with default
        if (localPlayerProfileImage != null)
            localPlayerProfileImage.texture = defaultProfileTexture;
            
        // Initialize remote profile picture with default
        if (remotePlayerProfileImage != null)
            remotePlayerProfileImage.texture = defaultProfileTexture;
        
        // Load player data
        LoadPlayerData();
        
        // Fetch available profile pictures
        FetchAvailableProfiles();
    }

    private void InitializeFirebase()
    {
        Debug.Log("Initializing Firebase Storage...");
        try
        {
            // Initialize Firebase Storage
            storage = FirebaseStorage.DefaultInstance;
            storageReference = storage.GetReferenceFromUrl("gs://jeromecgdlc.firebasestorage.app");
            Debug.Log("Firebase Storage initialized successfully");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to initialize Firebase Storage: {ex.Message}");
        }
    }

    private void FetchAvailableProfiles()
    {
        Debug.Log("Fetching available profile pictures...");
        
        // Using the chess pieces in firebase as profile pics 
        availableProfiles = new List<ProfileData>
        {
            new ProfileData { id = "chess-bishop-black", displayName = "Bishop", price = 200 },
            new ProfileData { id = "chess-king-black", displayName = "King", price = 300 },
            new ProfileData { id = "chess-knight-black", displayName = "Knight", price = 200 },
            new ProfileData { id = "chess-pawn-black", displayName = "Pawn", price = 100 },
            new ProfileData { id = "chess-queen-black", displayName = "Queen", price = 300 },
            new ProfileData { id = "chess-rook-black", displayName = "Rook", price = 200 }
        };
        
        Debug.Log($"Loaded {availableProfiles.Count} available profile pictures");
    }

    public async Task<Texture2D> LoadProfileTexture(string profileId)
    {
        // For default profile, return the default texture
        if (profileId == "default")
        {
            return defaultProfileTexture;
        }
        
        // Check if already have the texture cached
        if (profileTextureCache.TryGetValue(profileId, out Texture2D cachedTexture))
        {
            Debug.Log($"Using cached texture for profile {profileId}");
            return cachedTexture;
        }
        
        Debug.Log($"Downloading texture for profile {profileId}...");
        
        try
        {
            // direct URL of Firebase Storage API
            string encodedFileName = UnityEngine.Networking.UnityWebRequest.EscapeURL(profileId + ".png");
            string directUrl = $"https://firebasestorage.googleapis.com/v0/b/jeromecgdlc.firebasestorage.app/o/{encodedFileName}?alt=media";
            
            Debug.Log($"Using direct URL: {directUrl}");
            
            // Download the texture directly
            Texture2D texture = await DownloadTexture(directUrl);
            
            if (texture != null)
            {
                // Cache the texture
                profileTextureCache[profileId] = texture;
                Debug.Log($"Successfully downloaded and cached texture for profile {profileId}");
                return texture;
            }
            else
            {
                Debug.LogError($"Failed to download texture for profile {profileId}");
                return defaultProfileTexture;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error loading profile texture {profileId}: {e.Message}");
            return defaultProfileTexture;
        }
    }

    private async Task<Texture2D> DownloadTexture(string url)
    {
        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            var operation = www.SendWebRequest();

            while (!operation.isDone)
                await Task.Yield();

            if (www.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Failed to download texture: {www.error}");
                return null;
            }

            return ((UnityEngine.Networking.DownloadHandlerTexture)www.downloadHandler).texture;
        }
    }

    public void OpenDLCStore()
    {
        Debug.Log("Opening Profile Store");
        if (dlcStorePanel != null)
        {
            dlcStorePanel.SetActive(true);
            
            // Populate the store when opening
            PopulateProfileStore();
            
            // Refresh the store UI
            RefreshStoreUI();
        }
    }

    public void CloseDLCStore()
    {
        Debug.Log("Closing Profile Store");
        if (dlcStorePanel != null)
        {
            dlcStorePanel.SetActive(false);
        }
    }

    private void PopulateProfileStore()
    {
        Debug.Log("Populating profile store with items");
        
        if (profileItems.Length < availableProfiles.Count) {
            Debug.LogError($"Not enough profile UI items! Have {profileItems.Length}, need {availableProfiles.Count}");
            return;
        }
        
        // For each profile data, initialize a UI item
        for (int i = 0; i < availableProfiles.Count; i++)
        {
            var profile = availableProfiles[i];
            var profileUI = profileItems[i];
            
            // Enable the UI element
            profileUI.gameObject.SetActive(true);
            
            // Initialize with profile data
            profileUI.Initialize(profile, this);
            
            // Load preview image
            StartCoroutine(LoadProfilePreview(profile.id, profileUI));
        }
        
        // Hide any unused profile items
        for (int i = availableProfiles.Count; i < profileItems.Length; i++)
        {
            profileItems[i].gameObject.SetActive(false);
        }
    }

    private IEnumerator LoadProfilePreview(string profileId, ProfilePictureUI profileItemUI)
    {
        Task<Texture2D> textureTask = LoadProfileTexture(profileId);
        
        while (!textureTask.IsCompleted)
            yield return null;
        
        if (textureTask.Result != null && profileItemUI != null)
        {
            profileItemUI.SetPreviewImage(textureTask.Result);
        }
    }

    private void RefreshStoreUI()
    {
        // Update credits display
        if (creditsText != null)
        {
            creditsText.text = $"Credits: {playerCredits}";
        }
        
        // Refresh purchase buttons based on ownership
        for (int i = 0; i < availableProfiles.Count && i < profileItems.Length; i++)
        {
            ProfilePictureUI profileItemUI = profileItems[i];
            string profileId = availableProfiles[i].id;
            
            profileItemUI.RefreshUI(ownedProfiles.Contains(profileId), currentProfileId == profileId);
        }
        
        Debug.Log("Store UI refreshed");
    }

    private void LoadPlayerData()
    {
        // Load owned profiles
        string ownedProfilesStr = PlayerPrefs.GetString("OwnedProfiles", "");
        if (!string.IsNullOrEmpty(ownedProfilesStr))
        {
            string[] profileIds = ownedProfilesStr.Split(',');
            ownedProfiles = new HashSet<string>(profileIds);
        }
        
        // Load current profile
        currentProfileId = PlayerPrefs.GetString("CurrentProfile", "default");
        
        // Load credits
        playerCredits = PlayerPrefs.GetInt("PlayerCredits", defaultCredits);
        
        Debug.Log($"Loaded player data: {ownedProfiles.Count} owned profiles, {playerCredits} credits, current profile: {currentProfileId}");
        
        // Apply current profile
        if (currentProfileId != "default")
        {
            StartCoroutine(ApplyLocalPlayerProfile(currentProfileId));
        }
    }

    private void SavePlayerData()
    {
        // Save owned profiles
        string ownedProfilesStr = string.Join(",", ownedProfiles);
        PlayerPrefs.SetString("OwnedProfiles", ownedProfilesStr);
        
        // Save current profile
        PlayerPrefs.SetString("CurrentProfile", currentProfileId);
        
        // Save credits
        PlayerPrefs.SetInt("PlayerCredits", playerCredits);
        
        PlayerPrefs.Save();
        Debug.Log("Player data saved");
    }

    public bool PurchaseProfile(string profileId)
    {
        // Find the profile data
        ProfileData profile = availableProfiles.Find(p => p.id == profileId);
        if (profile == null)
        {
            Debug.LogError($"Profile {profileId} not found!");
            return false;
        }
        
        // Check if already owned
        if (ownedProfiles.Contains(profileId))
        {
            Debug.Log($"Profile {profileId} already owned");
            return true;
        }
        
        // Check if enough credits
        if (playerCredits < profile.price)
        {
            Debug.Log($"Not enough credits to purchase profile {profileId}. Need {profile.price}, have {playerCredits}");
            return false;
        }
        
        // Deduct credits
        playerCredits -= profile.price;
        
        // Add to owned profiles
        ownedProfiles.Add(profileId);
        
        // Save player data
        SavePlayerData();
        
        // Refresh UI
        RefreshStoreUI();
        
        Debug.Log($"Successfully purchased profile {profileId} for {profile.price} credits");
        return true;
    }

    public void EquipProfile(string profileId)
    {
        // Check if owned (or default)
        if (!ownedProfiles.Contains(profileId) && profileId != "default")
        {
            Debug.LogError($"Cannot equip profile {profileId} as it is not owned!");
            return;
        }
        
        AnalyticsLogger.LogDLCPurchase(profileId);

        // Set as current profile
        currentProfileId = profileId;
        
        // Save player data
        SavePlayerData();
        
        // Refresh UI
        RefreshStoreUI();
        
        // Apply the profile picture
        StartCoroutine(ApplyLocalPlayerProfile(profileId));
        
        // Notify other clients about the profile change
        SyncProfileToNetwork(profileId);
        
        Debug.Log($"Equipped profile {profileId}");
    }

    private IEnumerator ApplyLocalPlayerProfile(string profileId)
    {
        if (localPlayerProfileImage == null)
            yield break;
            
        Task<Texture2D> textureTask = LoadProfileTexture(profileId);
        
        while (!textureTask.IsCompleted)
            yield return null;
            
        if (textureTask.Result != null)
        {
            localPlayerProfileImage.texture = textureTask.Result;
            Debug.Log($"Applied profile {profileId} to local player UI");
        }
    }

    private IEnumerator ApplyRemotePlayerProfile(string profileId)
    {
        if (remotePlayerProfileImage == null)
            yield break;
            
        remotePlayerProfileId = profileId;
            
        Task<Texture2D> textureTask = LoadProfileTexture(profileId);
        
        while (!textureTask.IsCompleted)
            yield return null;
            
        if (textureTask.Result != null)
        {
            remotePlayerProfileImage.texture = textureTask.Result;
            Debug.Log($"Applied profile {profileId} to remote player UI");
        }
    }

    public void SyncProfileToNetwork(string profileId)
    {
        // If have a NetworkManager and it's connected, sync the profile
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
        {
            // If using ChessMoveRelay for network communication
            if (ChessMoveRelay.Instance != null)
            {
                // Call the method to handle profile updates
                ChessMoveRelay.Instance.SendProfileUpdateServerRpc(profileId);
                Debug.Log($"Sent profile update to server: {profileId}");
            }
            else
            {
                Debug.LogWarning("ChessMoveRelay instance not found. Cannot sync profile.");
            }
        }
        else
        {
            Debug.Log("Not connected to network, skipping profile sync");
        }
    }

    // Call this when receiving a profile update from the network
    public void HandleRemoteProfileUpdate(string profileId)
    {
        Debug.Log($"Received remote profile update: {profileId}");
        StartCoroutine(ApplyRemotePlayerProfile(profileId));
    }
}