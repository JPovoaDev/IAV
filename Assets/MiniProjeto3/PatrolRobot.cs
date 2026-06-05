using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PatrolRobot : MonoBehaviour {

    [SerializeField] private Transform[] waypoints;

    // pausa em cada waypoint para simular inspeção, 1.5s parece natural sem parecer que está preso
    private float waitAtWaypoint = 1.5f;
    private NavMeshAgent nav;
    private int currentWP = 0;

    // estas duas flags fazem coisas diferentes e não podiamos usar só uma:
    // patrolling = false quando o ARIAInvestigator precisa do robot para uma missão
    // waiting = true durante a pausa normal entre waypoints da patrulha
    // se fosse a mesma flag o ResumePatrol não conseguia distinguir entre os dois casos
    private bool patrolling = true;
    private float waitTimer = 0f;
    private bool waiting = false;

    private void Awake() {
        nav = GetComponent<NavMeshAgent>();
    }

    private void Start() {
        nav.SetDestination(waypoints[0].position);
    }

    private void Update() {
        if (!patrolling)
            return;

        if (waiting) {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f) {
                waiting = false;
                currentWP = (currentWP + 1) % waypoints.Length;
                nav.SetDestination(waypoints[currentWP].position);
            }
            return;
        }

        // pathPending tem de ser false antes de verificar o remainingDistance
        // porque enquanto o navmesh ainda está a calcular o caminho o remainingDistance não tem um valor correto
        if (!nav.pathPending && nav.remainingDistance <= nav.stoppingDistance) {
            waiting = true;
            waitTimer = waitAtWaypoint;
        }
    }

    public void StopPatrol() {
        patrolling = false;
        nav.isStopped = true; // congela o robot no lugar mas mantém o caminho calculado para poder retomar
    }

    public void ResumePatrol() {
        patrolling = true;
        nav.isStopped = false;
        // volta para o waypoint atual e não o próximo, senão podia saltar waypoints
        // quando o ARIAInvestigator devolvesse o controlo a meio de um percurso
        nav.SetDestination(waypoints[currentWP].position);
    }

    // este método não toca no patrolling nem no currentWP de propósito
    // o ARIAInvestigator é quem chama StopPatrol antes e ResumePatrol depois da missão
    // este método é só "vai para esta posição", sem mais nada
    public void GoTo(Vector3 position) {
        nav.isStopped = false; // pode ter ficado true se StopPatrol foi chamado antes
        nav.SetDestination(position);
    }
}