// WeaponUIController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class WeaponUIController : MonoBehaviour
{
    public GameObject weaponButtonPrefab;
    public Transform weaponButtonContainer;
    public GameObject weaponUIPanel;
    public TextMeshProUGUI remainingAttacksText; // Reference to display remaining attacks

    public WeaponController selectedWeapon;

    private List<Button> weaponButtons = new List<Button>();

    public void Initialize()
    {
        HideWeaponUI();
    }

    public void ShowWeaponOptions(ModelController model)
    {
        ClearWeaponButtons();
        foreach (WeaponController weapon in model.GetComponentsInChildren<WeaponController>())
        {
            if (weapon.range > 0)
            {
                CreateWeaponButton(weapon, model);
            }
        }
        weaponUIPanel.SetActive(true);
    }

    // New method for melee weapons
    public void ShowMeleeWeaponOptions(ModelController model)
    {
        ClearWeaponButtons();
        foreach (WeaponController weapon in model.GetComponentsInChildren<WeaponController>())
        {
            if (weapon.range == 0)
            {
                CreateWeaponButton(weapon, model);
            }
        }
        weaponUIPanel.SetActive(true);
    }

    public void HideWeaponUI()
    {
        weaponUIPanel.SetActive(false);
        ClearWeaponButtons();
    }

    private void CreateWeaponButton(WeaponController weapon, ModelController model)
    {
        if (weaponButtonPrefab == null)
        {
            Debug.LogError("Weapon Button Prefab is not assigned!");
            return;
        }

        GameObject buttonObject = Instantiate(weaponButtonPrefab, weaponButtonContainer);
        Button button = buttonObject.GetComponent<Button>();
        TextMeshProUGUI buttonText = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
        buttonText.text = weapon.weaponName;

        bool isUsed = ShootingController.Instance.IsWeaponUsed(weapon);
        button.interactable = !isUsed;

        button.onClick.AddListener(() => OnWeaponButtonClicked(weapon, model));
        weaponButtons.Add(button);
    }

    private void ClearWeaponButtons()
    {
        foreach (Transform child in weaponButtonContainer)
        {
            Destroy(child.gameObject);
        }
        weaponButtons.Clear();
        selectedWeapon = null;
    }

    private void OnWeaponButtonClicked(WeaponController weapon, ModelController model)
    {
        selectedWeapon = weapon;
        ShootingController.Instance.SelectWeaponForShooting(model, weapon); // For melee attacks, reusing this method
        Debug.Log($"Weapon {weapon.weaponName} selected for attacks.");
    }
}

