using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PatrolRobot : MonoBehaviour {

    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float waitAtWaypoint = 1.5f;

    private NavMeshAgent nav;
    private int currentWP = 0;
    private bool patrolling = true;
    private float waitTimer = 0f;
    private bool waiting = false;

    private void Awake() {
        nav = GetComponent<NavMeshAgent>();
    }

    private void Start() {
        if (waypoints.Length > 0)
            nav.SetDestination(waypoints[0].position);
    }

    private void Update() {
        if (!patrolling || waypoints.Length == 0) return;

        if (waiting) {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f) {
                waiting = false;
                currentWP = (currentWP + 1) % waypoints.Length;
                nav.SetDestination(waypoints[currentWP].position);
            }
            return;
        }

        if (!nav.pathPending && nav.remainingDistance <= nav.stoppingDistance) {
            waiting = true;
            waitTimer = waitAtWaypoint;
        }
    }

    public void StopPatrol() {
        patrolling = false;
        nav.isStopped = true;
    }

    public void ResumePatrol() {
        patrolling = true;
        nav.isStopped = false;
        nav.SetDestination(waypoints[currentWP].position);
    }
}