using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

[RequireComponent(typeof(Outline))]
public class ModelController : MonoBehaviour
{
    public enum Faction
    {
        AdeptusMechanicus,
        DeathGuard
    }

    [Header("Model Attributes")]
    public int playerID; // ID of the player who owns this model (1 or 2)
    public float movementRange = 6f; // Maximum distance the model can move in a turn
    public Faction faction; // Faction of the model

    [Header("Shooting Attributes")]
    public int ballisticSkill; // Ballistic Skill for shooting (1 to 6)
    public int toughness; // Toughness of the model
    public int armourSave; // Armour save value
    public int invulnerabilitySave; // Invulnerability save value
    public int wounds; // Current wounds of the model

    private float initialMovement; // Store the initial movement value
    private float remainingMovement; // Remaining movement allowed in the current phase
    private Vector3 startPosition; // The original position at the start of the phase

    private bool isSelected = false; // Whether this model is currently selected
    private bool hasMoved = false; // Track if this model has moved during the Movement phase

    private NavMeshAgent agent; // Reference to the NavMeshAgent component
    private Outline outline; // Reference to the Outline component
    private GameObject rangeIndicator; // Reference to the movement range visual indicator
    private GameObject moveIndicator; // Visual indicator for the original position after the first move

    private List<WeaponController> weapons = new List<WeaponController>(); // Weapons the model has

    private const float MOVE_SPEED = 10000f; // Movement speed is a constant for all objects
    private const float ROTATION_SPEED = 6400f; // Rotation speed (updated for faster rotation)

    void Start()
    {
        outline = GetComponent<Outline>();
        outline.enabled = false; // Disable outline by default

        // Initialize movement
        initialMovement = movementRange * GameConstants.MOVEMENT_CONVERSION_FACTOR; // Store initial movement
        remainingMovement = initialMovement; // Initialize remaining movement

        startPosition = transform.position; // Store the starting position at the start of the phase

        UpdateColors(); // Set initial colors based on faction
    }

    /// <summary>
    /// Called when the object is destroyed. Notifies the GameController to remove this model from player lists.
    /// </summary>
    void OnDestroy()
    {
        // Notify GameController to remove this model from the lists
        if (GameController.Instance != null)
        {
            GameController.Instance.RemoveModel(this);
        }
    }

    /// <summary>
    /// Selects the model, enabling the outline and showing the movement range.
    /// </summary>
    public void SelectModel()
    {
        isSelected = true;
        outline.enabled = true; // Enable outline when selected
        ShowMovementRange(); // Show the movement range when the model is selected
        Debug.Log("Model selected: " + gameObject.name);
    }

    /// <summary>
    /// Deselects the model, disabling the outline and hiding the movement range.
    /// </summary>
    public void DeselectModel()
    {
        isSelected = false;
        outline.enabled = false; // Disable outline when deselected
        HideMovementRange(); // Hide the movement range when the model is deselected
        Debug.Log("Model deselected: " + gameObject.name);
    }

    /// <summary>
    /// Displays the movement range indicator based on the current remaining movement.
    /// </summary>
    private void ShowMovementRange()
    {
        // Show movement range only during the Movement Phase
        if (GameController.Instance.GetCurrentPhase() != GameController.Phase.Movement)
        {
            return;
        }

        if (rangeIndicator == null)
        {
            // Create the movement range indicator
            rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rangeIndicator.transform.position = new Vector3(transform.position.x, 58.0f, transform.position.z); // Set height to 58

            float radius = remainingMovement; // Use current remaining movement
            rangeIndicator.transform.localScale = new Vector3(radius * 2, 0.01f, radius * 2); // Scale based on current remaining movement

            // Create and assign a transparent material with faction color
            Material transparentMaterial = new Material(Shader.Find("Standard"));
            Color factionColor = faction == Faction.AdeptusMechanicus ? Color.red : Color.green;
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
            // Update the scale based on the current remaining movement
            float radius = remainingMovement;
            rangeIndicator.transform.localScale = new Vector3(radius * 2, 0.01f, radius * 2);
            rangeIndicator.transform.position = new Vector3(transform.position.x, 58.0f, transform.position.z);
            rangeIndicator.SetActive(true); // Reactivate if already created
        }

        UpdateColors(); // Ensure the color matches the faction when shown
    }

