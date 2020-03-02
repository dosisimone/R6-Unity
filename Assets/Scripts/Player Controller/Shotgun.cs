using UnityEngine;

public class Shotgun : AGun
{
    [SerializeField, Range(0f, 1f)]
    private float circularSpreadAt1M = 0.1f;
    [SerializeField, Range(1f, 20f)]
    private int numberOfBulletsInACardridge = 8;
    [SerializeField]
    private AnimationCurve destructionLvDropoff;
    [SerializeField, Range(1f, 100f)]
    private float destructionLvDropoffMaxRange = 25f;

    private float nextShootTime = 0f;

    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    public override void Shoot()
    {
        if (nextShootTime > Time.time)
        {
            return;
        }

        nextShootTime = Time.time + reloadTime;

        Vector3 screenCenter = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width / 2f, Screen.height / 2f, 0));
        int layerMask = 1 << 8;
        Vector3 randomPoint;
        Vector3 rayDirection;
        RaycastHit[] hits;
        Ray ray;
        for (int i = 0; i < numberOfBulletsInACardridge; ++i)
        {
            //create the ray
            randomPoint = Random.insideUnitSphere * circularSpreadAt1M;
            rayDirection = Camera.main.transform.forward;
            rayDirection += randomPoint;
            ray = new Ray(screenCenter, rayDirection);
            //raycast
            hits = Physics.RaycastAll(ray, Mathf.Infinity, layerMask);
            //
            for (int rhi = 0; rhi < hits.Length; ++rhi)
            {
                Wall wall = hits[rhi].collider.gameObject.GetComponent<Wall>();
                float distance = hits[rhi].distance;
                float xCurveDropoff = distance / destructionLvDropoffMaxRange;
                float destructionLvWithDropoff = destructionLevel * destructionLvDropoff.Evaluate(xCurveDropoff);                
                wall.AddBulletHole(ray, destructionLvWithDropoff);
            }
        }

        audioSource.Play();
    }
}
