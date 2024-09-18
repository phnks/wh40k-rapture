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

    [Header("Buttons and Panels")]
    public Button endTurnButton; // Reference to the End Turn button
    public GameObject weaponUIPanel; // Reference to the UI panel displaying weapons
    public WeaponUIController weaponUIController; // Reference to the Weapon UI controller

    private int currentRound = 1;       
    private Phase currentPhase;         
    private int currentPlayer = 1;      

    private int totalPlayers = 2;       

    private List<GameObject> player1Models = new List<GameObject>();  // List of Player 1's models
    private List<GameObject> player2Models = new List<GameObject>();  // List of Player 2's models

    private ModelController selectedModel; // Reference to the currently selected model
    private ShootingController shootingController; // Reference to the Shooting Controller

    private const float RAYCAST_RANGE = 2000f; // Increased raycast range for selection

    void Awake()
    {
        // Set up the singleton instance
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        currentPhase = Phase.Movement; 
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

        // Initialize the WeaponUIController
        if (weaponUIController != null)
        {
            weaponUIController.Initialize(); // Initialize the Weapon UI Controller
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
    }

    /// <summary>
    /// Disables the End Turn button.
    /// </summary>
    public void DisableEndTurnButton()
    {
        endTurnButton.interactable = false; // Disable the button
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
                    SelectModel(model); // Select the model
                }
                else if (selectedModel != null && currentPhase == Phase.Movement && hit.collider.CompareTag("Ground")) // Move if ground is clicked and a model is selected, only in movement phase
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

        // Calculate distance between current position and target position on the XZ plane
        float distance = Vector3.Distance(new Vector3(selectedModel.transform.position.x, 0, selectedModel.transform.position.z), 
                                          new Vector3(targetPosition.x, 0, targetPosition.z));

        // Calculate distance from start position to current position and to the target position
        float distanceToStart = Vector3.Distance(new Vector3(selectedModel.transform.position.x, 0, selectedModel.transform.position.z), 
                                                 new Vector3(selectedModel.GetStartPosition().x, 0, selectedModel.GetStartPosition().z));
        float newDistanceToStart = Vector3.Distance(new Vector3(targetPosition.x, 0, targetPosition.z), 
                                                    new Vector3(selectedModel.GetStartPosition().x, 0, selectedModel.GetStartPosition().z));

        // Calculate potential remaining movement based on moving closer to the start position
        float potentialRemainingMovement = selectedModel.GetRemainingMovement();

        if (newDistanceToStart < distanceToStart)
        {
            float distanceDiff = distanceToStart - newDistanceToStart;
            potentialRemainingMovement = Mathf.Min(potentialRemainingMovement + distanceDiff, selectedModel.movementRange * GameConstants.MOVEMENT_CONVERSION_FACTOR);
        }

        if (distance <= potentialRemainingMovement) // Check against potential remaining movement
        {
            selectedModel.MoveTo(targetPosition); // Move the selected model
            HidePlayerErrorMessage(); // Hide any previous error message
            EnableEndTurnButton(); // Re-enable the end turn button after a valid move
        }
        else
        {
            ShowPlayerErrorMessage("Invalid move! Please select a valid location within the movement range."); // Display an error message
            DisableEndTurnButton(); // Disable the end turn button if an invalid move is made
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

        if (currentPhase == Phase.FirstFire || currentPhase == Phase.AdvanceFire)
        {
            if (currentPhase == Phase.FirstFire && model.HasMoved())
            {
                ShowPlayerErrorMessage("Cannot shoot with a model that moved in the Movement phase!");
                DeselectAllModels();
            }
            else
            {
                ShowWeaponsUI(model); // Show weapons UI when selecting a model for shooting
            }
        }
    }

    /// <summary>
    /// Deselects all models and hides relevant UI elements.
    /// </summary>
    public void DeselectAllModels()
    {
        if (selectedModel != null)
        {
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
        }
    }

    /// <summary>
    /// Ends the current round, resets states, and prepares for the next round.
    /// </summary>
    public void EndRound()
    {
        currentRound++;
        currentPhase = Phase.Movement;

        // Reset used weapons
        shootingController.ResetUsedWeapons();

        // Reset each model's movement and states
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
            }
            else if (model.playerID == 2)
            {
                player2Models.Add(model.gameObject);
            }
        }
    }

    /// <summary>
    /// Displays an error message to the player.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    public void ShowPlayerErrorMessage(string message)
    {
        playerErrorText.text = message;
        playerErrorText.gameObject.SetActive(true);
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
    }

    /// <summary>
    /// Displays the weapons UI for the selected model.
    /// </summary>
    /// <param name="model">The selected model.</param>
    void ShowWeaponsUI(ModelController model)
    {
        weaponUIController.ShowWeaponOptions(model);
    }

    /// <summary>
    /// Resets all models' movement and states at the end of a round.
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
                model.UpdateStartPosition(); // Update start position to current position
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
                model.UpdateStartPosition(); // Update start position to current position
            }
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
        }
        else if (model.playerID == 2)
        {
            player2Models.Remove(model.gameObject);
        }
    }
}

