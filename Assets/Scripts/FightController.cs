// FightController.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class FightController : MonoBehaviour
{
    public static FightController Instance;

    [Header("UI Elements")]
    public Button fightButton; // Assign the "Fight" button in the Inspector
    public Button confirmPileInMoveButton; // Assign the "Confirm Pile In Move" button in the Inspector
    public TextMeshProUGUI initiativeText; // Assign the Initiative Round Text UI element

    [Header("Pile In Move")]
    public GameObject pileInMoveRangeIndicatorPrefab; // Assign the Pile In Move Range Indicator Prefab

    private GameController gameController;
    private List<Fight> activeFights = new List<Fight>();
    private Fight selectedFight = null;

    private int currentInitiativeRound;
    private const int maxInitiativeRound = 10;

    private HashSet<ModelController> availableFighters;
    private HashSet<ModelController> usedFighters;

    private int currentPlayer;

    private bool isResolvingFight = false;
    private ModelController selectedModelForPileInMove = null;
    private GameObject currentPileInMoveIndicator = null;

    private Coroutine fightResolutionCoroutine;

    // Enum to manage fight phase states
    private enum FightPhaseState
    {
        None,
        SelectingFight,
        ResolvingInitiativeRound,
        PileInMove
    }

    private FightPhaseState fightPhaseState = FightPhaseState.None;

    // Represents a fight containing participating models
    private class Fight
    {
        public HashSet<ModelController> participants;

        public Fight()
        {
            participants = new HashSet<ModelController>();
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("FightController singleton instance initialized.");
        }
        else
        {
            Destroy(gameObject);
            Debug.LogWarning("Duplicate FightController instance destroyed.");
        }

        if (fightButton != null)
        {
            fightButton.onClick.RemoveAllListeners(); // Remove any existing listeners
            fightButton.onClick.AddListener(StartFightResolution);
            fightButton.gameObject.SetActive(false); // Hide initially
        }
        else
        {
            Debug.LogError("FightButton is not assigned in the Inspector!");
        }

        if (confirmPileInMoveButton != null)
        {
            confirmPileInMoveButton.onClick.RemoveAllListeners();
            confirmPileInMoveButton.onClick.AddListener(ConfirmPileInMove);
            confirmPileInMoveButton.gameObject.SetActive(false); // Hide initially
        }
        else
        {
            Debug.LogError("ConfirmPileInMoveButton is not assigned in the Inspector!");
        }

        if (initiativeText != null)
        {
            initiativeText.gameObject.SetActive(false); // Hide initiative text initially
        }
        else
        {
            Debug.LogError("InitiativeText is not assigned in the Inspector!");
        }

        if (pileInMoveRangeIndicatorPrefab == null)
        {
            Debug.LogError("PileInMoveRangeIndicatorPrefab is not assigned in the Inspector!");
        }
    }

    void Start()
    {
        gameController = GameController.Instance;
        if (gameController == null)
        {
            Debug.LogError("GameController instance not found!");
        }
    }

    /// <summary>
    /// Call this method to start the Fight phase from GameController.
    /// </summary>
    public void StartFightPhase()
    {
        Debug.Log("Fight phase started.");
        FindAllFights();
        if (activeFights.Count == 0)
        {
            Debug.Log("No fights detected. Ending Fight phase.");
            EndFightPhase();
            return;
        }
        fightPhaseState = FightPhaseState.SelectingFight;
        PromptPlayerToSelectFight();
    }

    /// <summary>
    /// Call this method to start the Fight resolution process when the Fight button is clicked.
    /// </summary>
    private void StartFightResolution()
    {
        if (selectedFight == null)
        {
            Debug.LogError("No fight selected to resolve.");
            gameController.ShowPlayerErrorMessage("No fight selected to resolve.");
            return;
        }

        isResolvingFight = true;
        currentInitiativeRound = maxInitiativeRound;
        fightPhaseState = FightPhaseState.ResolvingInitiativeRound;
        UpdateInitiativeUI();
        initiativeText.gameObject.SetActive(true); // Show initiative text
        gameController.ShowPlayerErrorMessage($"Fight resolution started. Initiating at Round {currentInitiativeRound}.");
        fightResolutionCoroutine = StartCoroutine(FightResolutionCoroutine());
    }

    /// <summary>
    /// Coroutine to handle the fight resolution steps.
    /// </summary>
    private IEnumerator FightResolutionCoroutine()
    {
        while (currentInitiativeRound >= 1)
        {
            Debug.Log($"Initiative Round {currentInitiativeRound}.");
            gameController.ShowPlayerErrorMessage($"Initiative Round {currentInitiativeRound}");

            availableFighters = new HashSet<ModelController>();
            foreach (var model in selectedFight.participants)
            {
                int effectiveInitiative = model.initiative;
                if (model.HasCharged())
                {
                    effectiveInitiative += 1;
                }
                if (effectiveInitiative == currentInitiativeRound)
                {
                    availableFighters.Add(model);
                }
            }

            if (availableFighters.Count > 0)
            {
                Debug.Log($"Available fighters in Initiative Round {currentInitiativeRound}: {availableFighters.Count}");
                usedFighters = new HashSet<ModelController>();
                currentPlayer = gameController.GetCurrentPlayer();

                while (availableFighters.Any(m => m.playerID == currentPlayer && !usedFighters.Contains(m)))
                {
                    gameController.ShowPlayerErrorMessage($"Player {currentPlayer}, select a model to perform a pile in move.");
                    fightPhaseState = FightPhaseState.PileInMove;

                    yield return StartCoroutine(WaitForModelSelection());

                    if (selectedModelForPileInMove != null)
                    {
                        yield return StartCoroutine(HandlePileInMove(selectedModelForPileInMove));
                        usedFighters.Add(selectedModelForPileInMove);
                        selectedModelForPileInMove = null;

                        // Switch to the other player
                        currentPlayer = (currentPlayer == 1) ? 2 : 1;

                        // Check if the next player has any available fighters
                        if (!availableFighters.Any(m => m.playerID == currentPlayer && !usedFighters.Contains(m)))
                        {
                            // If not, switch back
                            currentPlayer = (currentPlayer == 1) ? 2 : 1;
                            if (!availableFighters.Any(m => m.playerID == currentPlayer && !usedFighters.Contains(m)))
                            {
                                // No available fighters for any player
                                break;
                            }
                        }
                    }
                }
            }

            currentInitiativeRound--;
            UpdateInitiativeUI();
            yield return null;
        }

        // Fight resolved
        gameController.ShowPlayerErrorMessage("Fight resolved.");
        EndFightResolution();
    }

    /// <summary>
    /// Waits for the player to select a model to perform a pile in move.
    /// </summary>
    private IEnumerator WaitForModelSelection()
    {
        selectedModelForPileInMove = null;
        while (selectedModelForPileInMove == null && fightPhaseState == FightPhaseState.PileInMove)
        {
            if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
                Ray ray = Camera.main.ScreenPointToRay(screenCenter);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 2000f))
                {
                    ModelController clickedModel = hit.transform.GetComponent<ModelController>();
                    if (clickedModel != null && availableFighters.Contains(clickedModel) && clickedModel.playerID == currentPlayer)
                    {
                        selectedModelForPileInMove = clickedModel;
                        Debug.Log($"Model {clickedModel.gameObject.name} selected for pile in move.");
                    }
                    else
                    {
                        gameController.ShowPlayerErrorMessage("Select one of your available fighters to perform a pile in move.");
                        Debug.Log("Invalid model selected for pile in move.");
                    }
                }
            }
            yield return null;
        }
    }

    /// <summary>
    /// Handles the pile in move for a selected model.
    /// </summary>
    private IEnumerator HandlePileInMove(ModelController model)
    {
        gameController.ShowPlayerErrorMessage($"Player {currentPlayer}, perform a pile in move with {model.gameObject.name}.");

        // Display pile in move range indicator
        currentPileInMoveIndicator = Instantiate(pileInMoveRangeIndicatorPrefab, model.transform.position + Vector3.up * 60f, Quaternion.identity);
        float pileInMoveDistance = 3 * GameConstants.MOVEMENT_CONVERSION_FACTOR;
        currentPileInMoveIndicator.transform.localScale = new Vector3(pileInMoveDistance * 2, 0.01f, pileInMoveDistance * 2);
        Renderer renderer = currentPileInMoveIndicator.GetComponent<Renderer>();
        renderer.material.color = model.GetFactionColor();
        renderer.material.color = new Color(renderer.material.color.r, renderer.material.color.g, renderer.material.color.b, 0.3f);

        bool moveValid = false;
        Vector3 targetPosition = Vector3.zero;

        // Wait for player to click on a valid location
        while (!moveValid && fightPhaseState == FightPhaseState.PileInMove)
        {
            if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
                Ray ray = Camera.main.ScreenPointToRay(screenCenter);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 2000f))
                {
                    if (hit.collider.CompareTag("Ground"))
                    {
                        Vector3 clickedPosition = hit.point;
                        float distance = Vector3.Distance(model.transform.position, clickedPosition);

                        if (distance <= pileInMoveDistance)
                        {
                            // Check collision with at least one enemy model in the fight
                            bool collidesWithEnemy = false;
                            foreach (var enemy in selectedFight.participants)
                            {
                                if (enemy.playerID != model.playerID)
                                {
                                    Collider enemyCollider = enemy.GetComponent<Collider>();
                                    Collider modelCollider = model.GetComponent<Collider>();

                                    if (modelCollider == null || enemyCollider == null)
                                    {
                                        Debug.LogError("Model or enemy does not have a collider.");
                                        continue;
                                    }

                                    Bounds modelBounds = modelCollider.bounds;
                                    Vector3 newPosition = new Vector3(clickedPosition.x, modelBounds.center.y, clickedPosition.z);
                                    Bounds newModelBounds = new Bounds(newPosition, modelBounds.size);

                                    if (newModelBounds.Intersects(enemyCollider.bounds))
                                    {
                                        collidesWithEnemy = true;
                                        break;
                                    }
                                }
                            }

                            if (collidesWithEnemy)
                            {
                                targetPosition = new Vector3(clickedPosition.x, model.transform.position.y, clickedPosition.z);
                                model.MoveTo(targetPosition, distance);
                                moveValid = true;
                                gameController.ShowPlayerErrorMessage("Pile in move successful.");
                            }
                            else
                            {
                                gameController.ShowPlayerErrorMessage("Invalid pile in move! Must collide with at least one enemy model.");
                                Debug.Log("Move does not collide with any enemy models in the fight.");
                            }
                        }
                        else
                        {
                            gameController.ShowPlayerErrorMessage("Pile in move out of range.");
                            Debug.Log("Pile in move attempted out of range.");
                        }
                    }
                }
            }
            yield return null;
        }

        // Destroy the pile in move indicator
        if (currentPileInMoveIndicator != null)
        {
            Destroy(currentPileInMoveIndicator);
            currentPileInMoveIndicator = null;
        }

        // Show the "Confirm Pile In Move" button
        confirmPileInMoveButton.gameObject.SetActive(true);
        gameController.ShowPlayerErrorMessage("Click 'Confirm Pile In Move' to finalize your action.");
        Debug.Log("Pile in move performed. Awaiting confirmation.");

        // Wait until the player confirms the move
        while (confirmPileInMoveButton.gameObject.activeSelf && fightPhaseState == FightPhaseState.PileInMove)
        {
            yield return null;
        }

        // Reset the fight phase state
        fightPhaseState = FightPhaseState.ResolvingInitiativeRound;
    }

    /// <summary>
    /// Handles the Confirm Pile In Move button click.
    /// </summary>
    private void ConfirmPileInMove()
    {
        confirmPileInMoveButton.gameObject.SetActive(false);
        gameController.ShowPlayerErrorMessage("Pile in move confirmed.");
        Debug.Log("Pile in move confirmed.");
    }

    /// <summary>
    /// Updates the initiative round UI.
    /// </summary>
    private void UpdateInitiativeUI()
    {
        if (initiativeText != null)
        {
            initiativeText.text = "Initiative Round: " + currentInitiativeRound;
        }
    }

    /// <summary>
    /// Ends the Fight resolution process.
    /// </summary>
    private void EndFightResolution()
    {
        isResolvingFight = false;
        selectedFight = null;
        fightPhaseState = FightPhaseState.None;
        initiativeText.gameObject.SetActive(false); // Hide initiative text
        gameController.NextPhase();
        Debug.Log("Fight resolution ended.");
    }

    /// <summary>
    /// Finds all ongoing fights by grouping colliding models.
    /// </summary>
    public void FindAllFights()
    {
        activeFights.Clear();
        List<ModelController> allModels = FindObjectsOfType<ModelController>().ToList();

        // Initialize Union-Find structure
        Dictionary<ModelController, ModelController> parent = new Dictionary<ModelController, ModelController>();
        foreach (var model in allModels)
        {
            parent[model] = model;
        }

        // Find sets of colliding models
        for (int i = 0; i < allModels.Count; i++)
        {
            for (int j = i + 1; j < allModels.Count; j++)
            {
                if (allModels[i].IsColliding(allModels[j]))
                {
                    Union(allModels[i], allModels[j], parent);
                }
            }
        }

        // Group models by their root parent
        Dictionary<ModelController, List<ModelController>> groups = new Dictionary<ModelController, List<ModelController>>();
        foreach (var model in allModels)
        {
            ModelController root = Find(model, parent);
            if (!groups.ContainsKey(root))
            {
                groups[root] = new List<ModelController>();
            }
            groups[root].Add(model);
        }

        // Create fights from groups with more than one participant
        foreach (var group in groups.Values)
        {
            if (group.Count > 1)
            {
                Fight fight = new Fight();
                foreach (var model in group)
                {
                    fight.participants.Add(model);
                }
                activeFights.Add(fight);
                Debug.Log($"Fight identified with {group.Count} models.");
            }
        }

        if (activeFights.Count == 0)
        {
            Debug.Log("No fights detected. Ending Fight phase.");
            EndFightPhase();
        }
    }

    /// <summary>
    /// Finds the root parent of a model in Union-Find.
    /// </summary>
    private ModelController Find(ModelController model, Dictionary<ModelController, ModelController> parent)
    {
        if (parent[model] != model)
        {
            parent[model] = Find(parent[model], parent);
        }
        return parent[model];
    }

    /// <summary>
    /// Unions two models in Union-Find.
    /// </summary>
    private void Union(ModelController a, ModelController b, Dictionary<ModelController, ModelController> parent)
    {
        ModelController rootA = Find(a, parent);
        ModelController rootB = Find(b, parent);
        if (rootA != rootB)
        {
            parent[rootB] = rootA;
        }
    }

    /// <summary>
    /// Prompts the current player to select a fight.
    /// </summary>
    private void PromptPlayerToSelectFight()
    {
        int currentPlayerId = gameController.GetCurrentPlayer();
        Debug.Log($"Player {currentPlayerId}, select a fight to resolve.");
        gameController.ShowPlayerErrorMessage($"Player {currentPlayerId}, select a fight to resolve.");
    }

    /// <summary>
    /// Handles the selection of a fight when a model is clicked.
    /// </summary>
    private void HandleFightSelection()
    {
        if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            // Use the screen center (reticle) for raycasting
            Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
            Ray ray = Camera.main.ScreenPointToRay(screenCenter);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 2000f))
            {
                ModelController clickedModel = hit.transform.GetComponent<ModelController>();
                if (clickedModel != null)
                {
                    Fight fight = activeFights.FirstOrDefault(f => f.participants.Contains(clickedModel));
                    if (fight != null)
                    {
                        int currentPlayerId = gameController.GetCurrentPlayer();
                        if (fight.participants.Any(m => m.playerID == currentPlayerId))
                        {
                            SelectFight(fight);
                        }
                        else
                        {
                            gameController.ShowPlayerErrorMessage("You must select a fight that includes one of your own models.");
                            Debug.Log("Selected fight does not include any of your models.");
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Deselects the currently selected fight when Escape is pressed.
    /// </summary>
    private void HandleDeselect()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && selectedFight != null)
        {
            DeselectCurrentFight();
            Debug.Log("Fight deselected via Escape key.");
        }
    }

    /// <summary>
    /// Selects a fight and highlights its models.
    /// </summary>
    private void SelectFight(Fight fight)
    {
        if (selectedFight != null)
        {
            DeselectCurrentFight();
        }

        selectedFight = fight;
        HighlightFightModels(fight);
        fightButton.gameObject.SetActive(true);
        Debug.Log("Fight selected and highlighted.");
    }

    /// <summary>
    /// Deselects the currently selected fight and removes highlights.
    /// </summary>
    private void DeselectCurrentFight()
    {
        if (selectedFight != null)
        {
            UnhighlightFightModels(selectedFight);
            selectedFight = null;
            fightButton.gameObject.SetActive(false);
            Debug.Log("Fight deselected and highlights removed.");
        }
    }

    /// <summary>
    /// Highlights all models in a fight by enabling their outlines.
    /// </summary>
    private void HighlightFightModels(Fight fight)
    {
        foreach (var model in fight.participants)
        {
            Outline outline = model.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = true;
            }
        }
    }

    /// <summary>
    /// Unhighlights all models in a fight by disabling their outlines.
    /// </summary>
    private void UnhighlightFightModels(Fight fight)
    {
        foreach (var model in fight.participants)
        {
            Outline outline = model.GetComponent<Outline>();
            if (outline != null)
            {
                outline.enabled = false;
            }
        }
    }

    /// <summary>
    /// Ends the Fight phase and notifies the GameController.
    /// </summary>
    private void EndFightPhase()
    {
        if (fightResolutionCoroutine != null)
        {
            StopCoroutine(fightResolutionCoroutine);
        }

        fightPhaseState = FightPhaseState.None;
        selectedFight = null;
        initiativeText.gameObject.SetActive(false); // Hide initiative text
        gameController.HidePlayerErrorMessage();
        gameController.NextPhase();
        Debug.Log("Fight phase ended.");
    }

    void Update()
    {
        if (fightPhaseState == FightPhaseState.SelectingFight)
        {
            HandleFightSelection();
            HandleDeselect();
        }
        else if (fightPhaseState == FightPhaseState.None && gameController.GetCurrentPhase() == GameController.Phase.Fight)
        {
            // Ensure fightPhaseState is set correctly
            PromptPlayerToSelectFight();
            fightPhaseState = FightPhaseState.SelectingFight;
        }
    }
}

