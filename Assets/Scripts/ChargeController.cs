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

    // Public property to access chargeDistance
    public float ChargeDistance
    {
        get { return chargeDistance; }
    }

    // Public property to access chargeTarget
    public ModelController ChargeTarget
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

        chargingModel = model;
        chargeTarget = targetModel;

        maxChargeRange = (chargingModel.movementRange + 6) * GameConstants.MOVEMENT_CONVERSION_FACTOR;
        Debug.Log($"Maximum Charge Range for {chargingModel.gameObject.name}: {maxChargeRange}");

        DisplayChargeRangeIndicator();

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
            // Charge succeeded - allow player to move
            gameController.ShowPlayerErrorMessage("Charge succeeded! Move towards the target and collide.");
            Debug.Log("Charge has succeeded.");
            // The player can now move the model manually within charge distance
        }

        chargingModel.SetCharged(); // Mark as charged
        Debug.Log($"Model {chargingModel.gameObject.name} has been marked as charged.");
    }

    /// <summary>
    /// Displays the charge range indicator.
    /// </summary>
    private void DisplayChargeRangeIndicator()
    {
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
    }

    /// <summary>
    /// Performs a surge move (chargeDistance / 2 towards the target).
    /// </summary>
    private void PerformSurgeMove()
    {
        float surgeMoveDistance = chargeDistance / 2;
        Vector3 direction = (chargeTarget.transform.position - chargingModel.transform.position).normalized;
        Vector3 surgeTarget = chargingModel.transform.position + direction * surgeMoveDistance;

        Debug.Log($"Performing surge move for {chargingModel.gameObject.name} towards {chargeTarget.gameObject.name} by {surgeMoveDistance} units.");
        chargingModel.MoveTo(surgeTarget, Vector3.Distance(new Vector3(chargingModel.transform.position.x, 0, chargingModel.transform.position.z),
                                                             new Vector3(surgeTarget.x, 0, surgeTarget.z)));

        DestroyChargeRangeIndicator();
        chargingModel = null;
        chargeTarget = null;
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
            gameController.ShowPlayerErrorMessage("Charge successful! End your turn.");
            gameController.EnableEndTurnButton();
        }
        else
        {
            Debug.Log("Charge collision failed.");
            gameController.ShowPlayerErrorMessage("Charge did not collide with the target. Please attempt to collide.");
            gameController.DisableEndTurnButton();
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
}

