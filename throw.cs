using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Throw : MonoBehaviour
{

    public GameObject ballPrefab;
    public float shotSpeed;
    public int shotCount;
    
    void Update()
    {
        if (Input.GetKey(KeyCode.Mouse0))
        {      

            if (shotCount > 0)
            {
                shotCount -= 1;

                GameObject ball = (GameObject)Instantiate(ballPrefab, transform.position, Quaternion.identity);
                Rigidbody ballRb = ball.GetComponent<Rigidbody>();
                ballRb.AddForce(transform.forward * shotSpeed);

                //ボールは10秒後に削除する

                Destroy(ball, 10.0f);
            }

        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            shotCount = 1;

        }

    }
}