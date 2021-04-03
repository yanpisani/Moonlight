using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class RigidBehavior : MonoBehaviour, IDamagable {
    Player player;
    public NavMeshAgent agent;
    public GameObject head;
    public Transform[] visionTransforms; //multiple points to be checked if the player can see us
    RaycastHit rayHit;
    NavMeshHit navHit;
    int layerMask;
    int health = 50;
    public bool stop;
    const float speed = 1f; //meters per second that we move towards the player
    float teleportTimer; //countdown to teleportation
    const float teleportMinTime = 10f;
    const float teleportMaxTime = 30f;
    float timeOfLastAttack;
    const float attackCooldown = 1f;
    const int damage = 1;
    public float walkChance = 0.7f; //chance for rigid to walk to player
    float walkRoll;
    bool canWalk; //only true if we have a straight path to the player and he is looking away from us

    void Start() {
       player = FindObjectOfType<Player>();
       layerMask = 768;
       layerMask = ~layerMask;
    }

    void OnEnable(){
        health = 50;
        teleportTimer = Random.Range(teleportMinTime, teleportMaxTime);
    }

    void Update(){
        if (!PlayerCanSeeAnyOffsetPoint(transform.position)) { //can't see me
            //look at player
            Vector3 lookTarget = player.transform.position;
            lookTarget.y = transform.position.y;
            transform.LookAt(lookTarget);
            
            if (!NavMesh.Raycast(transform.position, player.transform.position, out navHit, NavMesh.AllAreas)) { //straight path to player
                if (Vector3.SqrMagnitude(player.GetFootPosition() - transform.position) > 1f) {
                    if (!canWalk) {
                        canWalk = true;
                        walkRoll = Random.value;
                    }
                    if (walkRoll < walkChance) {
                        //walk forward
                        agent.velocity = transform.forward * speed;
                    }
                }
                else if (Time.time > timeOfLastAttack + attackCooldown) {
                    //attack
                    timeOfLastAttack = Time.time;
                    player.Hit(damage);
                }
            }
            else {
                //wait to teleport
                canWalk = false;
                teleportTimer -= Time.deltaTime;
                if (teleportTimer < 0f) {
                    Teleport();
                }
            }
        }
        else { //can see me
            canWalk = false;
        }
    }

    bool HasLineOfSight(Vector3 p) {
        return !Physics.Linecast(p, player.playerCamera.transform.position, out rayHit, layerMask);
    }

    bool PlayerCanSeePoint(Vector3 p) {
        if (Vector3.Dot(player.playerCamera.transform.forward, (p - player.playerCamera.transform.position).normalized) < 0.3f) {
            return false;
        }
        return HasLineOfSight(p);
    }

    bool PlayerCanSeeAnyOffsetPoint(Vector3 p) {
        foreach (var t in visionTransforms) {
            if (PlayerCanSeePoint(p + t.localPosition)) {
                return true;
            }
        }
        return false;
    }

    public void Damage(int n) {
        health -= n;
        if (health <= 0) {
            Destroy(gameObject);
        }
    }

    public void Teleport() {
        Vector3 randomPoint = Random.insideUnitSphere * 50f + player.transform.position;
        //if point is close to navmesh snap to it
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 1f, NavMesh.AllAreas)) {
            //return if too close (< 5 meters) or if chosen spot can be seen by the player
            if (Vector3.SqrMagnitude(player.GetFootPosition() - hit.position) < 25f || PlayerCanSeeAnyOffsetPoint(hit.position)) {
                return;
            }
            NavMeshPath path = new NavMeshPath();
            NavMesh.CalculatePath(hit.position, player.transform.position, NavMesh.AllAreas, path);
            //if there is a path to the player
            if (path.status == NavMeshPathStatus.PathComplete) {
                transform.position = hit.position + Vector3.up;
                teleportTimer = Random.Range(teleportMinTime, teleportMaxTime);
            }
        }
    }
}