    /// <summary>
    /// Hides the movement range indicator.
    /// </summary>
    private void HideMovementRange()
    {
        if (rangeIndicator != null)
        {
            rangeIndicator.SetActive(false); // Deactivate the range indicator
        }
    }

    /// <summary>
    /// Creates a move indicator at the initial position when the model moves.
    /// </summary>
    /// <param name="initialPosition">The initial position before moving.</param>
    private void CreateMoveIndicator(Vector3 initialPosition)
    {
        if (moveIndicator == null)
        {
            moveIndicator = Instantiate(gameObject, initialPosition, Quaternion.identity);
            Renderer[] renderers = moveIndicator.GetComponentsInChildren<Renderer>();
            Color factionColor = faction == Faction.AdeptusMechanicus ? Color.red : Color.green;

            foreach (Renderer renderer in renderers)
            {
                Material transparentMaterial = new Material(Shader.Find("Standard"));
                transparentMaterial.color = new Color(factionColor.r, factionColor.g, factionColor.b, 0.3f); // Set the color with transparency
                transparentMaterial.SetFloat("_Mode", 3); // Set rendering mode to Transparent
                transparentMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                transparentMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                transparentMaterial.SetInt("_ZWrite", 0);
                transparentMaterial.DisableKeyword("_ALPHATEST_ON");
                transparentMaterial.EnableKeyword("_ALPHABLEND_ON");
                transparentMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                transparentMaterial.renderQueue = 3000;

                renderer.material = transparentMaterial; // Assign the transparent material
            }
            Destroy(moveIndicator.GetComponent<NavMeshAgent>()); // Remove unnecessary components
            Destroy(moveIndicator.GetComponent<ModelController>()); // Remove unnecessary components
        }
    }

    /// <summary>
    /// Moves the model to the specified target position.
    /// </summary>
    /// <param name="targetPosition">The position to move the model to.</param>
    public void MoveTo(Vector3 targetPosition)
    {
        targetPosition.y = transform.position.y; // Ensure movement stays on the XZ plane

        // Calculate distance between current position and target position on the XZ plane
        float distance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                          new Vector3(targetPosition.x, 0, targetPosition.z));

