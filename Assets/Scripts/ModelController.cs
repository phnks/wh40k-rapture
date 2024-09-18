using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Outline))]
public class ModelController : MonoBehaviour
{
    public enum Faction
    {
        AdeptusMechanicus,
        DeathGuard
    }

    public int playerID; // ID of the player who owns this model (1 or 2)
    public float movementRange = 6f; // Maximum distance the model can move in a turn
    private float remainingMovement; // Remaining movement allowed in the current phase
    public Faction faction; // Faction of the model

    private bool isSelected = false; // Whether this model is currently selected
    private NavMeshAgent agent; // Reference to the NavMeshAgent component
    private Outline outline; // Reference to the Outline component
    private GameObject rangeIndicator; // Reference to the movement range visual indicator
    private GameObject moveIndicator; // Visual indicator for the original position after the first move
    private Vector3 startPosition; // The original position at the start of the phase

    private const float MOVE_SPEED = 10000f; // Movement speed is a constant for all objects
    private const float ROTATION_SPEED = 6400f; // Rotation speed (updated for faster rotation)

    // New Attributes for Shooting Mechanics
    public int ballisticSkill; // Ballistic Skill for shooting (1 to 6)
    public int toughness; // Toughness of the model
    public int armourSave; // Armour save value
    public int invulnerabilitySave; // Invulnerability save value
    public int wounds; // Current wounds of the model
    private int originalWounds; // Store the original wounds for reset purposes
    private bool hasMoved; // Track if this model has moved during the Movement phase
    private List<WeaponController> weapons = new List<WeaponController>(); // Weapons the model has

    void Start()
    {
        outline = GetComponent<Outline>();
        outline.enabled = false; // Disable outline by default
        remainingMovement = movementRange * GameConstants.MOVEMENT_CONVERSION_FACTOR; // Convert range to world space distance
        startPosition = transform.position; // Store the starting position at the start of the phase
        originalWounds = wounds; // Store the original wounds
        UpdateColors(); // Set initial colors based on faction
    }

    public void SelectModel()
    {
        isSelected = true;
        outline.enabled = true; // Enable outline when selected
        ShowMovementRange(); // Show the movement range when the model is selected
        Debug.Log("Model selected: " + gameObject.name);
    }

    public void DeselectModel()
    {
        isSelected = false;
        outline.enabled = false; // Disable outline when deselected
        HideMovementRange(); // Hide the movement range when the model is deselected
        Debug.Log("Model deselected: " + gameObject.name);
    }

    private void ShowMovementRange()
    {
        // **CHANGE: Show movement range only during the Movement Phase**
        if (GameController.Instance.GetCurrentPhase() != GameController.Phase.Movement)
        {
            return;
        }

        if (rangeIndicator == null)
        {
            rangeIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rangeIndicator.transform.position = new Vector3(transform.position.x, 58.0f, transform.position.z); // Set height to 58
            float radius = remainingMovement; // Use world space distance directly
            rangeIndicator.transform.localScale = new Vector3(radius * 2, 0.01f, radius * 2); // Properly scale to match world space distance

            Material transparentMaterial = new Material(Shader.Find("Standard"));
            transparentMaterial.color = new Color(0, 1, 0, 0.3f); // Green with transparency
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
            rangeIndicator.SetActive(true); // Reactivate if already created
        }

        UpdateColors(); // Ensure the color matches the faction when shown
    }

    private void HideMovementRange()
    {
        if (rangeIndicator != null)
        {
            rangeIndicator.SetActive(false); // Deactivate the range indicator
        }
    }

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

    public void MoveTo(Vector3 targetPosition)
    {
        targetPosition.y = transform.position.y; // Ensure movement stays on the XZ plane

        float distance = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), new Vector3(targetPosition.x, 0, targetPosition.z));
        float distanceToStart = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z), 
                                                 new Vector3(startPosition.x, 0, startPosition.z));
        float newDistanceToStart = Vector3.Distance(new Vector3(targetPosition.x, 0, targetPosition.z), 
                                                    new Vector3(startPosition.x, 0, startPosition.z));

        float potentialRemainingMovement = remainingMovement;

        if (newDistanceToStart < distanceToStart)
        {
            float distanceDiff = distanceToStart - newDistanceToStart;
            potentialRemainingMovement = Mathf.Min(potentialRemainingMovement + distanceDiff, movementRange * GameConstants.MOVEMENT_CONVERSION_FACTOR);
        }

        if (distance <= potentialRemainingMovement)
        {
            if (newDistanceToStart < distanceToStart)
            {
                float distanceDiff = distanceToStart - newDistanceToStart;
                remainingMovement = Mathf.Min(remainingMovement + distanceDiff, movementRange * GameConstants.MOVEMENT_CONVERSION_FACTOR);
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

    private System.Collections.IEnumerator MoveToPosition(Vector3 targetPosition)
    {
        while (Vector3.Distance(transform.position, targetPosition) > 0.1f)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, MOVE_SPEED * Time.deltaTime);
            yield return null; // Wait for the next frame
        }
        transform.position = targetPosition; // Snap to the exact position at the end
    }

    private void UpdateMovementRangeIndicator()
    {
        if (rangeIndicator != null)
        {
            float radius = remainingMovement; // Use world space distance directly
            rangeIndicator.transform.position = new Vector3(transform.position.x, 58.0f, transform.position.z); // Set height to 58
            rangeIndicator.transform.localScale = new Vector3(radius * 2, 0.01f, radius * 2); // Update the size
        }
    }

    private void UpdateColors()
    {
        Color factionColor = faction == Faction.AdeptusMechanicus ? Color.red : Color.green;

        outline.OutlineColor = factionColor;

        if (rangeIndicator != null)
        {
            Renderer rangeRenderer = rangeIndicator.GetComponent<Renderer>();
            rangeRenderer.material.color = factionColor;
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

    public void RotateModel(float rotationAmount)
    {
        transform.Rotate(Vector3.up, rotationAmount * ROTATION_SPEED * Time.deltaTime);
    }

    public float GetRemainingMovement()
    {
        return remainingMovement;
    }

    public Vector3 GetStartPosition()
    {
        return startPosition;
    }

    public void DeleteMoveIndicator()
    {
        if (moveIndicator != null)
        {
            Destroy(moveIndicator); // Delete the move indicator at the end of the phase
            moveIndicator = null;
        }
    }

    public void AddWeapon(WeaponController weapon)
    {
        weapons.Add(weapon);
    }

    public bool HasMoved()
    {
        return hasMoved;
    }

    public void ResetMovement()
    {
        hasMoved = false;
    }

    public bool IsDestroyed()
    {
        return wounds <= 0;
    }

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

