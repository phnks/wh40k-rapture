// FightController.cs
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class FightController : MonoBehaviour
{
    public static FightController Instance;

    [Header("UI Elements")]
    public Button fightButton; // Assign the "Fight" button in the Inspector
    public Text fightButtonText; // Optional: Assign if you want to change button text dynamically

    private GameController gameController;
    private List<Fight> activeFights = new List<Fight>();
    private Fight selectedFight = null;

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
            fightButton.onClick.AddListener(ResolveSelectedFight);
            fightButton.gameObject.SetActive(false); // Hide initially
        }
        else
        {
            Debug.LogError("FightButton is not assigned in the Inspector!");
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
    /// Call this method to start the Fight phase.
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
        PromptPlayerToSelectFight();
    }

    /// <summary>
    /// Finds all ongoing fights by grouping colliding models.
    /// </summary>
    private void FindAllFights()
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
        int currentPlayer = gameController.GetCurrentPlayer();
        Debug.Log($"Player {currentPlayer}, select a fight to resolve.");
        gameController.ShowPlayerErrorMessage($"Player {currentPlayer}, select a fight to resolve.");
    }

    void Update()
    {
        HandleFightSelection();
        HandleDeselect();
    }

    /// <summary>
    /// Handles the selection of a fight when a model is clicked.
    /// </summary>
    private void HandleFightSelection()
    {
        if (gameController.GetCurrentPhase() != GameController.Phase.Fight)
            return;

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
                        int currentPlayer = gameController.GetCurrentPlayer();
                        if (fight.participants.Any(m => m.playerID == currentPlayer))
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
    /// Handles deselection when the Escape key is pressed.
    /// </summary>
    private void HandleDeselect()
    {
        if (gameController.GetCurrentPhase() != GameController.Phase.Fight)
            return;

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
            model.GetComponent<Outline>().enabled = true;
        }
    }

    /// <summary>
    /// Unhighlights all models in a fight by disabling their outlines.
    /// </summary>
    private void UnhighlightFightModels(Fight fight)
    {
        foreach (var model in fight.participants)
        {
            model.GetComponent<Outline>().enabled = false;
        }
    }

    /// <summary>
    /// Resolves the selected fight.
    /// </summary>
    private void ResolveSelectedFight()
    {
        if (selectedFight == null)
        {
            Debug.LogError("No fight selected to resolve.");
            return;
        }

        int currentPlayer = gameController.GetCurrentPlayer();
        Debug.Log($"[ResolveSelectedFight] Current Player: {currentPlayer}");
        Debug.Log($"Resolving fight with {selectedFight.participants.Count} models by Player {currentPlayer}.");
        gameController.ShowPlayerErrorMessage($"Player {currentPlayer} is resolving a fight.");

        // Placeholder for fight resolution logic
        // TODO: Implement actual fight resolution mechanics

        // After resolving, mark fight as resolved
        activeFights.Remove(selectedFight);
        UnhighlightFightModels(selectedFight);
        selectedFight = null;
        fightButton.gameObject.SetActive(false);
        Debug.Log($"Fight resolved by Player {currentPlayer}.");

        // Check if there are more fights to resolve
        if (activeFights.Count > 0)
        {
            gameController.IncrementPlayer(); // Switch to the next player
            Debug.Log($"Player incremented to Player {gameController.GetCurrentPlayer()}.");
            PromptPlayerToSelectFight();
        }
        else
        {
            Debug.Log("All fights resolved. Ending Fight phase.");
            EndFightPhase();
        }
    }

    /// <summary>
    /// Ends the Fight phase and notifies the GameController.
    /// </summary>
    private void EndFightPhase()
    {
        gameController.HidePlayerErrorMessage();
        gameController.NextPhase();
        Debug.Log("Fight phase ended.");
    }
}