        // Calculate distance from start position to current position and to the target position
        float distanceToStart = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                                 new Vector3(startPosition.x, 0, startPosition.z));
        float newDistanceToStart = Vector3.Distance(new Vector3(targetPosition.x, 0, targetPosition.z), 
                                                    new Vector3(startPosition.x, 0, startPosition.z));

        // Calculate potential remaining movement based on moving closer to the start position
        float potentialRemainingMovement = remainingMovement;

        if (newDistanceToStart < distanceToStart)
        {
            float distanceDiff = distanceToStart - newDistanceToStart;
            potentialRemainingMovement = Mathf.Min(potentialRemainingMovement + distanceDiff, initialMovement);
        }

        if (distance <= potentialRemainingMovement) // Check against potential remaining movement
        {
            if (newDistanceToStart < distanceToStart)
            {
                float distanceDiff = distanceToStart - newDistanceToStart;
                remainingMovement = Mathf.Min(remainingMovement + distanceDiff, initialMovement);
            }
            else
            {
                remainingMovement -= distance; // Deduct the distance from remaining movement
            }

            StartCoroutine(MoveToPosition(targetPosition)); // Move the model with a coroutine

            if (moveIndicator == null)
            {
                CreateMoveIndicator(startPosition); // Create the move indicator at the original position
            }

            UpdateMovementRangeIndicator(); // Update the movement range cylinder
            hasMoved = true; // Mark that this model has moved in the Movement phase
        }
        else
        {
            Debug.Log("Move exceeds remaining movement range.");
        }
    }

    /// <summary>
    /// Coroutine to smoothly move the model to the target position.
    /// </summary>
    /// <param name="targetPosition">The target position to move to.</param>
    /// <returns>IEnumerator for the coroutine.</returns>
    private System.Collections.IEnumerator MoveToPosition(Vector3 targetPosition)
    {
        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, MOVE_SPEED * Time.deltaTime);
            yield return null; // Wait for the next frame
        }
        transform.position = targetPosition; // Snap to the exact position at the end
    }

    /// <summary>
    /// Updates the movement range indicator's scale and position based on remaining movement.
    /// </summary>
    private void UpdateMovementRangeIndicator()
    {
        if (rangeIndicator != null)
        {
            float radius = remainingMovement; // Use current remaining movement
            rangeIndicator.transform.position = new Vector3(transform.position.x, 58.0f, transform.position.z); // Set height to 58
            rangeIndicator.transform.localScale = new Vector3(radius * 2, 0.01f, radius * 2); // Update the size
        }
    }

    /// <summary>
    /// Updates the colors of the outline, movement range indicator, and move indicator based on faction.
    /// </summary>
    private void UpdateColors()
    {
        Color factionColor = faction == Faction.AdeptusMechanicus ? Color.red : Color.green;

        outline.OutlineColor = factionColor;

        if (rangeIndicator != null)
        {
            Renderer rangeRenderer = rangeIndicator.GetComponent<Renderer>();
            rangeRenderer.material.color = new Color(factionColor.r, factionColor.g, factionColor.b, 0.3f);
        }

        if (moveIndicator != null)
        {
            Renderer[] moveIndicatorRenderers = moveIndicator.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in moveIndicatorRenderers)
            {
                renderer.material.color = factionColor;
            }
        }
    }

    /// <summary>
    /// Rotates the model based on the input rotation amount.
    /// </summary>
    /// <param name="rotationAmount">The amount to rotate the model.</param>
    public void RotateModel(float rotationAmount)
    {
        transform.Rotate(Vector3.up, rotationAmount * ROTATION_SPEED * Time.deltaTime);
    }

    /// <summary>
    /// Returns the remaining movement allowed for the model.
    /// </summary>
    /// <returns>Remaining movement as a float.</returns>
    public float GetRemainingMovement()
    {
        return remainingMovement;
    }

    /// <summary>
    /// Returns the starting position of the model.
    /// </summary>
    /// <returns>Starting position as a Vector3.</returns>
    public Vector3 GetStartPosition()
    {
        return startPosition;
    }

    /// <summary>
    /// Deletes the move indicator associated with the model.
    /// </summary>
    public void DeleteMoveIndicator()
    {
        if (moveIndicator != null)
        {
            Destroy(moveIndicator); // Delete the move indicator at the end of the phase
            moveIndicator = null;
        }
    }

    /// <summary>
    /// Adds a weapon to the model's weapon list.
    /// </summary>
    /// <param name="weapon">The WeaponController to add.</param>
    public void AddWeapon(WeaponController weapon)
    {
        weapons.Add(weapon);
    }

    /// <summary>
    /// Checks if the model has moved during the Movement phase.
    /// </summary>
    /// <returns>True if the model has moved; otherwise, false.</returns>
    public bool HasMoved()
    {
        return hasMoved;
    }

    /// <summary>
    /// Resets the model's movement for a new round.
    /// </summary>
    public void ResetMovement()
    {
        remainingMovement = initialMovement; // Reset movement to initial value
        hasMoved = false; // Reset hasMoved flag
        HideMovementRange(); // Hide movement range indicator

        // Ensure the range indicator reflects the reset remainingMovement if it's active
        if (rangeIndicator != null && rangeIndicator.activeSelf)
        {
            rangeIndicator.transform.localScale = new Vector3(remainingMovement * 2, 0.01f, remainingMovement * 2);
        }
    }

    /// <summary>
    /// Updates the model's starting position to its current position.
    /// </summary>
    public void UpdateStartPosition()
    {
        startPosition = transform.position; // Update start position to current position

        // Update the movement range indicator's position if it's active
        if (rangeIndicator != null && rangeIndicator.activeSelf)
        {
            rangeIndicator.transform.position = new Vector3(transform.position.x, 58.0f, transform.position.z);
        }
    }

    /// <summary>
    /// Checks if the model is destroyed based on its wounds.
    /// </summary>
    /// <returns>True if the model is destroyed; otherwise, false.</returns>
    public bool IsDestroyed()
    {
        return wounds <= 0;
    }

    /// <summary>
    /// Applies damage to the model and destroys it if wounds reach zero.
    /// </summary>
    /// <param name="damage">The amount of damage to apply.</param>
    public void TakeDamage(int damage)
    {
        wounds -= damage;
        Debug.Log($"Model {gameObject.name} took {damage} damage. Remaining wounds: {wounds}");

        if (wounds <= 0)
        {
            Debug.Log($"Model {gameObject.name} is destroyed!");
            Destroy(gameObject); // Remove model from the game
        }
    }
}

