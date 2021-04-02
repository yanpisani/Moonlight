using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class VFAS : MonoBehaviour {

    NavMeshAgent agent;

    void Start() {
        agent = GetComponent<NavMeshAgent>();
    }
    void FixedUpdate() {
        //if (agent.pathPending || agent.pathStatus == NavMeshPathStatus.PathComplete) return;
        Vector3 randomPoint = Random.insideUnitSphere * 20f + transform.position;
        //if point is close to navmesh snap to it
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 1f, NavMesh.AllAreas)) {
            agent.SetDestination(hit.position);
        }
    }
    void OnDrawGizmos() {
        Gizmos.DrawCube(agent.destination, new Vector3(1f, 1f, 1f));
    }
}
