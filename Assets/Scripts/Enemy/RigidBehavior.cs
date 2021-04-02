using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum RigidState {
    Stop,
    Approach,
    Attack
}
public class RigidBehavior : MonoBehaviour {
        DangerGrid dangerGrid;
        Player player;
        public NavMeshAgent agent;
        public GameObject head;
        RaycastHit hitInfo;
        int layerMask;
        public RigidState state;
        RigidState statePreviousFrame;
        int health = 50;
        public bool stop;
        float reactionTimer = Mathf.Infinity;

        [HideInInspector]
        public Vector3 lookAtVector;

    // Start is called before the first frame update
    void Start() {
       dangerGrid = FindObjectOfType<DangerGrid>();
       player = FindObjectOfType<Player>();
       layerMask = 768;
       layerMask = ~layerMask;
    }

    void OnEnable(){
        health = 50;
        agent.ResetPath();
        lookAtVector = Random.onUnitSphere;
    }
    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate(){
        if(stop){
            state = RigidState.Stop;
            return;
        }

        if (statePreviousFrame != state) {
            statePreviousFrame = state;
            reactionTimer = Time.time + 0.3f;
        }

        switch(state){
            case RigidState.Approach:
                if(!PlayerCanSeeMe()){
                    if(CanSeePlayer()){
                        if (Vector3.Distance(agent.destination, player.transform.position) > 1f && !agent.pathPending) { //so we are not constantly setting agent.destination
                            agent.destination = player.transform.position;
                        }

                        if (Vector3.Distance(transform.position, player.transform.position) < 2f) {
                            state = RigidState.Attack;
                        }
                    }
                }
                break;
            
            case RigidState.Attack:
                player.Hit(1);
            break;

            case RigidState.Stop:
            break;

            default:
                state = RigidState.Stop;
                break;
        }
    }

    bool HasLineOfSight() {
        return !Physics.Linecast(transform.position, player.transform.position, out hitInfo, layerMask);
    }

    public bool CanSeePlayer() {
        if (Vector3.Dot(transform.forward, (player.transform.position - transform.position).normalized) < 0.707f) {
            return false;
        }
        return HasLineOfSight();
    }

    public bool PlayerCanSeeMe() {
        if (Vector3.Dot(player.playerCamera.transform.forward, (transform.position - player.transform.position).normalized) < 0.3f) {
            return false;
        }
        return HasLineOfSight();
    }

    public void Damage(int n) {
        health -= n;
        if (health <= 0) {
            gameObject.SetActive(false);
            Respawn();
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
}
