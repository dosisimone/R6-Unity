using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerGunController : MonoBehaviour
{
    [SerializeField]
    private AGun gun;

    void Update()
    {
        float fire = Input.GetAxis("Fire1");

        if (fire > 0)
        {
            gun.Shoot();
        }
    }
}
