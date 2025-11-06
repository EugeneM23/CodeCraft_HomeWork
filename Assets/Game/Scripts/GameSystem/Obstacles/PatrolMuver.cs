using UnityEngine;
using DG.Tweening;
using System.Collections;

public class PatrolMover : MonoBehaviour
{
    [SerializeField] private Transform[] waypoints;  
    [SerializeField] private float moveSpeed = 3f;    
    [SerializeField] private float waitTime = 1f;    

    private Vector3[] positions;                    
    private int currentIndex = 0;
    private Tween moveTween;

    private void Start()
    {
   
        positions = new Vector3[waypoints.Length];
        for (int i = 0; i < waypoints.Length; i++)
        {
            positions[i] = waypoints[i].position;
        }

        StartCoroutine(PatrolRoutine());
    }

    private IEnumerator PatrolRoutine()
    {
        while (true)
        {
            Vector3 targetPos = positions[currentIndex];
            float distance = Vector3.Distance(transform.position, targetPos);
            float duration = distance / moveSpeed;

            moveTween = transform.DOMove(targetPos, duration)
                .SetEase(Ease.Linear);

            yield return moveTween.WaitForCompletion();

            yield return new WaitForSeconds(waitTime);

            currentIndex = (currentIndex + 1) % positions.Length;
        }
    }

    private void OnDisable() => moveTween?.Kill();
}