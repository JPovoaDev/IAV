using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PatrolRobot : MonoBehaviour
{

    [SerializeField] private Transform[] waypoints;       // WP1-WP4 wander
    [SerializeField] private float waitAtWaypoint = 1.5f;

    private NavMeshAgent nav;
    private int currentWP = 0;
    private bool patrolling = true;
    private float waitTimer = 0f;
    private bool waiting = false;

    private void Awake()
    {
        nav = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        if (waypoints.Length > 0)
            nav.SetDestination(waypoints[0].position);
    }

    private void Update()
    {
        if (!patrolling || waypoints.Length == 0) return;

        if (waiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                waiting = false;
                currentWP = (currentWP + 1) % waypoints.Length;
                nav.SetDestination(waypoints[currentWP].position);
            }
            return;
        }

        if (!nav.pathPending && nav.remainingDistance <= nav.stoppingDistance)
        {
            waiting = true;
            waitTimer = waitAtWaypoint;
        }
    }

    public void StopPatrol()
    {
        patrolling = false;
        nav.isStopped = true;
    }

    public void ResumePatrol()
    {
        patrolling = true;
        nav.isStopped = false;
        if (waypoints.Length > 0)
            nav.SetDestination(waypoints[currentWP].position);
    }

    // Navega para um ponto arbitrário sem interferir com o wander
    // Usado pelo ARIAInvestigator para mandar o robot para zonas e base
    public void GoTo(Vector3 position)
    {
        nav.isStopped = false;
        nav.SetDestination(position);
    }

    public NavMeshAgent Agent => nav;
}