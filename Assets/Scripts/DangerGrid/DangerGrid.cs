using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;

enum Status {
    Invalid,
    Clear,
    Danger,
    Yellow
}

public class DangerGrid : MonoBehaviour {
    Status[,,] dangerArray;
    [HideInInspector] public List<Vector3Int> yellowList = new List<Vector3Int>();
    //List<Vector3> ghostList = new List<Vector3>(); //locations that the ai should look at
    Vector3Int[] dirArray = new Vector3Int[4]; //so we can easily loop through all directions like NSEW and diagnally up/down
    Vector3Int offset; //subtract this to go from world to grid, add to go from grid to world
    Vector3 vec; //avoid new
    Vector3Int veci;
    NavMeshHit navHit;
    NavMeshPath path;
    public Vector3Int arraySize;
    uint tick;
    uint expandEveryXthTick = 25u;

    Player player;
    Vector3 playerLastKnownPos;
    Vector3 playerHeightOffset = Vector3.up * 1.3f;
    List<Enemy> ai = new List<Enemy>();
    RaycastHit rayHit;
    LayerMask defaultOnlyMask;
    bool threatVisible;
    //public float ghostSpacing = 5f; //ghosts must be this far apart from eachother
    const float clusterDistanceSqr = 25f;
    const float watchedDistanceSqr = 4f;
    [HideInInspector] public List<Vector3Int> clusterList = new List<Vector3Int>(); //yellows that represent a group of clusted yellows
    HashSet<Vector3Int> clusterSet = new HashSet<Vector3Int>(); //skip checking yellows in the set because they are too close to an already checked one
    HashSet<Vector3Int> watchedSet = new HashSet<Vector3Int>(); //a spot that's being watched
    List<Vector3> watchedList = new List<Vector3>(); //needed so we can cluster our watched points
    HashSet<int> watcherSet = new HashSet<int>(); //list index of AI's that are already watching a spot
    Vector3[] firstCornerArray = new Vector3[2]; //so we can use non-alloc version of path corners and we only need the first corner
    Vector3[] fullCornerArray = new Vector3[16];

    public Vector3 cubeSize = new Vector3(0.5f, 0.2f, 0.5f);

    public Vector3Int GridCoords(Vector3 n) {
        n -= offset;
        veci.Set(Mathf.RoundToInt(n.x), Mathf.RoundToInt(n.y), Mathf.RoundToInt(n.z));
        return veci;
    }
    public Vector3 WorldCoords(Vector3Int n) {
        n += offset;
        return n;
    }

    private void Awake() {
        dirArray[0] = Vector3Int.forward;
        dirArray[1] = Vector3Int.right;
        dirArray[2] = Vector3Int.back;
        dirArray[3] = Vector3Int.left;
    }

    void Start() {
        player = FindObjectOfType<Player>();
        playerLastKnownPos = player.GetFootPosition();
        expandEveryXthTick = (uint)Mathf.Clamp(Mathf.RoundToInt(50f/player.walkingSpeed), 0, 500);
        path = new NavMeshPath();
        offset.Set(Mathf.RoundToInt(transform.position.x - arraySize.x/2), Mathf.RoundToInt(transform.position.y), Mathf.RoundToInt(transform.position.z - arraySize.z/2)); //center is bottom middle
        dangerArray = new Status[arraySize.x,arraySize.y,arraySize.z]; //can set with non-const values in start

        //find valid cells
        //also check if they are reachable
        for (int x = 0; x < dangerArray.GetLength(0); x++) {
            for (int y = 0; y < dangerArray.GetLength(1); y++) {
                for (int z = 0; z < dangerArray.GetLength(2); z++) {
                    veci.Set(x, y, z);
                    veci += offset;
                    if (NavMesh.SamplePosition(veci, out navHit, 0.5f, NavMesh.AllAreas)) {
                        //if (hit.position.x == (float)veci.x && hit.position.z == (float)veci.z) { //we only want up/down(y) snapping from SamplePosition
                        if (Mathf.Approximately(navHit.position.x, (float)veci.x) && Mathf.Approximately(navHit.position.z, (float)veci.z)) {
                            if (NavMesh.CalculatePath(transform.position, veci, NavMesh.AllAreas, path)) { //make sure it's reachable from the center of the grid
                                if (path.status == NavMeshPathStatus.PathComplete) {
                                    dangerArray[x,y,z] = Status.Clear;
                                }
                            }
                        }
                    }
                }
            }
        }


        Enemy[] list = FindObjectsOfType<Enemy>();
        ai.AddRange(list);
        defaultOnlyMask = LayerMask.GetMask("Default");

        //test
        /*veci.Set((int)transform.position.x, (int)transform.position.y, (int)transform.position.z);
        veci -= offset;
        dangerArray[veci.x, veci.y, veci.z] = Status.Yellow;
        yellowList.Add(veci);*/
        //test
        threatVisible = true;
    }

