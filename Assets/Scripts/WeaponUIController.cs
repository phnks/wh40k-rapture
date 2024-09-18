using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class WeaponUIController : MonoBehaviour
{
    public GameObject weaponButtonPrefab; // Prefab for the weapon button
    public Transform weaponButtonContainer; // Container to hold weapon buttons
    public GameObject weaponUIPanel; // Reference to the actual UI Panel (Canvas or Panel containing the buttons)

    private List<Button> weaponButtons = new List<Button>();

    // Initialize the UI Controller
    public void Initialize()
    {
        HideWeaponUI(); // Hide UI on initialization
    }

    // Show weapon options for the selected model
    public void ShowWeaponOptions(ModelController model)
    {
        ClearWeaponButtons(); // Clear previous buttons
        foreach (WeaponController weapon in model.GetComponentsInChildren<WeaponController>())
        {
            if (weapon.range > 0) // Skip weapons with 0 range
            {
                CreateWeaponButton(weapon, model);
            }
        }
        weaponUIPanel.SetActive(true); // Show the weapon UI panel
    }

    // Hide the weapon UI
    public void HideWeaponUI()
    {
        weaponUIPanel.SetActive(false); // Hide the actual UI panel
        ClearWeaponButtons(); // Clear any existing weapon buttons
    }

    // Create a button for each weapon
    private void CreateWeaponButton(WeaponController weapon, ModelController model)
    {
        GameObject buttonObject = Instantiate(weaponButtonPrefab, weaponButtonContainer);
        Button button = buttonObject.GetComponent<Button>();
        TextMeshProUGUI buttonText = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
        buttonText.text = weapon.weaponName;

        // **NEW: Disable the button if the weapon has been used**
        bool isUsed = ShootingController.Instance.IsWeaponUsed(weapon);
        button.interactable = !isUsed;

        button.onClick.AddListener(() => OnWeaponButtonClicked(weapon, model));
        weaponButtons.Add(button);
    }

    // Clear existing weapon buttons
    private void ClearWeaponButtons()
    {
        foreach (Button button in weaponButtons)
        {
            Destroy(button.gameObject);
        }
        weaponButtons.Clear();
    }

    // Handle weapon button click
    private void OnWeaponButtonClicked(WeaponController weapon, ModelController model)
    {
        ShootingController.Instance.SelectWeaponForShooting(model, weapon); // Trigger shooting logic
    }
}

