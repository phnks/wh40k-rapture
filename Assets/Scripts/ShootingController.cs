using UnityEngine;
using System.Collections.Generic; // Required for HashSet<>

public class ShootingController : MonoBehaviour
{
    public static ShootingController Instance; // Singleton instance

    private WeaponController selectedWeapon;
    private ModelController selectedModel;
    private GameObject rangeIndicator; // Visual indicator for weapon range
    private HashSet<WeaponController> usedWeapons = new HashSet<WeaponController>(); // Track used weapons

    void Awake()
    {
        Instance = this; // Set up the singleton instance
    }

    // Select a weapon for shooting
    public void SelectWeaponForShooting(ModelController model, WeaponController weapon)
    {
        if (usedWeapons.Contains(weapon))
        {
            GameController.Instance.ShowPlayerErrorMessage("Weapon has already been used this phase!");
            return;
        }

        selectedModel = model;
        selectedWeapon = weapon;
        ShowWeaponRangeIndicator();
    }

    // Show weapon range indicator
    private void ShowWeaponRangeIndicator()
    {
        // Create a weapon range indicator if it doesn't exist
        if (rangeIndicator == null)
        {
            rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rangeIndicator.transform.position = new Vector3(selectedModel.transform.position.x, 58.0f, selectedModel.transform.position.z); // Set height to 58
            float radius = selectedWeapon.range; // Use world space distance directly, already converted in WeaponController
            rangeIndicator.transform.localScale = new Vector3(radius * 2, 0.01f, radius * 2); // Properly scale to match range

            // Create a transparent material
            Material transparentMaterial = new Material(Shader.Find("Standard"));
            Color factionColor = selectedModel.faction == ModelController.Faction.AdeptusMechanicus ? Color.red : Color.green;
            transparentMaterial.color = new Color(factionColor.r, factionColor.g, factionColor.b, 0.3f); // Faction color with transparency
            transparentMaterial.SetFloat("_Mode", 3); // Set rendering mode to Transparent
            transparentMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            transparentMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            transparentMaterial.SetInt("_ZWrite", 0);
            transparentMaterial.DisableKeyword("_ALPHATEST_ON");
            transparentMaterial.EnableKeyword("_ALPHABLEND_ON");
            transparentMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            transparentMaterial.renderQueue = 3000;

            rangeIndicator.GetComponent<Renderer>().material = transparentMaterial; // Assign the transparent material
            rangeIndicator.GetComponent<Collider>().enabled = false; // Disable collision for this visual
        }
        else
        {
            rangeIndicator.transform.position = new Vector3(selectedModel.transform.position.x, 58.0f, selectedModel.transform.position.z);
            float radius = selectedWeapon.range; // Use world space distance directly, already converted
            rangeIndicator.transform.localScale = new Vector3(radius * 2, 0.01f, radius * 2); // Update size to match range

            // Update color based on the current selected model's faction
            Renderer rangeRenderer = rangeIndicator.GetComponent<Renderer>();
            Color factionColor = selectedModel.faction == ModelController.Faction.AdeptusMechanicus ? Color.red : Color.green;
            rangeRenderer.material.color = new Color(factionColor.r, factionColor.g, factionColor.b, 0.3f);

            rangeIndicator.SetActive(true); // Reactivate if already created
        }

        Debug.Log($"Showing range indicator for {selectedWeapon.weaponName} with range {selectedWeapon.range}");
    }

    // Hide weapon range indicator
    public void HideWeaponRangeIndicator()
    {
        if (rangeIndicator != null)
        {
            rangeIndicator.SetActive(false); // Hide the weapon range indicator
        }
    }

    // Perform shooting
    public void HandleShooting(ModelController targetModel)
    {
        if (selectedWeapon == null || selectedModel == null)
        {
            Debug.LogError("Weapon or model not selected for shooting.");
            return;
        }

        // Range check
        float distance = Vector3.Distance(selectedModel.transform.position, targetModel.transform.position);
        if (distance > selectedWeapon.range)
        {
            GameController.Instance.ShowPlayerErrorMessage("Target is out of range!");
            return;
        }

        Debug.Log($"Shooting {selectedWeapon.weaponName} at {targetModel.gameObject.name}");

        // Roll to hit
        int successfulHits = 0;
        for (int i = 0; i < selectedWeapon.numberOfShots; i++)
        {
            int roll = DiceRoller.RollD6();
            Debug.Log($"Rolling to hit: {roll}");
            if (roll >= selectedModel.ballisticSkill)
            {
                successfulHits++;
            }
        }

        if (successfulHits == 0)
        {
            Debug.Log("All shots missed.");
            DisableUsedWeapon(); // Disable weapon after use
            GameController.Instance.DeselectAllModels(); // Deselect the model after shooting
            return;
        }

        // Roll to wound
        int successfulWounds = 0;
        for (int i = 0; i < successfulHits; i++)
        {
            int roll = DiceRoller.RollD6();
            Debug.Log($"Rolling to wound: {roll}");
            if (IsWoundSuccessful(roll, selectedWeapon.strength, targetModel.toughness))
            {
                successfulWounds++;
            }
        }

        if (successfulWounds == 0)
        {
            Debug.Log("No wounds inflicted.");
            DisableUsedWeapon(); // Disable weapon after use
            GameController.Instance.DeselectAllModels(); // Deselect the model after shooting
            return;
        }

        // Roll for armor save
        int damageInflicted = 0;
        for (int i = 0; i < successfulWounds; i++)
        {
            int roll = DiceRoller.RollD6();
            int saveRollRequired = Mathf.Min(targetModel.armourSave - selectedWeapon.armourPiercing, targetModel.invulnerabilitySave);
            Debug.Log($"Rolling for armor save: {roll}, required: {saveRollRequired}");
            if (roll < saveRollRequired)
            {
                damageInflicted += selectedWeapon.damage;
            }
        }

        // Apply damage
        if (damageInflicted > 0)
        {
            Debug.Log($"{targetModel.gameObject.name} takes {damageInflicted} damage!");
            targetModel.TakeDamage(damageInflicted);
        }
        else
        {
            Debug.Log("All wounds were saved!");
        }

        DisableUsedWeapon(); // Disable weapon after use
        selectedWeapon = null; // Reset selection after shooting
        selectedModel = null;
        HideWeaponRangeIndicator(); // Hide range indicator after shooting
        GameController.Instance.DeselectAllModels(); // Deselect the model after shooting
    }

    private void DisableUsedWeapon()
    {
        if (selectedWeapon != null)
        {
            usedWeapons.Add(selectedWeapon); // Mark weapon as used
        }
    }

    // Determine if a wound roll is successful
    private bool IsWoundSuccessful(int roll, int weaponStrength, int targetToughness)
    {
        if (weaponStrength >= 2 * targetToughness) return roll >= 2;
        if (weaponStrength > targetToughness) return roll >= 3;
        if (weaponStrength == targetToughness) return roll >= 4;
        if (weaponStrength < targetToughness) return roll >= 5;
        if (weaponStrength * 2 < targetToughness) return false; // Impossible roll to wound
        return roll == 6; // Minimum roll for low strength weapons
    }

    // Public method to check if a weapon is used
    public bool IsWeaponUsed(WeaponController weapon)
    {
        return usedWeapons.Contains(weapon);
    }

    // Reset used weapons at the end of the round
    public void ResetUsedWeapons()
    {
        usedWeapons.Clear();
    }
}

