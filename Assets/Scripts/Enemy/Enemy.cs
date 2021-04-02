using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum EnemyState {
    Wander,
    Stop,
    Chase,
    Attack,
    Shoot,
    Flee
}

public class Enemy : MonoBehaviour, IDamagable {

    DangerGrid dangerGrid;
    Player player;
    public NavMeshAgent agent;
    public GameObject head;
    //Vector3 target;
    RaycastHit hitInfo;
    int layerMask;
    public EnemyState state;
    EnemyState statePreviousFrame;

    int health = 100;

    public AudioSource audioSource;
    public AudioClip chargeSound;
    public AudioClip shotSound;
    public AudioSource footstepSound;
    float footstepTime;
    const float footstepInterval = 0.3f;
    bool walking = true;
    const float walkSpeed = 2.5f;
    const float runSpeed = 5f;
    float walkingChance = 0.9f;
    float timer = Mathf.Infinity;
    bool chance;
    bool reachedWaypoint;
    float reactionTimer = Mathf.Infinity;
    [HideInInspector] public Vector3 lookAtVector; //direction we rotate towards
    //[HideInInspector] public float watchedDistanceSqr = Mathf.Infinity;
    //Vector3 lookingAt; //the location we're looking at

    public bool stop; //do nothing except looking

    Vector3 vec;

    float dodge;
    float dodgeTarget;


    void Start() {
        dangerGrid = FindObjectOfType<DangerGrid>();
        player = FindObjectOfType<Player>();
        layerMask = 768; //player and enemy
        layerMask = ~layerMask; //invert so everything except the player and enemy
        footstepTime = Random.value * 0.3f;
        //Respawn();
    }
    void OnEnable() {
        walking = true;
        state = EnemyState.Wander;
        statePreviousFrame = EnemyState.Flee;
        health = 100;
        agent.ResetPath();
        lookAtVector = Random.onUnitSphere;
    }

