using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class NavMeshSpawnFix : MonoBehaviour
{
    NavMeshAgent agent;
    bool initialized;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    IEnumerator Start()
    {
        // ЖДЁМ 1 КАДР — КЛЮЧ
        yield return null;

        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(
                    transform.position,
                    out var hit,
                    10f,
                    NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }

        initialized = true;
    }
}