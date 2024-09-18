using UnityEngine;

public class WeaponController : MonoBehaviour
{
    public string weaponName; // Name of the weapon
    public float range; // Range of the weapon (in inches, to be converted in Start())
    public int numberOfShots; // Number of shots the weapon fires
    public int strength; // Strength of the weapon
    public int armourPiercing; // Armour piercing value (negative or zero)
    public int damage; // Damage value

    // Removed the constructor since it's not used by MonoBehaviour

    void Start()
    {
        // Convert range from inches to Unity units using the conversion factor
        range = range * GameConstants.MOVEMENT_CONVERSION_FACTOR;
    }

    // Method to get weapon details for debug
    public string GetWeaponDetails()
    {
        return $"Weapon: {weaponName}, Range: {range}, Shots: {numberOfShots}, Strength: {strength}, AP: {armourPiercing}, Damage: {damage}";
    }
}

