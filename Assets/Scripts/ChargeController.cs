// ChargeController.cs
using UnityEngine;
using System.Collections;

public class ChargeController : MonoBehaviour
{
    public static ChargeController Instance;

    [Header("Charge Settings")]
    public GameObject chargeRangeIndicatorPrefab;

    private GameController gameController;
    private ModelController chargingModel;
    private ModelController chargeTarget;
    private float maxChargeRange;
    private float chargeDistance;
    private float minChargeDistance;
    private GameObject currentChargeIndicator;

    // Enum to track charge phase state
    public enum ChargePhaseState
    {
        None,
        PendingTarget,
        AwaitingMovement
    }

    public ChargePhaseState chargeState = ChargePhaseState.None; // Current state

    // Public properties to access charge distance and target
    public float ChargeDistance
    {
        get { return chargeDistance; }
    }

    public ModelController ChargeTargetModel
    {
        get { return chargeTarget; }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("ChargeController singleton instance initialized.");
        }
        else
        {
            Destroy(gameObject);
            Debug.LogWarning("Duplicate ChargeController instance destroyed.");
        }
    }

    void Start()
    {
        gameController = GameController.Instance;
        if (gameController == null)
        {
            Debug.LogError("GameController instance not found!");
        }
        else
        {
            Debug.Log("ChargeController successfully linked to GameController.");
        }
    }

    /// <summary>
    /// Public method to display the charge range indicator when a model is selected.
    /// </summary>
    /// <param name="model">The model that is selected for charging.</param>
    public void DisplayChargeRangeIndicator(ModelController model)
    {
        if (model == null)
        {
            Debug.LogError("Cannot display charge range indicator. Model is null.");
            return;
        }

        chargingModel = model;
        maxChargeRange = (chargingModel.movementRange + 6) * GameConstants.MOVEMENT_CONVERSION_FACTOR;
        Debug.Log($"Maximum Charge Range for {chargingModel.gameObject.name}: {maxChargeRange}");

        if (chargeRangeIndicatorPrefab != null && chargingModel != null)
        {
            Vector3 indicatorPosition = new Vector3(chargingModel.transform.position.x, 60f, chargingModel.transform.position.z);
            currentChargeIndicator = Instantiate(chargeRangeIndicatorPrefab, indicatorPosition, Quaternion.identity);
            currentChargeIndicator.transform.localScale = new Vector3(maxChargeRange * 2, 0.01f, maxChargeRange * 2);
            Renderer renderer = currentChargeIndicator.GetComponent<Renderer>();
            renderer.material.color = chargingModel.GetFactionColor();
            renderer.material.color = new Color(renderer.material.color.r, renderer.material.color.g, renderer.material.color.b, 0.3f);
            currentChargeIndicator.name = "ChargeRangeIndicator";
            Debug.Log($"Charge range indicator instantiated at position: {indicatorPosition} for {chargingModel.gameObject.name}");
        }
        else
        {
            Debug.LogError("ChargeRangeIndicatorPrefab is not assigned or chargingModel is null.");
        }

        chargeState = ChargePhaseState.PendingTarget; // Update state to PendingTarget
    }

    /// <summary>
    /// Public method to hide the charge range indicator when a model is deselected.
    /// </summary>
    public void HideChargeRangeIndicator()
    {
        if (currentChargeIndicator != null)
        {
            Destroy(currentChargeIndicator);
            Debug.Log("Charge range indicator destroyed.");
            currentChargeIndicator = null;
        }
        chargingModel = null;
        chargeTarget = null;
        chargeState = ChargePhaseState.None; // Reset state
    }

    /// <summary>
    /// Initiates the charge process for the selected model.
    /// </summary>
    /// <param name="targetModel">The target model to charge.</param>
    public void SelectChargeTarget(ModelController targetModel)
    {
        Debug.Log("SelectChargeTarget called.");
        if (gameController.GetCurrentPhase() != GameController.Phase.Charge)
        {
            Debug.LogError("Charge can only be initiated during the Charge phase.");
            return;
        }

        // Check if the selected model can charge
        ModelController model = gameController.SelectedModel;
        if (model == null)
        {
            Debug.LogError("No model is currently selected for charging.");
            gameController.ShowPlayerErrorMessage("No model is currently selected for charging.");
            return;
        }

        if (model.HasMarched())
        {
            gameController.ShowPlayerErrorMessage("Models that have marched cannot charge!");
            Debug.Log("Selected model has marched and cannot charge.");
            return;
        }

        if (model.HasCharged())
        {
            gameController.ShowPlayerErrorMessage("This model has already attempted a charge this round!");
            Debug.Log("Selected model has already charged this round.");
            return;
        }

        // Ensure the target is an enemy model
        if (targetModel.playerID == model.playerID)
        {
            gameController.ShowPlayerErrorMessage("Invalid charge target! You can only charge enemy models.");
            Debug.Log("Charge target is not an enemy model.");
            return;
        }

        chargingModel = model;
        chargeTarget = targetModel;

        maxChargeRange = (chargingModel.movementRange + 6) * GameConstants.MOVEMENT_CONVERSION_FACTOR;
        Debug.Log($"Maximum Charge Range for {chargingModel.gameObject.name}: {maxChargeRange}");

        // Calculate minimum distance between colliders
        minChargeDistance = CalculateMinimumDistance(chargingModel, chargeTarget);
        Debug.Log($"Minimum Charge Distance between {chargingModel.gameObject.name} and {chargeTarget.gameObject.name}: {minChargeDistance}");

        if (minChargeDistance > maxChargeRange)
        {
            gameController.ShowPlayerErrorMessage("Target is outside of maximum charge range!");
            Debug.Log("Charge target is outside of maximum charge range.");
            DestroyChargeRangeIndicator();
            chargingModel = null;
            chargeTarget = null;
            return;
        }

        // Roll for charge distance
        int diceRoll = DiceRoller.RollD6();
        chargeDistance = (chargingModel.movementRange + diceRoll) * GameConstants.MOVEMENT_CONVERSION_FACTOR;
        Debug.Log($"Charge Roll: {diceRoll}, Charge Distance: {chargeDistance}");

        if (chargeDistance < minChargeDistance)
        {
            // Charge failed - perform surge move
            gameController.ShowPlayerErrorMessage("Charge failed! Performing surge move.");
            Debug.Log("Charge has failed.");
            PerformSurgeMove();
        }
        else
        {
            // Charge succeeded - update charge range indicator to current charge range
            Debug.Log("Charge has succeeded. Updating charge range indicator.");
            UpdateChargeRangeIndicator(chargeDistance);

            gameController.ShowPlayerErrorMessage("Charge succeeded! Click on a valid location to move and collide with the target.");
            Debug.Log("Charge has succeeded. Awaiting player movement.");

            // Update state to AwaitingMovement
            chargeState = ChargePhaseState.AwaitingMovement;
        }

        // Do NOT mark as charged here. Only mark as charged after successful movement.
    }

    /// <summary>
    /// Updates the charge range indicator based on the current charge distance.
    /// </summary>
    /// <param name="currentChargeDistance">The current charge distance.</param>
    private void UpdateChargeRangeIndicator(float currentChargeDistance)
    {
        if (currentChargeIndicator != null)
        {
            currentChargeIndicator.transform.localScale = new Vector3(currentChargeDistance * 2, 0.01f, currentChargeDistance * 2);
            Debug.Log($"Charge range indicator updated to new charge distance: {currentChargeDistance}");
        }
    }

    /// <summary>
    /// Performs a surge move (chargeDistance / 2 towards the target).
    /// </summary>
    public void PerformSurgeMove()
    {
        if (chargingModel == null || chargeTarget == null)
        {
            Debug.LogError("Cannot perform surge move. Charging model or target is null.");
            return;
        }

        float surgeMoveDistance = chargeDistance / 2;
        Vector3 direction = (chargeTarget.transform.position - chargingModel.transform.position).normalized;
        Vector3 surgeTarget = chargingModel.transform.position + direction * surgeMoveDistance;

        Debug.Log($"Performing surge move for {chargingModel.gameObject.name} towards {chargeTarget.gameObject.name} by {surgeMoveDistance} units.");
        chargingModel.MoveTo(surgeTarget, Vector3.Distance(new Vector3(chargingModel.transform.position.x, 0, chargingModel.transform.position.z),
                                                             new Vector3(surgeTarget.x, 0, surgeTarget.z)));

        chargingModel.SetCharged(); // Ensure the model is marked as having charged
        DestroyChargeRangeIndicator();
        gameController.DeselectAllModels(); // Deselect the model after surge
        chargingModel = null;
        chargeTarget = null;
        chargeState = ChargePhaseState.None; // Reset state
        Debug.Log("Surge move completed.");
    }

    /// <summary>
    /// Checks if the charging model has collided with the charge target.
    /// </summary>
    public void CheckChargeCollision()
    {
        if (chargingModel == null || chargeTarget == null)
        {
            Debug.LogError("Charge collision check called without valid charging model or target.");
            return;
        }

        bool isColliding = chargingModel.IsColliding(chargeTarget);
        Debug.Log($"Collision Check: {isColliding}");

        if (isColliding)
        {
            Debug.Log("Charge collision successful.");
            gameController.ShowPlayerErrorMessage("Charge successful! You can move the model to collide with the target.");
            gameController.EnableEndTurnButton();

            // Mark as charged
            chargingModel.SetCharged();

            // Reset charge state
            chargeState = ChargePhaseState.None;
        }
        else
        {
            Debug.Log("Charge collision failed.");
            gameController.ShowPlayerErrorMessage("Charge did not collide with the target. Performing surge move.");
            PerformSurgeMove(); // Automatically perform surge move
        }
    }

    /// <summary>
    /// Calculates the minimum distance required for two models to collide based on their colliders.
    /// </summary>
    /// <param name="modelA">First model.</param>
    /// <param name="modelB">Second model.</param>
    /// <returns>Minimum collision distance.</returns>
    private float CalculateMinimumDistance(ModelController modelA, ModelController modelB)
    {
        Collider colliderA = modelA.GetComponent<Collider>();
        Collider colliderB = modelB.GetComponent<Collider>();

        if (colliderA == null || colliderB == null)
        {
            Debug.LogError("One or both models do not have colliders.");
            return Mathf.Infinity;
        }

        Vector3 closestPointA = colliderA.ClosestPoint(modelB.transform.position);
        Vector3 closestPointB = colliderB.ClosestPoint(modelA.transform.position);
        float distance = Vector3.Distance(closestPointA, closestPointB);
        Debug.Log($"Minimum Charge Distance calculated: {distance}");
        return distance;
    }

    /// <summary>
    /// Destroys the charge range indicator.
    /// </summary>
    private void DestroyChargeRangeIndicator()
    {
        if (currentChargeIndicator != null)
        {
            Destroy(currentChargeIndicator);
            Debug.Log("Charge range indicator destroyed.");
            currentChargeIndicator = null;
        }
        chargeState = ChargePhaseState.None; // Reset state
    }
}

