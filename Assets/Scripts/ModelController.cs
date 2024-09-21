// ModelController.cs
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
    public int initiative = 3; // Initiative attribute (1 to 10)
    public Faction faction; // Faction of the model

    [Header("Shooting Attributes")]
    public int ballisticSkill; // Ballistic Skill for shooting (1 to 6)
    public int toughness; // Toughness of the model
    public int armourSave; // Armour save value
    public int invulnerabilitySave; // Invulnerability save value
    public int wounds; // Current wounds of the model

    private float initialMovement; // Initial movement value
    private float initialMarchMovement; // Initial march movement value
    private float remainingMovement; // Remaining movement allowed in the current phase
    private float remainingMarchMovement; // Remaining march movement allowed
    private Vector3 startPosition; // Original position at the start of the phase

    private bool isSelected = false;
    private bool hasMoved = false; // Tracks if the model has moved during the Movement phase
    private bool hasMarched = false; // Tracks if the model has marched during the Movement phase
    private bool hasCharged = false; // Tracks if the model has charged this round

    private NavMeshAgent agent; // Reference to the NavMeshAgent component
    private Outline outline; // Reference to the Outline component
    private GameObject rangeIndicator; // Movement range indicator
    private GameObject marchRangeIndicator; // March range indicator
    private GameObject chargeRangeIndicator; // Charge range indicator
    private GameObject moveIndicator; // Indicator for original position after the first move

    private List<WeaponController> weapons = new List<WeaponController>(); // Weapons the model possesses

    public const float ROTATION_SPEED = 6400f; // Rotation speed constant

    private const float MOVE_SPEED = 10000f; // Movement speed constant

    void Start()
    {
        outline = GetComponent<Outline>();
        outline.enabled = false; // Disable outline by default

        // Initialize movement
        initialMovement = movementRange * GameConstants.MOVEMENT_CONVERSION_FACTOR; // Store initial movement
        remainingMovement = initialMovement; // Initialize remaining movement
        initialMarchMovement = (movementRange + initiative) * GameConstants.MOVEMENT_CONVERSION_FACTOR; // Store initial march movement
        remainingMarchMovement = initialMarchMovement; // Initialize remaining march movement

        startPosition = transform.position; // Store starting position at the start of the phase

        UpdateColors(); // Set initial colors based on faction
        InitializeMovementIndicators();
        HideMovementRange(); // Ensure indicators are hidden initially

        Debug.Log($"Model {gameObject.name} initialized with Movement: {movementRange}, Initiative: {initiative}");
    }

    void InitializeMovementIndicators()
    {
        // Create movement range indicator
        rangeIndicator = Instantiate(GameController.Instance.movementRangeIndicatorPrefab, transform.position + Vector3.up * 60f, Quaternion.identity);
        rangeIndicator.transform.localScale = new Vector3(remainingMovement * 2, 0.01f, remainingMovement * 2);
        Renderer rangeRenderer = rangeIndicator.GetComponent<Renderer>();
        Color factionColor = GetFactionColor();
        rangeRenderer.material.color = new Color(factionColor.r, factionColor.g, factionColor.b, 0.3f);
        rangeIndicator.SetActive(false); // Hide initially

        // Create march range indicator
        marchRangeIndicator = Instantiate(GameController.Instance.marchRangeIndicatorPrefab, transform.position + Vector3.up * 60f, Quaternion.identity);
        marchRangeIndicator.transform.localScale = new Vector3(remainingMarchMovement * 2, 0.01f, remainingMarchMovement * 2);
        Renderer marchRenderer = marchRangeIndicator.GetComponent<Renderer>();
        Color darkerColor = factionColor * 0.7f;
        marchRenderer.material.color = new Color(darkerColor.r, darkerColor.g, darkerColor.b, 0.3f);
        marchRangeIndicator.SetActive(false); // Hide initially

        // Create charge range indicator
        chargeRangeIndicator = Instantiate(GameController.Instance.chargeRangeIndicatorPrefab, transform.position + Vector3.up * 60f, Quaternion.identity);
        chargeRangeIndicator.transform.localScale = new Vector3(0, 0.01f, 0); // Initially hidden
        Renderer chargeRenderer = chargeRangeIndicator.GetComponent<Renderer>();
        chargeRenderer.material.color = new Color(factionColor.r, factionColor.g, factionColor.b, 0.3f);
        chargeRangeIndicator.SetActive(false); // Hide initially

        Debug.Log($"Movement Indicators initialized for {gameObject.name}");
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
        Debug.Log($"Model {gameObject.name} destroyed and removed from player lists.");
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
    /// Also deletes the move and charge indicators.
    /// </summary>
    public void DeselectModel()
    {
        isSelected = false;
        outline.enabled = false; // Disable outline when deselected
        HideMovementRange(); // Hide the movement range when the model is deselected
        DeleteMoveIndicator(); // Delete move indicator when deselected
        DeleteChargeIndicator(); // Delete charge indicator when deselected
        Debug.Log("Model deselected: " + gameObject.name);
    }

    /// <summary>
    /// Displays the movement, march, and charge range indicators based on the current phase.
    /// </summary>
    public void ShowMovementRange()
    {
        GameController.Phase currentPhase = GameController.Instance.GetCurrentPhase();
        if (currentPhase == GameController.Phase.Movement)
        {
            if (rangeIndicator != null)
            {
                rangeIndicator.SetActive(true);
                rangeIndicator.transform.position = new Vector3(transform.position.x, 60f, transform.position.z);
                rangeIndicator.transform.localScale = new Vector3(remainingMovement * 2, 0.01f, remainingMovement * 2);
                Debug.Log($"Movement range indicator shown for {gameObject.name}.");
            }

            if (marchRangeIndicator != null)
            {
                marchRangeIndicator.SetActive(true);
                marchRangeIndicator.transform.position = new Vector3(transform.position.x, 60f, transform.position.z);
                marchRangeIndicator.transform.localScale = new Vector3(remainingMarchMovement * 2, 0.01f, remainingMarchMovement * 2);
                Debug.Log($"March range indicator shown for {gameObject.name}.");
            }
        }
        else if (currentPhase == GameController.Phase.Charge)
        {
            if (!hasMarched && !hasCharged)
            {
                if (chargeRangeIndicator != null)
                {
                    float maxChargeRange = (movementRange + 6) * GameConstants.MOVEMENT_CONVERSION_FACTOR;
                    chargeRangeIndicator.SetActive(true);
                    chargeRangeIndicator.transform.position = new Vector3(transform.position.x, 60f, transform.position.z);
                    chargeRangeIndicator.transform.localScale = new Vector3(maxChargeRange * 2, 0.01f, maxChargeRange * 2);
                    Debug.Log($"Charge range indicator shown for {gameObject.name}.");
                }
            }
        }

        UpdateColors(); // Ensure the color matches the faction when shown
        Debug.Log($"Movement range shown for {gameObject.name}");
    }

    /// <summary>
    /// Hides the movement, march, and charge range indicators.
    /// </summary>
    private void HideMovementRange()
    {
        if (rangeIndicator != null)
        {
            rangeIndicator.SetActive(false); // Deactivate the range indicator
            Debug.Log($"Movement range indicator hidden for {gameObject.name}.");
        }
        if (marchRangeIndicator != null)
        {
            marchRangeIndicator.SetActive(false); // Deactivate the march range indicator
            Debug.Log($"March range indicator hidden for {gameObject.name}.");
        }
        if (chargeRangeIndicator != null)
        {
            chargeRangeIndicator.SetActive(false); // Deactivate the charge range indicator
            Debug.Log($"Charge range indicator hidden for {gameObject.name}.");
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
            Color factionColor = GetFactionColor();

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
            Debug.Log($"Move indicator created for {gameObject.name} at {initialPosition}");
        }
    }

    /// <summary>
    /// Moves the model to the specified target position.
    /// </summary>
    /// <param name="targetPosition">The position to move the model to.</param>
    /// <param name="newDistance">New distance from the starting position after the move.</param>
    public void MoveTo(Vector3 targetPosition, float newDistance)
    {
        // Ensure Y remains constant
        targetPosition.y = transform.position.y;

        Debug.Log($"Initiating MoveTo coroutine for {gameObject.name} to {targetPosition}.");

        StartCoroutine(MoveToPosition(targetPosition)); // Move the model with a coroutine

        if (moveIndicator == null)
        {
            CreateMoveIndicator(startPosition); // Create the move indicator at the original position
        }

        UpdateMovementRangeIndicator(); // Update the movement range cylinder
        Debug.Log($"Model {gameObject.name} moved to {targetPosition}. New Distance from Start: {newDistance}");
    }

    /// <summary>
    /// Coroutine to smoothly move the model to the target position.
    /// </summary>
    /// <param name="targetPosition">The target position to move to.</param>
    /// <returns>IEnumerator for the coroutine.</returns>
    private System.Collections.IEnumerator MoveToPosition(Vector3 targetPosition)
    {
        Debug.Log($"Starting MoveToPosition coroutine for {gameObject.name}.");

        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            // Move towards the target position while keeping Y constant
            Vector3 newPosition = Vector3.MoveTowards(transform.position, targetPosition, MOVE_SPEED * Time.deltaTime);
            newPosition.y = transform.position.y; // Keep Y unchanged
            transform.position = newPosition;
            Debug.Log($"Model {gameObject.name} moving towards {targetPosition}. Current Position: {transform.position}");
            yield return null; // Wait for the next frame
        }
        transform.position = targetPosition; // Snap to the exact position at the end
        Debug.Log($"Model {gameObject.name} reached target position: {targetPosition}");
    }

    /// <summary>
    /// Updates the movement and march based on the new distance from the starting position.
    /// </summary>
    /// <param name="newDistance">The new distance from the starting position.</param>
    public void UpdateMovement(float newDistance)
    {
        remainingMovement = initialMovement - newDistance;
        remainingMarchMovement = initialMarchMovement - newDistance;

        bool previousHasMoved = hasMoved;
        bool previousHasMarched = hasMarched;

        if (remainingMovement >= 0)
        {
            hasMoved = newDistance > 0;
            if (hasMoved != previousHasMoved)
            {
                Debug.Log($"hasMoved changed to: {hasMoved} for {gameObject.name}");
            }
        }

        if (remainingMarchMovement >= 0)
        {
            hasMarched = newDistance > movementRange * GameConstants.MOVEMENT_CONVERSION_FACTOR;
            if (hasMarched != previousHasMarched)
            {
                Debug.Log($"hasMarched changed to: {hasMarched} for {gameObject.name}");
            }
        }

        // Clamp values to prevent negative remaining movement
        remainingMovement = Mathf.Max(remainingMovement, 0);
        remainingMarchMovement = Mathf.Max(remainingMarchMovement, 0);

        Debug.Log($"UpdateMovement: newDistance={newDistance}, remainingMovement={remainingMovement}, remainingMarchMovement={remainingMarchMovement}, hasMoved={hasMoved}, hasMarched={hasMarched}");
    }

    /// <summary>
    /// Updates the movement and march range indicators' scale and position based on remaining movement.
    /// </summary>
    private void UpdateMovementRangeIndicator()
    {
        if (rangeIndicator != null)
        {
            rangeIndicator.transform.position = new Vector3(transform.position.x, 60f, transform.position.z);
            rangeIndicator.transform.localScale = new Vector3(remainingMovement * 2, 0.01f, remainingMovement * 2);
            Debug.Log($"Updated Movement Range Indicator for {gameObject.name}.");
        }

        if (marchRangeIndicator != null)
        {
            marchRangeIndicator.transform.position = new Vector3(transform.position.x, 60f, transform.position.z);
            marchRangeIndicator.transform.localScale = new Vector3(remainingMarchMovement * 2, 0.01f, remainingMarchMovement * 2);
            Debug.Log($"Updated March Range Indicator for {gameObject.name}.");
        }

        Debug.Log($"Updated Indicators - Remaining Movement: {remainingMovement}, Remaining March: {remainingMarchMovement}");
    }

    /// <summary>
    /// Updates the colors of the outline, movement range indicator, and move indicator based on faction.
    /// </summary>
    private void UpdateColors()
    {
        Color factionColor = GetFactionColor();

        outline.OutlineColor = factionColor;

        if (rangeIndicator != null)
        {
            Renderer rangeRenderer = rangeIndicator.GetComponent<Renderer>();
            rangeRenderer.material.color = new Color(factionColor.r, factionColor.g, factionColor.b, 0.3f);
        }

        if (marchRangeIndicator != null)
        {
            Renderer marchRenderer = marchRangeIndicator.GetComponent<Renderer>();
            Color darkerColor = factionColor * 0.7f;
            marchRenderer.material.color = new Color(darkerColor.r, darkerColor.g, darkerColor.b, 0.3f);
        }

        if (chargeRangeIndicator != null)
        {
            Renderer chargeRenderer = chargeRangeIndicator.GetComponent<Renderer>();
            chargeRenderer.material.color = new Color(factionColor.r, factionColor.g, factionColor.b, 0.3f);
        }

        if (moveIndicator != null)
        {
            Renderer[] moveIndicatorRenderers = moveIndicator.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in moveIndicatorRenderers)
            {
                renderer.material.color = factionColor;
            }
        }

        Debug.Log($"Colors updated for {gameObject.name}");
    }

    /// <summary>
    /// Gets the faction color.
    /// </summary>
    public Color GetFactionColor()
    {
        return faction == Faction.AdeptusMechanicus ? Color.red : Color.green;
    }

    /// <summary>
    /// Rotates the model based on the input rotation amount.
    /// </summary>
    /// <param name="rotationAmount">The amount to rotate the model.</param>
    public void RotateModel(float rotationAmount)
    {
        float rotationDegrees = rotationAmount * ROTATION_SPEED * Time.deltaTime;
        transform.Rotate(Vector3.up, rotationDegrees);
        Debug.Log($"Model {gameObject.name} rotated by {rotationDegrees} degrees.");
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
    /// Returns the remaining march movement allowed for the model.
    /// </summary>
    /// <returns>Remaining march movement as a float.</returns>
    public float GetRemainingMarchMovement()
    {
        return remainingMarchMovement;
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
            Debug.Log($"Move indicator deleted for {gameObject.name}");
        }
    }

    /// <summary>
    /// Deletes the charge indicator associated with the model.
    /// </summary>
    public void DeleteChargeIndicator()
    {
        if (chargeRangeIndicator != null)
        {
            Destroy(chargeRangeIndicator); // Delete the charge indicator when deselected
            chargeRangeIndicator = null;
            Debug.Log($"Charge range indicator deleted for {gameObject.name}");
        }
    }

    /// <summary>
    /// Adds a weapon to the model's weapon list.
    /// </summary>
    /// <param name="weapon">The WeaponController to add.</param>
    public void AddWeapon(WeaponController weapon)
    {
        weapons.Add(weapon);
        Debug.Log($"Weapon {weapon.weaponName} added to {gameObject.name}");
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
    /// Checks if the model has marched during the Movement phase.
    /// </summary>
    /// <returns>True if the model has marched; otherwise, false.</returns>
    public bool HasMarched()
    {
        return hasMarched;
    }

    /// <summary>
    /// Checks if the model has charged this round.
    /// </summary>
    /// <returns>True if the model has charged; otherwise, false.</returns>
    public bool HasCharged()
    {
        return hasCharged;
    }

    /// <summary>
    /// Resets the model's movement for a new round.
    /// </summary>
    public void ResetMovement()
    {
        remainingMovement = initialMovement; // Reset movement to initial value
        bool previousHasMoved = hasMoved;
        hasMoved = false; // Reset hasMoved flag

        if (hasMoved != previousHasMoved)
        {
            Debug.Log($"hasMoved changed to: {hasMoved} for {gameObject.name}");
        }

        HideMovementRange(); // Hide movement range indicator
        Debug.Log($"Reset Movement - Remaining Movement: {remainingMovement}, Remaining March: {remainingMarchMovement}");
    }

    /// <summary>
    /// Resets the model's march status for a new round.
    /// </summary>
    public void ResetMarch()
    {
        remainingMarchMovement = initialMarchMovement; // Reset march movement
        bool previousHasMarched = hasMarched;
        hasMarched = false; // Reset hasMarched flag

        if (hasMarched != previousHasMarched)
        {
            Debug.Log($"hasMarched changed to: {hasMarched} for {gameObject.name}");
        }

        UpdateMovementRangeIndicator(); // Update indicators
        Debug.Log($"Reset March - Remaining Movement: {remainingMovement}, Remaining March: {remainingMarchMovement}");
    }

    /// <summary>
    /// Resets the model's charge status for a new round.
    /// </summary>
    public void ResetCharge()
    {
        bool previousHasCharged = hasCharged;
        hasCharged = false; // Reset hasCharged flag

        if (hasCharged != previousHasCharged)
        {
            Debug.Log($"hasCharged changed to: {hasCharged} for {gameObject.name}");
        }

        Debug.Log($"Reset Charge - hasCharged: {hasCharged}");
    }

    /// <summary>
    /// Sets the model as having charged.
    /// </summary>
    public void SetCharged()
    {
        hasCharged = true;
        Debug.Log($"Model {gameObject.name} has charged.");
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
            rangeIndicator.transform.position = new Vector3(transform.position.x, 60f, transform.position.z);
        }

        if (marchRangeIndicator != null && marchRangeIndicator.activeSelf)
        {
            marchRangeIndicator.transform.position = new Vector3(transform.position.x, 60f, transform.position.z);
        }

        if (chargeRangeIndicator != null && chargeRangeIndicator.activeSelf)
        {
            chargeRangeIndicator.transform.position = new Vector3(transform.position.x, 60f, transform.position.z);
        }

        Debug.Log($"Updated Start Position to: {startPosition}");
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

    /// <summary>
    /// Checks if this model is colliding with another model.
    /// </summary>
    /// <param name="otherModel">The other model to check collision with.</param>
    /// <returns>True if colliding; otherwise, false.</returns>
    public bool IsColliding(ModelController otherModel)
    {
        Collider colliderA = this.GetComponent<Collider>();
        Collider colliderB = otherModel.GetComponent<Collider>();

        if (colliderA == null || colliderB == null)
        {
            Debug.LogError("One or both models do not have colliders.");
            return false;
        }

        bool isIntersecting = colliderA.bounds.Intersects(colliderB.bounds);
        Debug.Log($"IsColliding between {this.gameObject.name} and {otherModel.gameObject.name}: {isIntersecting}");
        return isIntersecting;
    }
}

