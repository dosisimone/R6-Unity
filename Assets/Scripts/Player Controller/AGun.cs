using UnityEngine;

public abstract class AGun : MonoBehaviour
{
    [SerializeField]
    protected Transform holeTransform;

    [SerializeField, Range(0.1f, 2f)]
    protected float reloadTime = 1f;
    [SerializeField, Range(0.01f, 1f)]
    protected float destructionLevel = 10;

    public abstract void Shoot();
}
