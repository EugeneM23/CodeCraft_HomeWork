using UnityEngine;

public class Aim : MonoBehaviour
{
    [SerializeField] private Transform _targetTransform;
    [SerializeField] private Transform _aimTransform;
    [SerializeField] private int Iterations = 3;

    [Range(0f, 1f)] [SerializeField] public float Weight = 1f;

    [SerializeField] private float _angleLimit = 120f;
    [SerializeField] private float _distanceLimit = 1f;

    [SerializeField] private HumanBone[] _humanBones;
    private Transform[] _boneTransforms;
    //asdas

    private void Start()
    {
        Animator animator = GetComponent<Animator>();

        _boneTransforms = new Transform[_humanBones.Length];

        for (int i = 0; i < _boneTransforms.Length; i++)
            _boneTransforms[i] = animator.GetBoneTransform(_humanBones[i].Bone);
    }

    private void LateUpdate()
    {
        if (_targetTransform == null || _aimTransform == null) return;

        Vector3 targetPosition = GetTargetPosition(_targetTransform.position);

        for (int i = 0; i < Iterations; i++)
        {
            for (int j = 0; j < _boneTransforms.Length; j++)
            {
                float boneWeith = _humanBones[j].weith;

                AimAtTarget(_boneTransforms[j], targetPosition, Weight * boneWeith);
            }
        }
    }

    private Vector3 GetTargetPosition(Vector3 targetPosition)
    {
        Vector3 targetDirection = targetPosition - _aimTransform.position;
        Vector3 aimDirection = _aimTransform.forward;

        float blendOut = 0;

        float targetAngle = Vector3.Angle(targetDirection, aimDirection);

        if (targetAngle > _angleLimit)
            blendOut += (targetAngle - _angleLimit) / 1f;

        float targetDistance = targetDirection.magnitude;

        if (targetDistance < _distanceLimit)
            blendOut += _distanceLimit - targetDistance;

        Vector3 direction = Vector3.Slerp(targetDirection, aimDirection, blendOut);
        return _aimTransform.position + direction;
    }

    private void AimAtTarget(Transform bone, Vector3 targetPosition, float weight)
    {
        Vector3 aimDirection = _aimTransform.forward;
        Vector3 targetDirection = targetPosition - _aimTransform.position;
        Quaternion aimTowards = Quaternion.FromToRotation(aimDirection, targetDirection);
        Quaternion blendRotation = Quaternion.Slerp(Quaternion.identity, aimTowards, weight);
        bone.rotation = blendRotation * bone.rotation;
    }

    public void SetTargetTransform(Transform target) => _targetTransform = target;

    public void SetAimTransform(Transform aim) => _aimTransform = aim;
}

[System.Serializable]
public class HumanBone
{
    [Range(0f, 1f)] public float weith = 1f;
    public HumanBodyBones Bone;
}