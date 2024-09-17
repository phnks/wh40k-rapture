using UnityEngine;

public static class DiceRoller
{
    // Roll a six-sided dice
    public static int RollD6()
    {
        return Random.Range(1, 7);
    }
}

