using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class FishAI : MonoBehaviour
{
    public enum State { Wander, Chase, Flee }
    public State currentState = State.Wander;

    [Header("Movement Settings")]
    public float moveSpeed = 4f; // Constant speed
    public float turnSpeed = 200f; // Degrees per second
    public float wanderRadius = 2f;
    public float wanderDistance = 3f;
    public float wanderJitter = 1f;

    [Header("Behavior Settings")]
    public float chaseRadius = 6f;
    public float fleeRadius = 4.5f;
    public float separationRadius = 2f;
    
    // New: Chase Limits to prevent sticking
    public float maxChaseTime = 5f;       // Give up after 5 seconds
    public float chaseCooldownTime = 3f;  // Ignore player for 3 seconds
    private float currentChaseTimer = 0f;
    private float currentCooldownTimer = 0f;

    // New: Random "Leave" Behavior
    // Instead of wandering randomly in circles, they will sometimes pick a "Leave" direction
    private float leaveTimer = 0f;
    private Vector2 leaveDirection = Vector2.zero;
    private bool isLeaving = false;
    private float lifeTime = 0f;

    [Header("Obstacle Avoidance")]
    public bool stayOnScreen = false; // Default false (enabled for Golden Fish only)
    public LayerMask obstacleMask;
    public float avoidDistance = 3f;
    [Tooltip("Layers to include in separation calculations (e.g. Fish, Enemy)")]
    public LayerMask separationMask = -1; // Default to Everything

    [Header("Visuals")]
    public Transform graphicsTransform;
    public float tailSwaySpeed = 8f;
    public float tailSwayAmount = 10f;

    private Rigidbody2D rb;
    private Transform player;
    private Transform chaseTarget; // Can be Player or another Fish
    private GameManager cachedGameManager; // Cached reference
    private Fish fishData;
    private Vector2 currentDirection;
    private Vector2 wanderTarget;
    private System.Collections.Generic.List<Collider2D> neighborBuffer = new System.Collections.Generic.List<Collider2D>(10);

    private Vector2 cachedAvoidanceDir;
    private Vector2 cachedSeparationDir;
    private int aiTickOffset;
    private float randomOffset; // Small random offset for wobble

    // Conflicting script handling
    void Awake()
    {
        // Removed destructive logic. We no longer destroy FishMovement automatically.
        // If both are present, we rely on the manager/setup to enable the correct one.

        MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
        foreach (var script in scripts)
        {
            if (script.GetType().Name == "StateController")
            {
                Destroy(script);
            }
        }

        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.angularDamping = 0f;
        rb.linearDamping = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate; // Smooth movement

        if (graphicsTransform == null)
            graphicsTransform = transform.Find("PlayerGraphics") ?? transform;

        fishData = GetComponent<Fish>();
        
        // Initialize wander target
        float theta = Random.value * 2 * Mathf.PI;
        wanderTarget = new Vector2(Mathf.Cos(theta), Mathf.Sin(theta)) * wanderRadius;
    }

    void Start()
    {
        player = GameManager.instance?.playerGameObject?.transform;
        // Give initial direction
        currentDirection = transform.right;
        if (currentDirection == Vector2.zero) currentDirection = Vector2.right;

        // Randomize tick offset to distribute load across frames
        aiTickOffset = Random.Range(0, 5);
    }

    void FixedUpdate()
    {
        if (player == null && GameManager.instance?.playerGameObject != null)
            player = GameManager.instance.playerGameObject.transform;

        UpdateState();

        // New: Handle "Leaving" state logic (randomly decide to swim away)
        HandleLeavingLogic();

        // 1. Determine Desired Direction based on State
        Vector2 targetDir = currentDirection;

        // SCHOOLING OVERRIDE (If part of a school, and just wandering, follow the school)
        if (fishData != null && fishData.school != null && currentState == State.Wander)
        {
             // Add organic drift to the formation so it's not a rigid grid
             // Using InstanceID as a random seed for unique wobble per fish
             float wobbleSpeed = 2.0f;
             float wobbleAmount = 0.5f;
             float time = Time.fixedTime * wobbleSpeed + GetInstanceID();
             Vector2 organicDrift = new Vector2(Mathf.Sin(time), Mathf.Cos(time * 0.7f)) * wobbleAmount;

             Vector2 schoolTarget = fishData.school.CurrentDestination + fishData.formationOffset + organicDrift;
             Vector2 toTarget = schoolTarget - (Vector2)transform.position;
             
             // Prevent jitter when very close to target
             if (toTarget.sqrMagnitude < 0.25f) // 0.5f distance squared
             {
                 // Just drift with current direction to avoid snapping
                 targetDir = currentDirection;
             }
             else
             {
                 targetDir = toTarget.normalized;
             }
        }
        // If we are "leaving", override normal behavior unless we are fleeing/chasing intensely
        else if (isLeaving && currentState == State.Wander)
        {
            targetDir = leaveDirection;
        }
        else
        {
            switch (currentState)
            {
                case State.Wander:
                    targetDir = GetWanderDirection();
                    break;
                case State.Chase:
                    if (chaseTarget != null) 
                        targetDir = (chaseTarget.position - transform.position).normalized;
                    else if (player != null) 
                        targetDir = (player.position - transform.position).normalized;
                    break;
                case State.Flee:
                    if (player) targetDir = (transform.position - player.position).normalized;
                    break;
            }
        }

        // OPTIMIZATION: Throttle expensive checks (Raycasts & OverlapCircle)
        // Run only once every 5 physics frames (approx 10 times per second instead of 50)
        if ((Time.frameCount + aiTickOffset) % 5 == 0)
        {
            cachedAvoidanceDir = GetAvoidanceDirection();
            cachedSeparationDir = GetSeparationDirection();
        }

        // 2. Obstacle Avoidance (Overrides state)
        // Weighted blending to prevent snapping
        if (cachedAvoidanceDir != Vector2.zero)
        {
            // Add avoidance force rather than hard Lerp
            targetDir += cachedAvoidanceDir * 3.0f; 
        }

        // 3. Separation (Nudge away from neighbors)
        if (cachedSeparationDir != Vector2.zero)
        {
             targetDir += cachedSeparationDir * 1.5f;
        }

        // 4. Boundary Avoidance (If enabled)
        if (stayOnScreen)
        {
            Vector2 boundsDir = GetBoundaryAvoidanceDirection();
            if (boundsDir != Vector2.zero)
            {
                 // Add strong force (Stronger than obstacle avoidance 3.0f) to ensure we stay in bounds
                 targetDir += boundsDir * 5.0f;
            }
        }

        // REALISM TWEAK: Horizontal Bias
        // Fish naturally swim horizontally. We dampen vertical movement slightly in Wander state.
        if (currentState == State.Wander)
        {
            targetDir.y *= 0.6f; // Flatten vertical component
        }
        
        // Normalize once after all forces are applied
        targetDir = targetDir.normalized;

        // Ensure targetDir is valid
        if (targetDir == Vector2.zero) targetDir = currentDirection;

        // 4. Rotate Current Direction towards Target Direction
        if (targetDir != Vector2.zero)
        {
            // FIX: Use RotateTowards with actual turnSpeed to prevent snapping/jittering
            // "turn left right left right crazily" fix.
            float step = turnSpeed * Mathf.Deg2Rad * Time.fixedDeltaTime;
            currentDirection = Vector3.RotateTowards(currentDirection, targetDir, step, 0f).normalized;
        }
        
        // Safety check
        if (currentDirection == Vector2.zero) currentDirection = transform.right;

        // 5. Apply Velocity
        // ALWAYS move at moveSpeed (or slightly faster when fleeing)
        float currentSpeed = moveSpeed;
        if (currentState == State.Flee) currentSpeed *= 1.5f;
        if (currentState == State.Chase) currentSpeed *= 1.2f;

        rb.linearVelocity = currentDirection * currentSpeed;

        // 6. Physics Rotation (Standard 2D Flipping)
        // User Request: "make it swim normamlly not special swim shit"
        // Standard behavior: No Rotation, Flip X based on direction.
        
        rb.rotation = 0f;

        // Flip X if moving left
        Vector3 scale = transform.localScale;
        
        // FIX: Increased Deadzone to prevent rapid flipping (was 0.1f)
        float flipThreshold = 0.4f; 

        if (currentDirection.x < -flipThreshold)
        {
            // Moving Left -> Scale X should be negative (assuming default sprite faces right)
            scale.x = -Mathf.Abs(scale.x);
        }
        else if (currentDirection.x > flipThreshold)
        {
            // Moving Right -> Scale X should be positive
            scale.x = Mathf.Abs(scale.x);
        }
        
        // Ensure Y is always positive (Upright)
        scale.y = Mathf.Abs(scale.y);
            
        transform.localScale = scale;
    }

    void UpdateState()
    {
        if (player == null || fishData == null) return;

        // Decrease Cooldown
        if (currentCooldownTimer > 0f)
        {
            currentCooldownTimer -= Time.fixedDeltaTime;
            currentState = State.Wander; // Force wander during cooldown
            chaseTarget = null;
            return;
        }

        float distToPlayer = Vector2.Distance(transform.position, player.position);
        int playerLevel = GameManager.PlayerLevel;

        // 1. FLEE PLAYER (Priority: Survival)
        if (distToPlayer < fleeRadius && playerLevel > fishData.Level)
        {
            currentState = State.Flee;
            chaseTarget = null;
            currentChaseTimer = 0f;

            // Break Formation!
            if (fishData.school != null)
            {
                fishData.school = null;
                fishData.formationOffset = Vector2.zero;
            }
            return;
        }

        // 2. CHASE PLAYER (Priority: Aggression)
        if (distToPlayer < chaseRadius && playerLevel <= fishData.Level)
        {
            currentState = State.Chase;
            chaseTarget = player;
            
            // Increment Chase Timer
            currentChaseTimer += Time.fixedDeltaTime;
            if (currentChaseTimer >= maxChaseTime)
            {
                // Give Up!
                currentCooldownTimer = chaseCooldownTime;
                currentChaseTimer = 0f;
                currentState = State.Wander;
                chaseTarget = null;
            }
            return;
        }

        // 3. CHASE OTHER FISH (Priority: Hunger)
        // If not interacting with player, look for food.
        Fish nearestFood = null;
        float nearestDist = chaseRadius; // Only look within chase radius

        // Optimization: Only scan every few frames or if we are wandering
        foreach (var f in Fish.AllFish)
        {
            if (f == null || f == fishData) continue;
            
            // Can I eat it?
            if (f.Level < fishData.Level)
            {
                float d = Vector2.Distance(transform.position, f.transform.position);
                if (d < nearestDist)
                {
                    nearestDist = d;
                    nearestFood = f;
                }
            }
        }

        if (nearestFood != null)
        {
            currentState = State.Chase;
            chaseTarget = nearestFood.transform;
            currentChaseTimer = 0f; // Reset timer as switching targets resets interest? 
            return;
        }

        // 4. WANDER
        currentState = State.Wander;
        chaseTarget = null;
        currentChaseTimer = 0f;
    }

    void HandleLeavingLogic()
    {
        lifeTime += Time.fixedDeltaTime;

        // Force leave for old predators (High Level)
        // If they have been around for > 15 seconds, they should leave.
        // This ensures they don't stick around forever and get stuck.
        if (fishData != null && GameManager.PlayerLevel < fishData.Level && lifeTime > 15f)
        {
             if (!isLeaving) 
             {
                 StartLeaving();
                 leaveTimer = 999f; // Effectively permanent until destroyed
             }
             // Ensure they don't stop leaving
             if (leaveTimer < 100f) leaveTimer = 999f;
             return; 
        }

        // Only consider leaving if we are just wandering
        if (currentState != State.Wander)
        {
            isLeaving = false;
            return;
        }

        if (isLeaving)
        {
            leaveTimer -= Time.fixedDeltaTime;
            if (leaveTimer <= 0)
            {
                // Stop leaving
                isLeaving = false;
            }
        }
        else
        {
            // Random chance to start leaving (1% chance per fixed frame -> ~50% chance per second)
            if (Random.value < 0.005f) 
            {
                StartLeaving();
            }
        }
    }

    void StartLeaving()
    {
        isLeaving = true;
        leaveTimer = Random.Range(3f, 8f); // Leave for 3-8 seconds
        
        // Pick a direction AWAY from the center (0,0) or just a random far direction
        // Let's pick a random direction that is roughly away from the player to look like they are "done" with you
        if (player != null)
        {
            Vector2 awayFromPlayer = ((Vector2)transform.position - (Vector2)player.position).normalized;
            // Add some randomness so it's not a perfect straight line away
            float angle = Random.Range(-45f, 45f);
            leaveDirection = Quaternion.Euler(0, 0, angle) * awayFromPlayer;
        }
        else
        {
             leaveDirection = Random.insideUnitCircle.normalized;
        }
    }

    Vector2 GetWanderDirection()
    {
        // Reynolds' Wander
        float jitter = wanderJitter * Time.fixedDeltaTime * 60f;
        wanderTarget += new Vector2(Random.Range(-1f, 1f) * jitter, Random.Range(-1f, 1f) * jitter);
        wanderTarget = wanderTarget.normalized * wanderRadius;

        Vector2 targetLocal = wanderTarget + new Vector2(wanderDistance, 0);
        Vector2 targetWorld = transform.TransformPoint(targetLocal);
        
        return (targetWorld - (Vector2)transform.position).normalized;
    }

    Vector2 GetAvoidanceDirection()
    {
        // Raycast ahead
        RaycastHit2D hit = Physics2D.Raycast(transform.position, currentDirection, avoidDistance, obstacleMask);
        if (hit.collider != null)
        {
            // CHECK FOR FOOD: If we hit a fish we can eat, DON'T avoid it!
            if (fishData != null)
            {
                // TryGetComponent is faster
                if (hit.collider.TryGetComponent<Fish>(out Fish otherFish))
                {
                    if (fishData.Level > otherFish.Level)
                    {
                        // It's prey! Charge!
                        return Vector2.zero;
                    }
                }
            }

            // Reflect off the normal
            return Vector2.Reflect(currentDirection, hit.normal).normalized;
        }
        
        // Side feelers
        Vector2 leftDir = Quaternion.Euler(0, 0, 30) * currentDirection;
        Vector2 rightDir = Quaternion.Euler(0, 0, -30) * currentDirection;

        // Check Left
        RaycastHit2D hitLeft = Physics2D.Raycast(transform.position, leftDir, avoidDistance * 0.7f, obstacleMask);
        if (hitLeft.collider != null)
        {
            if (fishData != null && hitLeft.collider.TryGetComponent<Fish>(out Fish otherFish))
            {
                if (fishData.Level > otherFish.Level) return Vector2.zero;
            }
            return Quaternion.Euler(0, 0, -45) * currentDirection; // Turn right
        }
            
        // Check Right
        RaycastHit2D hitRight = Physics2D.Raycast(transform.position, rightDir, avoidDistance * 0.7f, obstacleMask);
        if (hitRight.collider != null)
        {
            if (fishData != null && hitRight.collider.TryGetComponent<Fish>(out Fish otherFish))
            {
                if (fishData.Level > otherFish.Level) return Vector2.zero;
            }
            return Quaternion.Euler(0, 0, 45) * currentDirection; // Turn left
        }

        return Vector2.zero;
    }

    Vector2 GetSeparationDirection()
    {
        // OPTIMIZATION: Use LayerMask to filter neighbors
        int count = Physics2D.OverlapCircle(transform.position, separationRadius, new ContactFilter2D { layerMask = separationMask, useLayerMask = true }, neighborBuffer);
        Vector2 separation = Vector2.zero;
        int separationCount = 0;

        for (int i = 0; i < count; i++)
        {
            var c = neighborBuffer[i];
            // Skip self
            if (c.gameObject == gameObject) continue;

            // Optimization: TryGetComponent is faster and safer
            if (c.TryGetComponent<Fish>(out Fish otherFish))
            {
                // CRITICAL FIX: Don't separate from things we can eat!
                // If I am bigger than the other fish, I should NOT push away from it.
                // I want to collide with it to eat it.
                if (fishData != null && fishData.Level > otherFish.Level)
                {
                    continue; // Skip separation force for food
                }

                Vector2 toNeighbor = transform.position - c.transform.position;
                // Protect against zero magnitude (division by zero)
                float sqrMag = toNeighbor.sqrMagnitude;
                if (sqrMag > 0.001f)
                {
                    // Math Optimization: 
                    // normalized / magnitude  ==  (vector / magnitude) / magnitude  == vector / magnitude^2
                    // This removes TWO square root calculations per neighbor.
                    separation -= toNeighbor / sqrMag; // Push away
                    separationCount++;
                }
            }
        }

        return separationCount > 0 ? separation.normalized : Vector2.zero;
    }

    Vector2 GetBoundaryAvoidanceDirection()
    {
        if (Camera.main == null) return Vector2.zero;

        float screenRatio = (float)Screen.width / (float)Screen.height;
        float height = Camera.main.orthographicSize;
        float width = height * screenRatio;

        // Add a margin so they turn before hitting the edge
        float margin = 1.0f; 
        
        Vector2 pos = transform.position;
        Vector2 steer = Vector2.zero;

        if (pos.x > width - margin)
            steer.x = -1;
        else if (pos.x < -width + margin)
            steer.x = 1;

        if (pos.y > height - margin)
            steer.y = -1;
        else if (pos.y < -height + margin)
            steer.y = 1;

        return steer.normalized;
    }
}
