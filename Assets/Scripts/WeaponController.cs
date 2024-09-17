using UnityEngine;

public class WeaponController : MonoBehaviour
{
    public string weaponName; // Name of the weapon
    public float range; // Range of the weapon (converted using the conversion factor)
    public int numberOfShots; // Number of shots the weapon fires
    public int strength; // Strength of the weapon
    public int armourPiercing; // Armour piercing value (negative or zero)
    public int damage; // Damage value

    // Constructor to initialize the weapon attributes
    public WeaponController(string weaponName, float range, int numberOfShots, int strength, int armourPiercing, int damage)
    {
        this.weaponName = weaponName;
        this.range = range * GameConstants.MOVEMENT_CONVERSION_FACTOR; // Convert range using the shared factor
        this.numberOfShots = numberOfShots;
        this.strength = strength;
        this.armourPiercing = armourPiercing;
        this.damage = damage;
    }

    // Method to get weapon details for debug
    public string GetWeaponDetails()
    {
        return $"Weapon: {weaponName}, Range: {range}, Shots: {numberOfShots}, Strength: {strength}, AP: {armourPiercing}, Damage: {damage}";
    }
}

