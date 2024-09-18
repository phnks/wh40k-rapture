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

    public TextMeshProUGUI roundText;  
    public TextMeshProUGUI phaseText;  
    public TextMeshProUGUI playerText;  
    public TextMeshProUGUI playerErrorText; // Reference to the Player Error Text UI

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
        Instance = this; // Set up the singleton instance
    }

    void Start()
    {
        currentPhase = Phase.Movement; 
        UpdateUI();
        InitializePlayerModels(); // Initialize and track player models
        EnableEndTurnButton(); // Always enable End Turn button initially
        HidePlayerErrorMessage(); // Hide the player error message initially
        shootingController = GetComponent<ShootingController>(); // Get the ShootingController

        // Make sure WeaponUIController is properly initialized
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

    public void EnableEndTurnButton()
    {
        endTurnButton.interactable = true; // Enable the button
    }

    public void DisableEndTurnButton()
    {
        endTurnButton.interactable = false; // Disable the button
    }

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

    void HandleMovement(Vector3 targetPosition)
    {
        if (selectedModel == null)
        {
            Debug.LogError("No model selected for movement.");
            return;
        }

        float distance = Vector3.Distance(new Vector3(selectedModel.transform.position.x, 0, selectedModel.transform.position.z), 
                                          new Vector3(targetPosition.x, 0, targetPosition.z));

        float distanceToStart = Vector3.Distance(new Vector3(selectedModel.transform.position.x, 0, selectedModel.transform.position.z), 
                                                 new Vector3(selectedModel.GetStartPosition().x, 0, selectedModel.GetStartPosition().z));
        float newDistanceToStart = Vector3.Distance(new Vector3(targetPosition.x, 0, targetPosition.z), 
                                                    new Vector3(selectedModel.GetStartPosition().x, 0, selectedModel.GetStartPosition().z));
        
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
        }
        else
        {
            ShowPlayerErrorMessage("Invalid move! Please select a valid location within the movement range."); // Display an error message
            DisableEndTurnButton(); // Disable the end turn button if an invalid move is made
        }
    }

    void HandleDeselect()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && selectedModel != null) // Deselect with Escape key
        {
            DeselectAllModels();
        }
    }

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

    // **Updated Access Modifier: Made public to allow external access**
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

    public void EndRound()
    {
        currentRound++;
        currentPhase = Phase.Movement;
        shootingController.ResetUsedWeapons(); // Reset used weapons at end of round
        UpdateUI();
    }

    public int GetCurrentPlayer()
    {
        return currentPlayer;
    }

    public Phase GetCurrentPhase()
    {
        return currentPhase;
    }

    void UpdateUI()
    {
        roundText.text = "Round: " + currentRound;
        phaseText.text = "Phase: " + currentPhase.ToString();
        playerText.text = "Current Player: " + currentPlayer;
    }

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

    // Show the player error message on the screen
    public void ShowPlayerErrorMessage(string message)
    {
        playerErrorText.text = message;
        playerErrorText.gameObject.SetActive(true);
        StartCoroutine(HidePlayerErrorMessageAfterDelay(2f)); // Hide after 2 seconds
    }

    IEnumerator HidePlayerErrorMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HidePlayerErrorMessage();
    }

    // Hide the player error message
    public void HidePlayerErrorMessage()
    {
        playerErrorText.gameObject.SetActive(false);
    }

    void ShowWeaponsUI(ModelController model)
    {
        weaponUIController.ShowWeaponOptions(model);
    }
}