    //for list looping through be like hasRecycled then move on first grow but if more adjacant add to list
    void ExpandYellow(Vector3Int position, Status moveInto, Status leaveBehind, int borderIndex) {
        bool hasRecycled = false;
        //bool adjacantSpotted = false;
        Vector3Int p = position;
        dangerArray[p.x,p.y,p.z] = leaveBehind;
        foreach(var dir in dirArray) {
            p = position + dir;
            if (p.x < 0 || p.x >= dangerArray.GetLength(0) || p.y < 0 || p.y >= dangerArray.GetLength(1) || p.z < 0 || p.z >= dangerArray.GetLength(2)) continue;
            if (dangerArray[p.x,p.y,p.z] == moveInto) { //expand only into the correct color
                /*if (moveInto == Status.Clear && VisibleAny(p + offset + playerHeightOffset)) {
                    adjacantSpotted = true;
                    continue;
                }*/
                dangerArray[p.x,p.y,p.z] = Status.Yellow;
                if (hasRecycled) {
                    yellowList.Add(p);
                }
                else {
                    hasRecycled = true;
                    yellowList[borderIndex] = p;
                }
            }
        }
        if (!hasRecycled) yellowList.RemoveAt(borderIndex);
        /*if (adjacantSpotted) {
            if (hasRecycled) yellowList.Add(position);
        } else {
            if (!hasRecycled) yellowList.RemoveAt(borderIndex);
            dangerArray[p.x,p.y,p.z] = leaveBehind;
        }*/
    }


    bool LineOfSight(Vector3 start, Vector3 end) {
        return !Physics.Linecast(start, end, out rayHit, defaultOnlyMask);
    }
    bool Visible(Enemy e, Vector3 v) {
        if (Vector3.Dot(e.head.transform.forward, (v - e.GetHeadPosition()).normalized) > 0.707f) { //is head facing towards v, was 0.3
            if (LineOfSight(e.GetHeadPosition(), v)) { //has line of sight
                return true;
            }
        }
        return false;
    }
    bool VisibleAny(Vector3 v) { //visible to any AI
        foreach(var item in ai) {
            if (Visible(item, v)) {
                return true;
            }
        }
        return false;
    }
    

    /*void ClearGrid() {
        borderList.Clear();
        for (int x = 0; x < dangerArray.GetLength(0); x++) {
            for (int y = 0; y < dangerArray.GetLength(1); y++) {
                for (int z = 0; z < dangerArray.GetLength(2); z++) {
                    if (dangerArray[x,y,z] != Status.Invalid) {
                        dangerArray[x,y,z] = Status.Clear;
                    }
                }
            }
        }
    }*/

