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
    public Button fightButton;
    public Button confirmPileInMoveButton;
    public TextMeshProUGUI initiativeText;
    public TextMeshProUGUI remainingAttacksText; // Text to display remaining attacks

    [Header("Pile In Move")]
    public GameObject pileInMoveRangeIndicatorPrefab;

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

    // Added new FightPhaseState
    private enum FightPhaseState
    {
        None,
        SelectingFight,
        ResolvingInitiativeRound,
        PileInMove,
        Attacks // New state
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

    // Data structure to store attacks
    private class AttackAction
    {
        public ModelController attacker;
        public ModelController target;
        public WeaponController weapon;

        public AttackAction(ModelController attacker, ModelController target, WeaponController weapon)
        {
            this.attacker = attacker;
            this.target = target;
            this.weapon = weapon;
        }
    }

    private List<AttackAction> attackActions = new List<AttackAction>(); // List to store all attacks

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
            fightButton.onClick.RemoveAllListeners();
            fightButton.onClick.AddListener(StartFightResolution);
            fightButton.gameObject.SetActive(false);
            Debug.Log("Fight button listeners set and button hidden initially.");
        }
        else
        {
            Debug.LogError("FightButton is not assigned in the Inspector!");
        }

        if (confirmPileInMoveButton != null)
        {
            confirmPileInMoveButton.onClick.RemoveAllListeners();
            confirmPileInMoveButton.onClick.AddListener(ConfirmPileInMove);
            confirmPileInMoveButton.gameObject.SetActive(false);
            Debug.Log("Confirm Pile In Move button listeners set and button hidden initially.");
        }
        else
        {
            Debug.LogError("ConfirmPileInMoveButton is not assigned in the Inspector!");
        }

        if (initiativeText != null)
        {
            initiativeText.gameObject.SetActive(false);
            Debug.Log("Initiative text hidden initially.");
        }
        else
        {
            Debug.LogError("InitiativeText is not assigned in the Inspector!");
        }

        if (pileInMoveRangeIndicatorPrefab == null)
        {
            Debug.LogError("PileInMoveRangeIndicatorPrefab is not assigned in the Inspector!");
        }
        else
        {
            Debug.Log("Pile In Move Range Indicator Prefab assigned.");
        }

        if (remainingAttacksText != null)
        {
            remainingAttacksText.gameObject.SetActive(false);
            Debug.Log("Remaining attacks text hidden initially.");
        }
        else
        {
            Debug.LogError("RemainingAttacksText is not assigned in the Inspector!");
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
            Debug.Log("FightController successfully linked to GameController.");
        }
    }

    void Update()
    {
        if (fightPhaseState == FightPhaseState.SelectingFight)
        {
            HandleFightSelection();
            HandleDeselect();
        }
        else if (fightPhaseState == FightPhaseState.Attacks)
        {
            HandleAttackSelection();
            HandleDeselect();
        }
        else if (fightPhaseState == FightPhaseState.None && gameController.GetCurrentPhase() == GameController.Phase.Fight)
        {
            PromptPlayerToSelectFight();
            fightPhaseState = FightPhaseState.SelectingFight;
            Debug.Log("FightController: FightPhaseState set to SelectingFight.");
        }
    }

    public void StartFightPhase()
    {
        Debug.Log("FightController: StartFightPhase called.");
        FindAllFights();
        if (activeFights.Count == 0)
        {
            Debug.Log("FightController: No fights detected. Ending Fight phase.");
            EndFightPhase();
            return;
        }
        fightPhaseState = FightPhaseState.SelectingFight;
        Debug.Log("FightController: FightPhaseState set to SelectingFight.");
        PromptPlayerToSelectFight();
    }

    private void StartFightResolution()
    {
        Debug.Log("FightController: StartFightResolution called.");
        if (selectedFight == null)
        {
            Debug.LogError("FightController: No fight selected to resolve.");
            gameController.ShowPlayerErrorMessage("No fight selected to resolve.");
            return;
        }

        foreach (var model in selectedFight.participants)
        {
            model.DeselectModel();
            Debug.Log($"FightController: Model {model.gameObject.name} deselected.");
        }

        fightButton.gameObject.SetActive(false);
        Debug.Log("FightController: Fight button hidden.");

        foreach (var model in selectedFight.participants)
        {
            model.UpdateStartPosition();
            Debug.Log($"FightController: Model {model.gameObject.name} start position updated.");
        }

        isResolvingFight = true;
        currentInitiativeRound = maxInitiativeRound;
        fightPhaseState = FightPhaseState.ResolvingInitiativeRound;
        UpdateInitiativeUI();
        initiativeText.gameObject.SetActive(true);
        gameController.ShowPlayerErrorMessage($"Fight resolution started. Initiating at Round {currentInitiativeRound}.");
        Debug.Log($"FightController: Fight resolution started at Round {currentInitiativeRound}.");
        fightResolutionCoroutine = StartCoroutine(FightResolutionCoroutine());
    }

    private IEnumerator FightResolutionCoroutine()
    {
        Debug.Log("FightController: FightResolutionCoroutine started.");
        while (currentInitiativeRound >= 1)
        {
            Debug.Log($"FightController: Initiative Round {currentInitiativeRound}.");
            gameController.ShowPlayerErrorMessage($"Initiative Round {currentInitiativeRound}");

            availableFighters = new HashSet<ModelController>();
            foreach (var model in selectedFight.participants)
            {
                int effectiveInitiative = model.initiative;
                if (model.HasCharged())
                {
                    effectiveInitiative += 1;
                }
                if (effectiveInitiative == currentInitiativeRound && !model.HasFought())
                {
                    availableFighters.Add(model);
                }
            }

            if (availableFighters.Count > 0)
            {
                Debug.Log($"FightController: Available fighters in Initiative Round {currentInitiativeRound}: {availableFighters.Count}");
                usedFighters = new HashSet<ModelController>();

                currentPlayer = 1;
                Debug.Log($"FightController: Starting Initiative Round {currentInitiativeRound} with Player {currentPlayer}.");

                bool fightersLeft = true;
                int playersChecked = 0;
                int totalPlayers = 2;

                while (fightersLeft && playersChecked < totalPlayers)
                {
                    var fightersForCurrentPlayer = availableFighters
                        .Where(m => m.playerID == currentPlayer && !usedFighters.Contains(m))
                        .ToList();

                    if (fightersForCurrentPlayer.Count > 0)
                    {
                        // Start Attacks phase
                        fightPhaseState = FightPhaseState.Attacks;
                        foreach (var fighter in fightersForCurrentPlayer)
                        {
                            if (fighter.HasFought())
                            {
                                usedFighters.Add(fighter);
                                continue;
                            }

                            gameController.ShowPlayerErrorMessage($"Player {currentPlayer}, select a model to fight.");
                            Debug.Log($"FightController: Prompting Player {currentPlayer} to select a model to fight.");

                            yield return StartCoroutine(WaitForModelSelectionForAttack(currentPlayer));

                            if (selectedModelForPileInMove != null)
                            {
                                Debug.Log($"FightController: Model {selectedModelForPileInMove.gameObject.name} selected for attacks.");
                                yield return StartCoroutine(HandleAttacks(selectedModelForPileInMove));
                                usedFighters.Add(selectedModelForPileInMove);
                                selectedModelForPileInMove = null;

                                currentPlayer = (currentPlayer == 1) ? 2 : 1;
                                Debug.Log($"FightController: Switched to Player {currentPlayer}.");
                            }
                            else
                            {
                                currentPlayer = (currentPlayer == 1) ? 2 : 1;
                                Debug.Log($"FightController: Switched to Player {currentPlayer}.");
                            }
                        }
                    }
                    else
                    {
                        Debug.Log($"FightController: Player {currentPlayer} has no available fighters.");
                        currentPlayer = (currentPlayer == 1) ? 2 : 1;
                        Debug.Log($"FightController: Switched to Player {currentPlayer}.");
                        playersChecked++;
                    }

                    var fightersForNextPlayer = availableFighters
                        .Where(m => m.playerID == currentPlayer && !usedFighters.Contains(m))
                        .ToList();

                    if (fightersForNextPlayer.Count == 0)
                    {
                        playersChecked++;
                    }
                    else
                    {
                        playersChecked = 0;
                    }

                    fightersLeft = availableFighters.Any(m => !usedFighters.Contains(m));
                }

                // Resolve attacks after all models have fought
                ResolveAttacks();
                attackActions.Clear(); // Clear attacks for the next round
            }

            currentInitiativeRound--;
            UpdateInitiativeUI();
            Debug.Log($"FightController: Initiative Round decremented to {currentInitiativeRound}.");
            yield return null;
        }

        // After all initiative rounds are resolved
        Debug.Log("FightController: All initiative rounds resolved for this fight.");
        gameController.ShowPlayerErrorMessage("Fight resolved.");
        Debug.Log("FightController: Fight resolved.");

        activeFights.Remove(selectedFight);
        Debug.Log($"FightController: Fight removed from activeFights. Remaining fights: {activeFights.Count}.");

        if (activeFights.Count > 0)
        {
            Debug.Log("FightController: More fights remain to be resolved.");
            fightPhaseState = FightPhaseState.SelectingFight;
            gameController.ShowPlayerErrorMessage("Select the next fight to resolve.");
            selectedFight = null;
        }
        else
        {
            Debug.Log("FightController: No more fights to resolve.");
            EndFightPhase();
        }

        if (initiativeText != null)
        {
            initiativeText.gameObject.SetActive(false);
            Debug.Log("FightController: Initiative text hidden after fight resolution.");
        }

        yield break;
    }

    private IEnumerator WaitForModelSelectionForAttack(int playerID)
    {
        selectedModelForPileInMove = null;
        Debug.Log("FightController: Waiting for model selection for attack.");

        while (selectedModelForPileInMove == null && fightPhaseState == FightPhaseState.Attacks)
        {
            if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
                Ray ray = Camera.main.ScreenPointToRay(screenCenter);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 2000f))
                {
                    Debug.Log($"FightController: Raycast hit: {hit.transform.name}");

                    ModelController clickedModel = hit.transform.GetComponent<ModelController>();
                    if (clickedModel != null && availableFighters.Contains(clickedModel) && clickedModel.playerID == playerID && !clickedModel.HasFought())
                    {
                        selectedModelForPileInMove = clickedModel;
                        Debug.Log($"FightController: Model {clickedModel.gameObject.name} selected for attacks.");

                        gameController.SelectModel(clickedModel);

                        // Calculate remaining attacks
                        int remainingAttacks = clickedModel.attacks;
                        if (clickedModel.HasCharged())
                        {
                            remainingAttacks += 1;
                        }
                        int meleeWeapons = clickedModel.GetMeleeWeapons().Count;
                        if (meleeWeapons >= 2)
                        {
                            remainingAttacks += 1;
                        }
                        clickedModel.SetRemainingAttacks(remainingAttacks);

                        // Show remaining attacks
                        UpdateRemainingAttacksUI(remainingAttacks);

                        // Show melee weapons UI
                        gameController.weaponUIController.ShowMeleeWeaponOptions(clickedModel);

                        break; // Exit the loop
                    }
                    else
                    {
                        gameController.ShowPlayerErrorMessage("Select one of your available fighters to attack.");
                        Debug.Log("FightController: Invalid model selected for attack.");
                    }
                }
            }
            yield return null;
        }
    }

    private IEnumerator HandleAttacks(ModelController model)
    {
        Debug.Log($"FightController: HandleAttacks started for model {model.gameObject.name}.");

        while (model.GetRemainingAttacks() > 0)
        {
            gameController.ShowPlayerErrorMessage($"Player {currentPlayer}, select a target for {model.gameObject.name}. Remaining Attacks: {model.GetRemainingAttacks()}");

            bool targetSelected = false;

            while (!targetSelected)
            {
                if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
                    Ray ray = Camera.main.ScreenPointToRay(screenCenter);
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit, 2000f))
                    {
                        ModelController targetModel = hit.transform.GetComponent<ModelController>();
                        if (targetModel != null && targetModel.playerID != currentPlayer)
                        {
                            if (model.IsColliding(targetModel))
                            {
                                if (gameController.weaponUIController.selectedWeapon != null)
                                {
                                    // Record the attack
                                    WeaponController weapon = gameController.weaponUIController.selectedWeapon;
                                    attackActions.Add(new AttackAction(model, targetModel, weapon));
                                    Debug.Log($"Attack recorded: {model.gameObject.name} attacks {targetModel.gameObject.name} with {weapon.weaponName}.");

                                    model.DecrementRemainingAttacks();
                                    UpdateRemainingAttacksUI(model.GetRemainingAttacks());

                                    if (model.GetRemainingAttacks() == 0)
                                    {
                                        // Mark weapons as used
                                        foreach (var meleeWeapon in model.GetMeleeWeapons())
                                        {
                                            gameController.shootingController.MarkWeaponAsUsed(meleeWeapon);
                                        }

                                        // Deselect model and hide UI
                                        gameController.DeselectAllModels();
                                        model.SetHasFought(true);
                                        remainingAttacksText.gameObject.SetActive(false);
                                        Debug.Log($"Model {model.gameObject.name} has finished attacking.");
                                        yield break;
                                    }

                                    targetSelected = true;
                                    break;
                                }
                                else
                                {
                                    gameController.ShowPlayerErrorMessage("Select a melee weapon to attack with.");
                                    Debug.Log("No melee weapon selected.");
                                    break;
                                }
                            }
                            else
                            {
                                gameController.ShowPlayerErrorMessage("Target is not in base contact.");
                                Debug.Log("Target is not colliding with attacker.");
                            }
                        }
                        else
                        {
                            gameController.ShowPlayerErrorMessage("Select a valid enemy target.");
                            Debug.Log("Invalid target selected.");
                        }
                    }
                }
                yield return null;
            }
        }
    }

    private void ResolveAttacks()
    {
        Debug.Log("Resolving attacks:");
        foreach (var attack in attackActions)
        {
            Debug.Log($"{attack.attacker.gameObject.name} attacks {attack.target.gameObject.name} with {attack.weapon.weaponName}");

            // Roll to hit
            int successfulHits = 0;
            for (int i = 0; i < attack.weapon.numberOfShots; i++)
            {
                int roll = DiceRoller.RollD6();
                Debug.Log($"Rolling to hit: {roll}");
                if (roll >= attack.attacker.weaponSkill)
                {
                    successfulHits++;
                }
            }

            if (successfulHits == 0)
            {
                Debug.Log("All attacks missed.");
                continue;
            }

            // Roll to wound
            int successfulWounds = 0;
            for (int i = 0; i < successfulHits; i++)
            {
                int roll = DiceRoller.RollD6();
                Debug.Log($"Rolling to wound: {roll}");
                if (IsWoundSuccessful(roll, attack.attacker.strength, attack.target.toughness))
                {
                    successfulWounds++;
                }
            }

            if (successfulWounds == 0)
            {
                Debug.Log("No wounds inflicted.");
                continue;
            }

            // Roll for armor save
            int damageInflicted = 0;
            for (int i = 0; i < successfulWounds; i++)
            {
                int roll = DiceRoller.RollD6();
                int saveRollRequired = Mathf.Min(attack.target.armourSave - attack.weapon.armourPiercing, attack.target.invulnerabilitySave);
                Debug.Log($"Rolling for armor save: {roll}, required: {saveRollRequired}");
                if (roll < saveRollRequired)
                {
                    damageInflicted += attack.weapon.damage;
                }
            }

            // Apply damage
            if (damageInflicted > 0)
            {
                Debug.Log($"{attack.target.gameObject.name} takes {damageInflicted} damage!");
                attack.target.TakeDamage(damageInflicted);
            }
            else
            {
                Debug.Log("All wounds were saved!");
            }
        }
    }

    private bool IsWoundSuccessful(int roll, int attackerStrength, int targetToughness)
    {
        if (attackerStrength >= 2 * targetToughness) return roll >= 2;
        if (attackerStrength > targetToughness) return roll >= 3;
        if (attackerStrength == targetToughness) return roll >= 4;
        if (attackerStrength < targetToughness) return roll >= 5;
        if (attackerStrength * 2 <= targetToughness) return roll >= 6;
        return false;
    }

    private void UpdateRemainingAttacksUI(int remainingAttacks)
    {
        if (remainingAttacksText != null)
        {
            remainingAttacksText.text = "Remaining Attacks: " + remainingAttacks;
            remainingAttacksText.gameObject.SetActive(true);
            Debug.Log($"Remaining Attacks UI updated: {remainingAttacks}");
        }
    }

    /// <summary>
    /// Waits for the player to select a model to perform a pile in move.
    /// </summary>
    private IEnumerator WaitForModelSelection()
    {
        selectedModelForPileInMove = null;
        Debug.Log("FightController: Waiting for model selection for pile in move.");

        while (selectedModelForPileInMove == null && fightPhaseState == FightPhaseState.PileInMove)
        {
            if (Input.GetMouseButtonDown(0) && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 0);
                Ray ray = Camera.main.ScreenPointToRay(screenCenter);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 2000f))
                {
                    Debug.Log($"FightController: Raycast hit: {hit.transform.name}");

                    ModelController clickedModel = hit.transform.GetComponent<ModelController>();
                    if (clickedModel != null && availableFighters.Contains(clickedModel) && clickedModel.playerID == currentPlayer)
                    {
                        selectedModelForPileInMove = clickedModel;
                        Debug.Log($"FightController: Model {clickedModel.gameObject.name} selected for pile in move.");
                        // Show the "Confirm Pile In Move" button immediately after selection
                        confirmPileInMoveButton.gameObject.SetActive(true);
                        gameController.ShowPlayerErrorMessage("Click 'Confirm Pile In Move' to finalize your action.");
                        Debug.Log("FightController: Confirm Pile In Move button displayed.");

                        // Notify GameController about the selected model
                        gameController.SelectModel(clickedModel);
                        Debug.Log($"FightController: GameController notified of model {clickedModel.gameObject.name} selection.");
                    }
                    else
                    {
                        gameController.ShowPlayerErrorMessage("Select one of your available fighters to perform a pile in move.");
                        Debug.Log("FightController: Invalid model selected for pile in move.");
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
        Debug.Log($"FightController: HandlePileInMove started for model {model.gameObject.name}.");
        gameController.ShowPlayerErrorMessage($"Player {currentPlayer}, perform a pile in move with {model.gameObject.name}.");

        // Display pile in move range indicator at ground level (y = 60)
        currentPileInMoveIndicator = Instantiate(pileInMoveRangeIndicatorPrefab, new Vector3(model.transform.position.x, 60f, model.transform.position.z), Quaternion.identity);
        float pileInMoveDistance = 3 * GameConstants.MOVEMENT_CONVERSION_FACTOR;
        currentPileInMoveIndicator.transform.localScale = new Vector3(pileInMoveDistance * 2, 0.01f, pileInMoveDistance * 2);
        Renderer renderer = currentPileInMoveIndicator.GetComponent<Renderer>();
        renderer.material.color = model.GetFactionColor();
        renderer.material.color = new Color(renderer.material.color.r, renderer.material.color.g, renderer.material.color.b, 0.3f);
        Debug.Log($"FightController: Pile in move range indicator instantiated at {currentPileInMoveIndicator.transform.position} with scale {currentPileInMoveIndicator.transform.localScale}.");

        bool moveValid = false;
        Vector3 targetPosition = Vector3.zero;

        // Wait for player to click on a valid location or confirm without moving
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
                        float distance = Vector3.Distance(new Vector3(model.transform.position.x, 60f, model.transform.position.z), new Vector3(clickedPosition.x, 60f, clickedPosition.z));

                        Debug.Log($"FightController: Player clicked at {clickedPosition} with distance {distance}.");

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
                                        Debug.LogError("FightController: Model or enemy does not have a collider.");
                                        continue;
                                    }

                                    Bounds modelBounds = modelCollider.bounds;
                                    Vector3 newPosition = new Vector3(clickedPosition.x, model.transform.position.y, clickedPosition.z);
                                    Bounds newModelBounds = new Bounds(newPosition, modelBounds.size);

                                    if (newModelBounds.Intersects(enemyCollider.bounds))
                                    {
                                        collidesWithEnemy = true;
                                        Debug.Log($"FightController: New position {newPosition} intersects with enemy {enemy.gameObject.name}.");
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
                                Debug.Log($"FightController: Pile in move successful. Model {model.gameObject.name} moved to {targetPosition}.");

                                // Notify GameController that the model has moved
                                // This may be necessary depending on how GameController handles model states
                            }
                            else
                            {
                                gameController.ShowPlayerErrorMessage("Invalid pile in move! Must collide with at least one enemy model.");
                                Debug.Log("FightController: Move does not collide with any enemy models in the fight.");
                            }
                        }
                        else
                        {
                            gameController.ShowPlayerErrorMessage("Pile in move out of range.");
                            Debug.Log("FightController: Pile in move attempted out of range.");
                        }
                    }
                    else
                    {
                        Debug.Log("FightController: Clicked object is not tagged as 'Ground'.");
                    }
                }
            }
            yield return null;
        }

        // Destroy the pile in move indicator
        if (currentPileInMoveIndicator != null)
        {
            Destroy(currentPileInMoveIndicator);
            Debug.Log("FightController: Pile in move range indicator destroyed.");
            currentPileInMoveIndicator = null;
        }

        // Wait until the player confirms the move
        while (confirmPileInMoveButton.gameObject.activeSelf && fightPhaseState == FightPhaseState.PileInMove)
        {
            yield return null;
        }

        // Reset the fight phase state
        fightPhaseState = FightPhaseState.ResolvingInitiativeRound;
        Debug.Log("FightController: FightPhaseState set to ResolvingInitiativeRound after pile in move.");

        // Deselect the model in GameController to allow rotation
        gameController.DeselectAllModels();
        Debug.Log("FightController: GameController deselected all models after pile in move.");
    }

    /// <summary>
    /// Handles the Confirm Pile In Move button click.
    /// </summary>
    private void ConfirmPileInMove()
    {
        Debug.Log("FightController: ConfirmPileInMove button clicked.");
        confirmPileInMoveButton.gameObject.SetActive(false);
        gameController.ShowPlayerErrorMessage("Pile in move confirmed.");
        Debug.Log("FightController: Pile in move confirmed.");

        if (selectedModelForPileInMove != null)
        {
            // If a pile in move was performed, it has already been handled in HandlePileInMove
            // So just mark the fighter as used
            usedFighters.Add(selectedModelForPileInMove);
            Debug.Log($"FightController: Model {selectedModelForPileInMove.gameObject.name} confirmed without additional move.");
            selectedModelForPileInMove = null;
        }
        else
        {
            // If no move was performed, still mark the fighter as used
            // Find the current player’s available fighter to mark as used
            ModelController fighterToMark = availableFighters.FirstOrDefault(m => m.playerID == currentPlayer && !usedFighters.Contains(m));
            if (fighterToMark != null)
            {
                usedFighters.Add(fighterToMark);
                Debug.Log($"FightController: Fighter {fighterToMark.gameObject.name} marked as used without performing pile in move.");
            }
            else
            {
                Debug.LogWarning("FightController: No available fighter found to mark as used.");
            }
        }

        // Proceed to resolve the next fighter
        fightPhaseState = FightPhaseState.ResolvingInitiativeRound;

        // Deselect the model in GameController to allow rotation
        gameController.DeselectAllModels();
        Debug.Log("FightController: GameController deselected all models after confirming pile in move.");
    }

    /// <summary>
    /// Updates the initiative round UI.
    /// </summary>
    private void UpdateInitiativeUI()
    {
        if (initiativeText != null)
        {
            initiativeText.text = "Initiative Round: " + currentInitiativeRound;
            Debug.Log($"FightController: Initiative UI updated to Round {currentInitiativeRound}.");
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
            Debug.Log("FightController: Fight resolution coroutine stopped.");
        }

        fightPhaseState = FightPhaseState.None;
        selectedFight = null;
        initiativeText.gameObject.SetActive(false); // Hide initiative text
        gameController.NextPhase();
        Debug.Log("FightController: Fight phase ended and NextPhase called.");
    }

    /// <summary>
    /// Finds all ongoing fights by grouping colliding models.
    /// </summary>
    public void FindAllFights()
    {
        Debug.Log("FightController: FindAllFights called.");
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
                Debug.Log($"FightController: Checking collision between {allModels[i].gameObject.name} and {allModels[j].gameObject.name}.");
                if (allModels[i].IsColliding(allModels[j]))
                {
                    Debug.Log($"FightController: {allModels[i].gameObject.name} and {allModels[j].gameObject.name} are colliding.");
                    Union(allModels[i], allModels[j], parent);
                }
                else
                {
                    Debug.Log($"FightController: {allModels[i].gameObject.name} and {allModels[j].gameObject.name} are not colliding.");
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
                Debug.Log($"FightController: Fight identified with {group.Count} models.");
            }
        }

        if (activeFights.Count == 0)
        {
            Debug.Log("FightController: No fights detected. Ending Fight phase.");
            EndFightPhase();
        }
        else
        {
            Debug.Log($"FightController: Total fights identified: {activeFights.Count}.");
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
            Debug.Log($"FightController: Unioned {a.gameObject.name} and {b.gameObject.name} under {rootA.gameObject.name}.");
        }
    }

    /// <summary>
    /// Prompts the current player to select a fight.
    /// </summary>
    private void PromptPlayerToSelectFight()
    {
        int currentPlayerId = gameController.GetCurrentPlayer();
        Debug.Log($"FightController: Player {currentPlayerId}, select a fight to resolve.");
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
                Debug.Log($"FightController: Raycast hit: {hit.transform.name}");

                ModelController clickedModel = hit.transform.GetComponent<ModelController>();
                if (clickedModel != null)
                {
                    Fight fight = activeFights.FirstOrDefault(f => f.participants.Contains(clickedModel));
                    if (fight != null)
                    {
                        // Check if the fight includes models from existing players
                        if (fight.participants.Any(m => m.playerID == gameController.GetCurrentPlayer()))
                        {
                            SelectFight(fight);
                            Debug.Log("FightController: Fight selected and highlighted.");
                        }
                        else
                        {
                            gameController.ShowPlayerErrorMessage("You must select a fight that includes one of your own models.");
                            Debug.Log("FightController: Selected fight does not include any of your models.");
                        }
                    }
                    else
                    {
                        Debug.Log("FightController: Clicked model is not part of any active fight.");
                    }
                }
                else
                {
                    Debug.Log("FightController: Clicked object does not have a ModelController.");
                }
            }
            else
            {
                Debug.Log("FightController: Raycast did not hit any object.");
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
            Debug.Log("FightController: Fight deselected via Escape key.");
        }
    }

    /// <summary>
    /// Selects a fight and highlights its models.
    /// </summary>
    private void SelectFight(Fight fight)
    {
        Debug.Log("FightController: Selecting a fight.");

        if (selectedFight != null)
        {
            DeselectCurrentFight();
        }

        selectedFight = fight;
        HighlightFightModels(fight);
        fightButton.gameObject.SetActive(true);
        Debug.Log("FightController: Fight selected and Fight button displayed.");
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
            Debug.Log("FightController: Fight deselected and Fight button hidden.");

            // Deselect the model in GameController
            gameController.DeselectAllModels();
            Debug.Log("FightController: GameController deselected all models after fight deselection.");
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
                Debug.Log($"FightController: Outline enabled for {model.gameObject.name}.");
            }
            else
            {
                Debug.LogWarning($"FightController: Model {model.gameObject.name} does not have an Outline component.");
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
                Debug.Log($"FightController: Outline disabled for {model.gameObject.name}.");
            }
            else
            {
                Debug.LogWarning($"FightController: Model {model.gameObject.name} does not have an Outline component.");
            }
        }
    }
    
        /// <summary>
    /// Added empty HandleAttackSelection method to avoid errors.
    /// </summary>
    private void HandleAttackSelection()
    {
        // Input handling during Attacks phase is managed in the coroutines
        // This method can be used if additional input handling is needed
    }					
}

