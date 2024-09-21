// GameController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    public enum Phase
    {
        Movement,
        FirstFire,
        Charge,
        Fight,
        AdvanceFire
    }

    public static GameController Instance; // Singleton instance

    [Header("UI Elements")]
    public TextMeshProUGUI roundText;  
    public TextMeshProUGUI phaseText;  
    public TextMeshProUGUI playerText;  
    public TextMeshProUGUI playerErrorText; // Reference to the Player Error Text UI

    [Header("Movement Indicators")]
    public GameObject movementRangeIndicatorPrefab;
    public GameObject marchRangeIndicatorPrefab;
    public GameObject chargeRangeIndicatorPrefab; // Ensure this is assigned

    [Header("Buttons and Panels")]
    public Button endTurnButton; // Reference to the End Turn button
    public GameObject weaponUIPanel; // Reference to the UI panel displaying weapons
    public WeaponUIController weaponUIController; // Reference to the Weapon UI controller

    [Header("Controllers")]
    public ChargeController chargeController; // Reference to the ChargeController

    private int currentRound = 1;       
    private Phase currentPhase;         
    private int currentPlayer = 1;      

    private int totalPlayers = 2;       

    private List<GameObject> player1Models = new List<GameObject>();  // List of Player 1's models
    private List<GameObject> player2Models = new List<GameObject>();  // List of Player 2's models

    private ModelController selectedModel; // Reference to the currently selected model
    private ShootingController shootingController; // Reference to the Shooting Controller

    private const float RAYCAST_RANGE = 2000f; // Increased raycast range for selection

    /// <summary>
    /// Public getter for selectedModel
    /// </summary>
    public ModelController SelectedModel
    {
        get { return selectedModel; }
    }

    void Awake()
    {
        // Set up the singleton instance
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("GameController singleton instance initialized.");
        }
        else
        {
            Destroy(gameObject);
            Debug.LogWarning("Duplicate GameController instance destroyed.");
        }

        // Ensure ChargeController is assigned
        if (chargeController == null)
        {
            chargeController = GetComponent<ChargeController>();
            if (chargeController == null)
            {
                Debug.LogError("ChargeController component is missing on GameController!");
            }
            else
            {
                Debug.Log("ChargeController successfully linked in GameController.");
            }
        }
    }

    void Start()
    {
        currentPhase = Phase.Movement; 
        Debug.Log($"Starting game at Round {currentRound}, Phase {currentPhase}.");
        UpdateUI();
        InitializePlayerModels(); // Initialize and track player models
        EnableEndTurnButton(); // Always enable End Turn button initially
        HidePlayerErrorMessage(); // Hide the player error message initially

        // Get the ShootingController component
        shootingController = GetComponent<ShootingController>(); 
        if (shootingController == null)
        {
            Debug.LogError("ShootingController component is missing on GameController!");
        }
        else
        {
            Debug.Log("ShootingController successfully linked in GameController.");
        }

        // Initialize the WeaponUIController
        if (weaponUIController != null)
        {
            weaponUIController.Initialize(); // Initialize the Weapon UI Controller
            Debug.Log("WeaponUIController initialized.");
        }
        else
        {
            Debug.LogError("WeaponUIController is not assigned in the Inspector!");
        }
    }

    void Update()
    {
        HandleSelection(); // Ensure this is called every frame for selecting models
        HandleDeselect(); // Handle deselecting models with the Escape key
        HandleRotation(); // Handle rotating the selected model with mouse wheel
    }

    /// <summary>
    /// Enables the End Turn button.
    /// </summary>
    public void EnableEndTurnButton()
    {
        endTurnButton.interactable = true; // Enable the button
        Debug.Log("End Turn button enabled.");
    }

    /// <summary>
    /// Disables the End Turn button.
    /// </summary>
    public void DisableEndTurnButton()
    {
        endTurnButton.interactable = false; // Disable the button
        Debug.Log("End Turn button disabled.");
    }

    /// <summary>
    /// Handles the selection of models and interaction based on input.
    /// </summary>
    void HandleSelection()
    {
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
            Ray ray = Camera.main.ScreenPointToRay(screenCenter);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, RAYCAST_RANGE)) // Increased raycast range
            {
                Debug.Log("Raycast hit: " + hit.collider.name); // Log what the raycast hit

                ModelController model = hit.transform.GetComponent<ModelController>();
                if (model != null && model.playerID == currentPlayer) // Check if it's a valid model and belongs to the current player
                {
                    // Prevent selecting models that cannot charge during Charge phase
                    if (currentPhase == Phase.Charge)
                    {
                        if (model.HasMarched() || model.HasCharged())
                        {
                            ShowPlayerErrorMessage("This model cannot charge!");
                            Debug.Log("Attempted to select a model that cannot charge.");
                            return; // Do not select the model
                        }
                    }

                    SelectModel(model); // Select the model
                }
                else if (selectedModel != null && (currentPhase == Phase.Movement || currentPhase == Phase.Charge) && hit.collider.CompareTag("Ground")) // Move if ground is clicked and a model is selected, in movement or charge phase
                {
                    HandleMovement(hit.point); // Pass the hit point to handle movement
                }
                else if (selectedModel != null && (currentPhase == Phase.FirstFire || currentPhase == Phase.AdvanceFire)) // Handle shooting in shooting phases
                {
                    ModelController targetModel = hit.transform.GetComponent<ModelController>();
                    if (targetModel != null && targetModel.playerID != currentPlayer) // Target model must belong to the opposing player
                    {
                        shootingController.HandleShooting(targetModel); // Handle shooting logic
                    }
                }
                else if (selectedModel != null && currentPhase == Phase.Charge) // Handle charging target selection in Charge phase
                {
                    ModelController targetModel = hit.transform.GetComponent<ModelController>();
                    if (targetModel != null && targetModel.playerID != currentPlayer)
                    {
                        chargeController.SelectChargeTarget(targetModel); // Delegate to ChargeController
                        Debug.Log($"Charge initiated from {selectedModel.gameObject.name} to {targetModel.gameObject.name}.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handles the movement of the selected model to the target position.
    /// </summary>
    /// <param name="targetPosition">The target position to move the model to.</param>
    void HandleMovement(Vector3 targetPosition)
    {
        if (selectedModel == null)
        {
            Debug.LogError("No model selected for movement.");
            return;
        }

        // Calculate new distance from start position after the move
        Vector3 startPosXZ = new Vector3(selectedModel.GetStartPosition().x, 0, selectedModel.GetStartPosition().z);
        Vector3 targetPosXZ = new Vector3(targetPosition.x, 0, targetPosition.z);
        float newDistance = Vector3.Distance(startPosXZ, targetPosXZ);

        Debug.Log($"Attempting to move to new position. New Distance from Start: {newDistance}");

        float movementRangeConverted = selectedModel.movementRange * GameConstants.MOVEMENT_CONVERSION_FACTOR;
        float marchRangeConverted = (selectedModel.movementRange + selectedModel.initiative) * GameConstants.MOVEMENT_CONVERSION_FACTOR;

        if (currentPhase == Phase.Charge)
        {
            // During Charge phase, allow movement up to chargeDistance
            float chargeDistance = chargeController.ChargeDistance;
            Debug.Log($"Charge Distance: {chargeDistance}");

            if (newDistance <= chargeDistance)
            {
                Debug.Log("Move is within charge distance.");
                selectedModel.UpdateMovement(newDistance);
                selectedModel.MoveTo(targetPosition, newDistance);
                HidePlayerErrorMessage();

                // After moving, check for collision with the target
                StartCoroutine(CheckChargeCollision());
            }
            else
            {
                Debug.Log("Move is outside charge distance.");
                ShowPlayerErrorMessage("Invalid move! Please move within the charge distance.");
                DisableEndTurnButton();
            }
        }
        else
        {
            // Regular Movement and March phases
            if (newDistance <= movementRangeConverted)
            {
                // Within movement range
                Debug.Log("Move is within movement range.");
                selectedModel.UpdateMovement(newDistance);
                selectedModel.MoveTo(targetPosition, newDistance);
                HidePlayerErrorMessage();
                EnableEndTurnButton();
            }
            else if (newDistance <= marchRangeConverted)
            {
                // Within march range
                Debug.Log("Move is within march range.");
                selectedModel.UpdateMovement(newDistance);
                selectedModel.MoveTo(targetPosition, newDistance);
                HidePlayerErrorMessage();
                EnableEndTurnButton();
            }
            else
            {
                // Outside both ranges
                Debug.Log("Move is outside both movement and march ranges.");
                ShowPlayerErrorMessage("Invalid move! Please select a valid location within the movement or march range.");
                DisableEndTurnButton();
            }
        }

        // Log remaining movement and march
        Debug.Log($"After Move - Remaining Movement: {selectedModel.GetRemainingMovement()}, Remaining March: {selectedModel.GetRemainingMarchMovement()}");
    }

    /// <summary>
    /// Coroutine to check collision after moving during Charge phase.
    /// </summary>
    /// <returns></returns>
    private IEnumerator CheckChargeCollision()
    {
        yield return new WaitForEndOfFrame(); // Wait for movement to complete

        if (currentPhase != Phase.Charge || selectedModel == null)
            yield break;

        ModelController targetModel = chargeController.ChargeTarget;

        if (targetModel == null)
        {
            Debug.LogError("ChargeTarget is null during collision check.");
            yield break;
        }

        bool isColliding = selectedModel.IsColliding(targetModel);
        Debug.Log($"Collision Check: {isColliding}");

        if (isColliding)
        {
            Debug.Log("Charge collision successful.");
            ShowPlayerErrorMessage("Charge successful! End your turn.");
            EnableEndTurnButton();
        }
        else
        {
            Debug.Log("Charge collision failed.");
            ShowPlayerErrorMessage("Charge did not collide with the target. Please attempt to collide.");
            DisableEndTurnButton();
        }
    }

    /// <summary>
    /// Handles deselecting the selected model when the Escape key is pressed.
    /// </summary>
    void HandleDeselect()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && selectedModel != null) // Deselect with Escape key
        {
            DeselectAllModels();
            Debug.Log("Model deselected via Escape key.");
        }
    }

    /// <summary>
    /// Selects a model for interaction.
    /// </summary>
    /// <param name="model">The model to select.</param>
    void SelectModel(ModelController model)
    {
        if (selectedModel != null)
        {
            DeselectAllModels(); // Deselect the currently selected model
        }

        selectedModel = model;
        selectedModel.SelectModel();
        Debug.Log($"Model {model.gameObject.name} has been selected.");

        if (currentPhase == Phase.FirstFire)
        {
            if (model.HasMoved() || model.HasMarched())
            {
                ShowPlayerErrorMessage("Cannot shoot with a model that moved or marched in the Movement phase!");
                Debug.Log("Cannot shoot with a model that moved or marched.");
                DeselectAllModels();
            }
            else
            {
                ShowWeaponsUI(model); // Show weapons UI when selecting a model for shooting
            }
        }
        else if (currentPhase == Phase.AdvanceFire)
        {
            if (model.HasMarched())
            {
                ShowPlayerErrorMessage("Cannot shoot with a model that has marched in the Movement phase!");
                Debug.Log("Cannot shoot with a model that has marched.");
                DeselectAllModels();
            }
            else
            {
                ShowWeaponsUI(model); // Allow shooting if hasn't marched, regardless of movement
            }
        }
        else if (currentPhase == Phase.Charge)
        {
            // No additional action on selection during Charge phase
            Debug.Log("Model selected during Charge phase.");
        }
    }

    /// <summary>
    /// Deselects all models and hides relevant UI elements.
    /// </summary>
    public void DeselectAllModels()
    {
        if (selectedModel != null)
        {
            Debug.Log($"Deselecting model: {selectedModel.gameObject.name}");
            selectedModel.DeselectModel();
            selectedModel = null;
        }
        weaponUIController.HideWeaponUI(); // Hide the weapon UI when nothing is selected
        shootingController.HideWeaponRangeIndicator(); // Hide the weapon range indicator when deselected
    }

    /// <summary>
    /// Handles rotating the selected model based on mouse scroll input.
    /// </summary>
    void HandleRotation()
    {
        if (selectedModel != null)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f) // Check if the mouse wheel has been scrolled
            {
                selectedModel.RotateModel(scroll); // Rotate the selected model based on scroll input
                Debug.Log($"Rotated model {selectedModel.gameObject.name} by {scroll * ModelController.ROTATION_SPEED * Time.deltaTime} degrees.");
            }
        }
    }

    /// <summary>
    /// Advances to the next turn, handling player and phase transitions.
    /// </summary>
    public void NextTurn()
    {
        if (selectedModel != null)
        {
            selectedModel.DeleteMoveIndicator(); // Ensure the move indicator is deleted
        }

        currentPlayer++;
        if (currentPlayer > totalPlayers)
        {
            currentPlayer = 1;
            NextPhase(); 
        }
        DeselectAllModels(); // Deselect models at the end of the turn
        UpdateUI();
        Debug.Log($"Advanced to Player {currentPlayer}'s turn.");
    }

    /// <summary>
    /// Advances to the next phase or ends the round if in the last phase.
    /// </summary>
    public void NextPhase()
    {
        if (currentPhase == Phase.AdvanceFire)
        {
            EndRound(); 
        }
        else
        {
            currentPhase++;
            UpdateUI(); 

            if (currentPhase == Phase.Charge)
            {
                Debug.Log("Entering Charge Phase.");
                ResetStartPositionsForCharge(); // Reset starting positions at the start of Charge phase
            }
            else
            {
                Debug.Log($"Entering {currentPhase} Phase.");
            }
        }
    }

    /// <summary>
    /// Resets the starting positions of all models for the Charge phase.
    /// </summary>
    private void ResetStartPositionsForCharge()
    {
        foreach (GameObject modelObj in player1Models)
        {
            ModelController model = modelObj.GetComponent<ModelController>();
            if (model != null)
            {
                model.UpdateStartPosition();
                Debug.Log($"Start position reset for {model.gameObject.name}.");
            }
        }

        foreach (GameObject modelObj in player2Models)
        {
            ModelController model = modelObj.GetComponent<ModelController>();
            if (model != null)
            {
                model.UpdateStartPosition();
                Debug.Log($"Start position reset for {model.gameObject.name}.");
            }
        }
    }

    /// <summary>
    /// Ends the current round, resets states, and prepares for the next round.
    /// </summary>
    public void EndRound()
    {
        currentRound++;
        currentPhase = Phase.Movement;

        Debug.Log($"Ending Round {currentRound - 1}. Starting Round {currentRound}.");

        // Reset used weapons
        shootingController.ResetUsedWeapons();

        // Reset each model's movement, march, and charge status
        ResetAllModels();

        UpdateUI();
    }

    /// <summary>
    /// Returns the current player ID.
    /// </summary>
    /// <returns>The current player's ID.</returns>
    public int GetCurrentPlayer()
    {
        return currentPlayer;
    }

    /// <summary>
    /// Returns the current phase of the game.
    /// </summary>
    /// <returns>The current game phase.</returns>
    public Phase GetCurrentPhase()
    {
        return currentPhase;
    }

    /// <summary>
    /// Updates the UI elements to reflect the current game state.
    /// </summary>
    void UpdateUI()
    {
        roundText.text = "Round: " + currentRound;
        phaseText.text = "Phase: " + currentPhase.ToString();
        playerText.text = "Current Player: " + currentPlayer;
        Debug.Log($"UI Updated - Round: {currentRound}, Phase: {currentPhase}, Player: {currentPlayer}");
    }

    /// <summary>
    /// Initializes and categorizes all models based on their player IDs.
    /// </summary>
    void InitializePlayerModels()
    {
        ModelController[] allModels = FindObjectsOfType<ModelController>(); 
        foreach (ModelController model in allModels)
        {
            if (model.playerID == 1)
            {
                player1Models.Add(model.gameObject);
                Debug.Log($"Model {model.gameObject.name} added to Player 1's list.");
            }
            else if (model.playerID == 2)
            {
                player2Models.Add(model.gameObject);
                Debug.Log($"Model {model.gameObject.name} added to Player 2's list.");
            }
        }
        Debug.Log("Player models initialized and categorized.");
    }

    /// <summary>
    /// Displays an error message to the player.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    public void ShowPlayerErrorMessage(string message)
    {
        playerErrorText.text = message;
        playerErrorText.gameObject.SetActive(true);
        Debug.Log($"Player Error Message Displayed: {message}");
        StartCoroutine(HidePlayerErrorMessageAfterDelay(2f)); // Hide after 2 seconds
    }

    /// <summary>
    /// Coroutine to hide the player error message after a delay.
    /// </summary>
    /// <param name="delay">Delay in seconds before hiding the message.</param>
    /// <returns>IEnumerator for the coroutine.</returns>
    IEnumerator HidePlayerErrorMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HidePlayerErrorMessage();
    }

    /// <summary>
    /// Hides the player error message.
    /// </summary>
    public void HidePlayerErrorMessage()
    {
        playerErrorText.gameObject.SetActive(false);
        Debug.Log("Player error message hidden.");
    }

    /// <summary>
    /// Displays the weapons UI for the selected model.
    /// </summary>
    /// <param name="model">The selected model.</param>
    void ShowWeaponsUI(ModelController model)
    {
        weaponUIController.ShowWeaponOptions(model);
        Debug.Log($"Weapons UI displayed for model {model.gameObject.name}.");
    }

    /// <summary>
    /// Resets all models' movement, march, and charge statuses at the end of a round.
    /// </summary>
    private void ResetAllModels()
    {
        // Iterate through player1Models list in reverse to safely remove null references
        for (int i = player1Models.Count - 1; i >= 0; i--)
        {
            GameObject modelObj = player1Models[i];
            if (modelObj == null)
            {
                player1Models.RemoveAt(i); // Remove null references
                continue;
            }

            ModelController model = modelObj.GetComponent<ModelController>();
            if (model != null)
            {
                model.ResetMovement(); // Reset movement and hasMoved
                model.ResetMarch(); // Reset hasMarched
                model.ResetCharge(); // Reset hasCharged
                model.UpdateStartPosition(); // Update start position to current position
                Debug.Log($"Model {model.gameObject.name} reset for new round.");
            }
        }

        // Iterate through player2Models list in reverse to safely remove null references
        for (int i = player2Models.Count - 1; i >= 0; i--)
        {
            GameObject modelObj = player2Models[i];
            if (modelObj == null)
            {
                player2Models.RemoveAt(i); // Remove null references
                continue;
            }

            ModelController model = modelObj.GetComponent<ModelController>();
            if (model != null)
            {
                model.ResetMovement(); // Reset movement and hasMoved
                model.ResetMarch(); // Reset hasMarched
                model.ResetCharge(); // Reset hasCharged
                model.UpdateStartPosition(); // Update start position to current position
                Debug.Log($"Model {model.gameObject.name} reset for new round.");
            }
        }

        // Reset ChargeController if needed
        if (chargeController != null)
        {
            // Additional reset logic if necessary
            Debug.Log("All models have been reset.");
        }
    }

    /// <summary>
    /// Removes a destroyed model from the player lists.
    /// </summary>
    /// <param name="model">The model to remove.</param>
    public void RemoveModel(ModelController model)
    {
        if (model.playerID == 1)
        {
            player1Models.Remove(model.gameObject);
            Debug.Log($"Model {model.gameObject.name} removed from Player 1's list.");
        }
        else if (model.playerID == 2)
        {
            player2Models.Remove(model.gameObject);
            Debug.Log($"Model {model.gameObject.name} removed from Player 2's list.");
        }
    }
}