    void FixedUpdate() {
        ++tick;

        if (VisibleAny(player.GetVisiblePosition())) {
            playerLastKnownPos = player.GetFootPosition();
            if (!threatVisible) { //player just became visible this tick
                threatVisible = true;
                //ClearGrid();
            }
            //return; //don't bother with grid stuff
        }
        else {
            if (threatVisible) { //just lost sight of the player this tick
                threatVisible = false;
                veci = GridCoords(player.GetFootPosition());
                yellowList.Add(veci);
                ExpandYellow(veci, Status.Clear, Status.Danger, yellowList.Count-1);
                return;
            }
        }

        //expand or shrink cells
        for(int i = yellowList.Count-1; i >= 0; i--) { //go in reverse order so removing or appending elements does not mess up our iteration
            if (threatVisible || VisibleAny(yellowList[i] + offset + playerHeightOffset)) { //shrink if visible to any AI
                ExpandYellow(yellowList[i], Status.Danger, Status.Clear, i);
            }
            else if (tick%expandEveryXthTick == 0) { //grow
                ExpandYellow(yellowList[i], Status.Clear, Status.Danger, i);
            }
        }

        //set AI lookAtVector
        if (tick%expandEveryXthTick != 0 && yellowList.Count > 0) {
            clusterList.Clear();
            clusterSet.Clear();
            watchedSet.Clear();
            watcherSet.Clear();
            /*for(int n = 0; n < ai.Count; n++) {
                ai[n].watchedDistanceSqr = Mathf.Infinity;
            }*/
            watchedList.Clear();

            //clustering
            for (int i = 0; i < yellowList.Count; i++) {
                if (clusterSet.Contains(yellowList[i])) continue;
                clusterList.Add(yellowList[i]); //this cell will represent the cells found in the following j loop
                clusterSet.Add(yellowList[i]); //don't check this cell again
                for (int j = 0; j < yellowList.Count; j++) { //mark nearby navmesh line of sight cells as clustered
                    if (clusterSet.Contains(yellowList[j])) continue;
                    if (Vector3.SqrMagnitude(yellowList[j] - yellowList[i]) < clusterDistanceSqr && !NavMesh.Raycast(yellowList[i] + offset, yellowList[j] + offset, out navHit, NavMesh.AllAreas)) {
                        clusterSet.Add(yellowList[j]);
                    }
                }
            }

            for(int n = 0; n < ai.Count; n++) { //todo: condider spreading the work out to one AI per tick instead of all per tick but would need to delay setting their look targets and watchedDistanceSqr until all done
                if (ai[n].state != EnemyState.Stop) continue; //todo: remove
                if (watcherSet.Contains(n)) continue;
                for(int i = 0; i < clusterList.Count; i++) { //for each cluster
                    //pathfind from cluster to AI
                    NavMesh.CalculatePath(clusterList[i] + offset, ai[n].transform.position, NavMesh.AllAreas, path);
                    if (path.status != NavMeshPathStatus.PathComplete) continue;
                    int numCorners = path.GetCornersNonAlloc(fullCornerArray);
                    int bestAngleIndex = -1; //index of the AI who has the best angle
                    float bestAngle = -1f;
                    //corners
                    for(int w = numCorners > 2 ? 1 : 0; w < numCorners && w < fullCornerArray.Length; w++) {
                        if (watchedSet.Contains(FuncLib.vectorRoundToInt(fullCornerArray[w]))) break; //abort this path if any corner is already watched
                        //abort if too close to any other watched point
                        bool tooClose = false;
                        foreach (var item in watchedList) {
                            if (Vector3.SqrMagnitude(item - fullCornerArray[w]) < watchedDistanceSqr) {
                                tooClose = true;
                                break;
                            }
                        }
                        if (tooClose) break;
                        //who has best angle
                        for(int k = 0; k < ai.Count; k++) {
                            if (watcherSet.Contains(k)) continue; //ignore anyone in the watchers set
                            if (!LineOfSight(ai[k].GetHeadPosition(), fullCornerArray[w] + playerHeightOffset)) continue; //ignore those who can't see the corner
                            /*Vector3 v = fullCornerArray[w] - ai[k].transform.position;
                            float distSqr = v.sqrMagnitude;
                            if (distSqr < ai[k].watchedDistanceSqr) {
                                ai[k].watchedDistanceSqr = distSqr;
                                ai[k].lookAtVector = v.normalized;
                            }*/
                            float angle = numCorners > 2 ? Vector3.Dot(ai[k].lookAtVector, (fullCornerArray[w-1] - fullCornerArray[w]).normalized) : 1f;
                            if (angle > bestAngle) {
                                bestAngle = angle;
                                bestAngleIndex = k;
                                ai[k].lookAtVector = (fullCornerArray[w] - ai[k].transform.position).normalized;
                            }
                        }
                        if (bestAngleIndex > -1) {
                            watcherSet.Add(bestAngleIndex);
                            watchedSet.Add(FuncLib.vectorRoundToInt(fullCornerArray[w]));
                            watchedList.Add(fullCornerArray[w]);
                            break; //someone has seen this corner
                        }
                    }
                }
            }
            //if an ai still has nowhere to look
            for(int n = 0; n < ai.Count; n++) {
                if (!watcherSet.Contains(n)) {
                    float closestDist = Mathf.Infinity;
                    Vector3 closestPoint = Vector3.zero;
                    for(int i = 0; i < watchedList.Count; i++) {
                        NavMesh.CalculatePath(ai[n].transform.position, watchedList[i], NavMesh.AllAreas, path);
                        if (path.status != NavMeshPathStatus.PathComplete) continue;
                        path.GetCornersNonAlloc(firstCornerArray);
                        float d = Vector3.SqrMagnitude(firstCornerArray[1] - firstCornerArray[0]);
                        if (d < closestDist) {
                            closestDist = d;
                            closestPoint = firstCornerArray[1];
                        }
                    }
                    ai[n].lookAtVector = (closestPoint - ai[n].transform.position).normalized;
                }
            }
        }

        //red piercer
        //random location check
        veci.Set(Random.Range(0, dangerArray.GetLength(0)), Random.Range(0, dangerArray.GetLength(1)), Random.Range(0, dangerArray.GetLength(2)));
        if (dangerArray[veci.x, veci.y, veci.z] == Status.Danger && VisibleAny(veci + offset)) {
            dangerArray[veci.x, veci.y, veci.z] = Status.Yellow;
            yellowList.Add(veci);
        }
        //current position check
        foreach (var item in ai) {
            GridCoords(item.transform.position + item.transform.forward * 0.71f);
            if (dangerArray[veci.x, veci.y, veci.z] == Status.Danger) {
                dangerArray[veci.x, veci.y, veci.z] = Status.Yellow;
                yellowList.Add(veci);
            }
        }
    }

