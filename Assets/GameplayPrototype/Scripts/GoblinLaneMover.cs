using UnityEngine;

/// <summary>Moves one Goblin straight from spawn toward the far lane endpoint.</summary>
public sealed class GoblinLaneMover : MonoBehaviour
{
    public LaneGenerator lane;
    [Min(.1f)] public float movementSpeed = 2.75f;
    [Min(0f)] public float goalInset = .8f;
    [Tooltip("Yaw correction for the imported model's forward axis, if required.")]
    public float modelYawOffset;

    void Update()
    {
        if (!lane) return;
        Vector3 local = lane.transform.InverseTransformPoint(transform.position);
        local.z += movementSpeed * Time.deltaTime;
        if (local.z >= lane.LengthWorld - goalInset)
        {
            Destroy(gameObject);
            return;
        }
        local.y = 0f;
        transform.position = lane.transform.TransformPoint(local);
        transform.rotation = lane.transform.rotation * Quaternion.Euler(0f, modelYawOffset, 0f);
    }
}