    void Update() {
        //dodge target is being randomly changed in FixedUpdate
        dodge = Mathf.MoveTowards(dodge, dodgeTarget, Time.deltaTime * 3f);
        if (state != EnemyState.Stop && state != EnemyState.Shoot) {
            agent.Move((transform.right * dodge + transform.forward * -0.2f * Mathf.Abs(dodge)) * Time.deltaTime * agent.speed);
        }
        if (state == EnemyState.Stop || state == EnemyState.Shoot) {
            lookAtVector.y = 0f;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(lookAtVector), agent.angularSpeed * Time.deltaTime);
        }
    }

    void FixedUpdate() {
        if (stop) { //do nothing
            state = EnemyState.Stop;
            return;
        }
        if (Random.Range(0, 20) == 0) {
            dodgeTarget = Random.Range(-0.5f, 0.5f);
        }

        if (!walking) {
            if (Time.time > footstepTime) {
                footstepTime += footstepInterval;
                footstepSound.Play();
            }
        } else {
            footstepTime = Time.time + footstepInterval;
        }

        //reaction time so we don't instantly switch states
        if (statePreviousFrame != state) {
            statePreviousFrame = state;
            reactionTimer = Time.time + 0.3f;
        }

        //when we first enter a new state
        if (Time.time > reactionTimer) {
            reactionTimer = Mathf.Infinity;
            switch (state) {
                case EnemyState.Wander:
                    chance = FuncLib.Chance(0.4f);
                    reachedWaypoint = false;
                    SetWalking(walkingChance);
                    break;

                case EnemyState.Stop:
                    agent.ResetPath();
                    SetWalking(1f);
                    timer = Time.time + Random.Range(3f, 15f);
                    //transform.LookAt(transform.position + Random.onUnitSphere);
                    lookAtVector = Random.onUnitSphere;
                    break;

                case EnemyState.Chase:
                    SetWalking(0.5f);
                    break;

                //case EnemyState.Attack:
                //    break;
                case EnemyState.Shoot:
                    agent.ResetPath();
                    SetWalking(1f);
                    timer = Time.time + 1f;
                    audioSource.PlayOneShot(chargeSound);
                    break;

                case EnemyState.Flee:
                    agent.ResetPath();
                    SetWalking(0f);
                    break;

                default:
                    break;
            }
        } else if (reactionTimer != Mathf.Infinity) {
            return;
        }

        switch (state) {
            case EnemyState.Wander:
                if (chance) {
                    state = EnemyState.Stop;
                    break;
                }
                if (agent.remainingDistance < 2f) {
                    if (!reachedWaypoint) {
                        reachedWaypoint = true;
                        chance = FuncLib.Chance(0.4f);
                    }
                    TickRandomWaypointSearch(20f, 30f);
                } else {
                    reachedWaypoint = false;
                }

                if (CanSeePlayer()) {
                    if (FuncLib.Chance(0.5f)) {
                        state = EnemyState.Shoot;
                    } else {
                        state = EnemyState.Chase;
                    }
                }
                break;

            case EnemyState.Stop:
                //transform.LookAt(player.transform);
                if (Time.time > timer) {
                    state = EnemyState.Wander;
                }
                if (CanSeePlayer()) {
                    if (Vector3.Distance(transform.position, player.transform.position) < 5f) {
                        state = EnemyState.Chase;
                    } else {
                        state = EnemyState.Shoot;
                    }
                }
                break;

            case EnemyState.Chase:
                if (Vector3.Distance(agent.destination, player.transform.position) > 1f && !agent.pathPending) { //so we are not constantly setting agent.destination
                    agent.destination = player.transform.position;
                }
                if (PlayerCanSeeMe()) {
                    SetWalking(0f);
                }

                if (Vector3.Distance(transform.position, player.transform.position) < 2f) {
                    state = EnemyState.Attack;
                }
                if (!CanSeePlayer()) {
                    state = EnemyState.Wander;
                }
                break;

            case EnemyState.Attack:
                player.Hit(1);
                /*doattack animation;
                
                if (attackdone) {
                    state = EnemyState.Flee
                }*/

                if (true) {
                    state = EnemyState.Flee;
                }
                break;

            case EnemyState.Shoot:
                //transform.LookAt(player.transform);
                lookAtVector = player.transform.position - transform.position;
                if (Time.time > timer) {
                    audioSource.PlayOneShot(shotSound);
                    if (CanSeePlayer()) {
                        player.Hit(1);
                    }
                    state = EnemyState.Flee;
                }
                break;

            case EnemyState.Flee:
                if (!agent.hasPath) {
                    TickRandomWaypointSearch(25f, 35f, 10f, true);
                } else if (agent.remainingDistance < 2f) {
                    state = EnemyState.Wander;
                }
                break;

            default:
                state = EnemyState.Wander;
                break;
        }
    }
    void OnDrawGizmos() {
        //Gizmos.DrawCube(agent.destination, new Vector3(1f, 1f, 1f));
        //Gizmos.DrawCube(hitInfo.point, new Vector3(0.5f, 0.5f, 0.5f));
        //Gizmos.color = Color.magenta;
        //vec.Set(0.2f, 2f, 0.2f);
        //Gizmos.DrawCube(lookAtVector, vec);
    }

    void SetWalking(float chance) {
        walking = Random.value < chance;
        agent.speed = walking ? walkSpeed : runSpeed;
    }

    bool HasLineOfSight() {
        return !Physics.Linecast(transform.position, player.transform.position, out hitInfo, layerMask);
    }
    bool CanSeePlayer() {
        if (Vector3.Dot(transform.forward, (player.transform.position - transform.position).normalized) < 0.707f) {
            return false;
        }
        return HasLineOfSight();
    }
    bool PlayerCanSeeMe() {
        if (Vector3.Dot(player.playerCamera.transform.forward, (transform.position - player.transform.position).normalized) < 0.3f) {
            return false;
        }
        return HasLineOfSight();
    }

    void TickRandomWaypointSearch(float radius, float maxDistance, float minDistance = 0f, bool hidden = false) {
        if (!agent.pathPending) {
            //changed random point to be around ai instead of around player
            Vector3 randomPoint = Random.insideUnitSphere * radius + /*player.*/transform.position;
            //if point is close to navmesh snap to it
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 1f, NavMesh.AllAreas)) {
                //return if chosen spot can be seen by the player
                if (hidden && !Physics.Linecast(hit.position, player.transform.position, out hitInfo, layerMask)) {
                    return;
                }
                agent.destination = hit.position;
                /*NavMeshPath path = new NavMeshPath();
                NavMesh.CalculatePath(hit.position, player.transform.position, NavMesh.AllAreas, path);
                //if there is a path to the player
                if (path.status == NavMeshPathStatus.PathComplete) {
                    //find path length
                    Vector3[] pathCorners = path.corners;
                    float totalLength = 0f;
                    for (int i = 0; i < pathCorners.Length - 1; i++) {
                        totalLength += Vector3.Distance(pathCorners[i], pathCorners[i + 1]);
                    }
                    if (totalLength <= maxDistance && totalLength > minDistance) {
                        agent.destination = hit.position;
                    }
                }*/
            }
        }
    }


    public void Damage(int n) {
        health -= n;
        if (health <= 0) {
            gameObject.SetActive(false);
            Respawn();
        } else if (state == EnemyState.Stop || state == EnemyState.Wander) {
            if (FuncLib.Chance(0.5f)) {
                state = EnemyState.Shoot;
            } else {
                state = EnemyState.Flee;
            }
        }
    }

    public void Respawn() {
        bool found = false;
        int loopCount = 0;
        while (!found && loopCount < 9002) {
            ++loopCount;
            if (loopCount > 9000) {
                Debug.LogError("Could not find a spawn point");
                transform.position = new Vector3(1f, 0f, 0f);
                break;
            }
            Vector3 randomPoint = Random.insideUnitSphere * 100f + player.transform.position;
            //if point is close to navmesh snap to it
            if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 1f, NavMesh.AllAreas)) {
                //return if chosen spot can be seen by the player
                if (!Physics.Linecast(hit.position + Vector3.up, player.transform.position, out hitInfo, layerMask)) {
                    continue;
                }
                NavMeshPath path = new NavMeshPath();
                NavMesh.CalculatePath(hit.position, player.transform.position, NavMesh.AllAreas, path);
                //if there is a path to the player
                if (path.status == NavMeshPathStatus.PathComplete) {
                    //find path length
                    Vector3[] pathCorners = path.corners;
                    float totalLength = 0f;
                    for (int i = 0; i < pathCorners.Length - 1; i++) {
                        totalLength += Vector3.Distance(pathCorners[i], pathCorners[i + 1]);
                    }
                    if (totalLength > 10f) {
                        found = true;
                        transform.position = hit.position + Vector3.up;
                    }
                }
            }
        }
        gameObject.SetActive(true);
    }

    public Vector3 GetHeadPosition() {
        return head.transform.position;
    }
}