    private void OnDrawGizmos() {
        if (!EditorApplication.isPlaying) return;
        for (int x = 0; x < dangerArray.GetLength(0); x++) {
            for (int y = 0; y < dangerArray.GetLength(1); y++) {
                for (int z = 0; z < dangerArray.GetLength(2); z++) {
                    switch (dangerArray[x,y,z]) {
                        case Status.Invalid:
                            //Gizmos.color = Color.grey;
                            continue;
                        case Status.Clear:
                            Gizmos.color = Color.green;
                            break;
                        case Status.Danger:
                            Gizmos.color = Color.red;
                            break;
                        case Status.Yellow:
                            Gizmos.color = Color.yellow;
                            break;
                        default:
                            break;
                    }
                    veci.Set(x, y, z);
                    Gizmos.DrawCube(veci + offset, cubeSize);
                }
            }
        }
        /*foreach (var item in borderList) {
            Gizmos.DrawSphere(item + offset, cubeSize.y);
        }*/
        Gizmos.color = Color.cyan;
        foreach (var item in clusterList) {
            Gizmos.DrawSphere(item + offset, 0.1f);
        }
        Gizmos.color = Color.magenta;
        foreach (var item in watchedList) {
            Gizmos.DrawSphere(item, 0.05f);
        }
    }
}
