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
        public EnemyState state;
        Enemy statePreviousFrame;
        int health = 50;
        public bool stop;

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
        switch(state){
            case RigidState.Approach:
                if(!PlayerCanSeeMe()){
                    break;
                }
            
        }
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
}
