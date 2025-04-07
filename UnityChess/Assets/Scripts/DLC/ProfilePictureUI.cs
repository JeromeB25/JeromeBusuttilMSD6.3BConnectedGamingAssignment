using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI component for individual profile picture items in the store
/// </summary>
public class ProfilePictureUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI profileNameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private RawImage previewImage;
    [SerializeField] private Button purchaseButton;
    [SerializeField] private Button equipButton;
    [SerializeField] private GameObject ownedIndicator;
    [SerializeField] private GameObject equippedIndicator;
    
    private ProfileData profileData;
    private DLCManager dlcManager;
    
    public string ProfileId => profileData?.id;
    
    public void Initialize(ProfileData data, DLCManager manager)
    {
        profileData = data;
        dlcManager = manager;
        
        if (profileNameText != null)
        {
            profileNameText.text = data.displayName;
            profileNameText.color = Color.white;  // Ensure text is visible
        }
            
        if (priceText != null)
        {
            priceText.text = $"{data.price} Credits";
            priceText.color = Color.yellow;  // Ensure text is visible
        }
            
        // Set up button events
        if (purchaseButton != null)
        {
            purchaseButton.onClick.RemoveAllListeners();
            purchaseButton.onClick.AddListener(OnPurchaseClicked);
            // Make button text visible
            TextMeshProUGUI buttonText = purchaseButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null) buttonText.color = Color.white;
        }
            
        if (equipButton != null)
        {
            equipButton.onClick.RemoveAllListeners();
            equipButton.onClick.AddListener(OnEquipClicked);
            // Make button text visible
            TextMeshProUGUI buttonText = equipButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null) buttonText.color = Color.white;
        }
            
        // Hide indicators initially
        if (ownedIndicator != null)
            ownedIndicator.SetActive(false);
            
        if (equippedIndicator != null)
            equippedIndicator.SetActive(false);
            
        // Make sure preview image is reset
        if (previewImage != null)
        {
            previewImage.texture = null;
            previewImage.color = Color.white;  // Full opacity
        }
    }
    
    public void SetPreviewImage(Texture2D texture)
    {
        if (previewImage != null && texture != null)
        {
            Debug.Log($"Setting preview image for {profileData.displayName}: Texture size={texture.width}x{texture.height}");
            previewImage.texture = texture;
            previewImage.color = Color.white;  // Ensure full opacity
            
            // Log the state after setting
            Debug.Log($"RawImage state: texture={previewImage.texture != null}, color={previewImage.color}, enabled={previewImage.enabled}");
        }
        else
        {
            Debug.LogError($"Cannot set preview image: previewImage={previewImage != null}, texture={texture != null}");
        }
    }
    
    public void RefreshUI(bool isOwned, bool isEquipped)
    {
        if (purchaseButton != null)
            purchaseButton.gameObject.SetActive(!isOwned);
            
        if (equipButton != null)
            equipButton.gameObject.SetActive(isOwned && !isEquipped);
            
        if (ownedIndicator != null)
            ownedIndicator.SetActive(isOwned);
            
        if (equippedIndicator != null)
            equippedIndicator.SetActive(isEquipped);
    }
    
    private void OnPurchaseClicked()
    {
        Debug.Log($"Purchase clicked for {profileData.displayName}");
        if (dlcManager != null && profileData != null)
        {
            dlcManager.PurchaseProfile(profileData.id);
        }
    }
    
    private void OnEquipClicked()
    {
        Debug.Log($"Equip clicked for {profileData.displayName}");
        if (dlcManager != null && profileData != null)
        {
            dlcManager.EquipProfile(profileData.id);
        }
    }
}