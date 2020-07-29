using UnityEngine;
using System.Collections;

public class SmallDial : MonoBehaviour
{
    private float pos;
    private int targ;
    void Update()
    {
        float t = targ * -90;
        float diff = pos - t;
        if(diff <= -180) diff += 360; if(diff > 180) diff -= 360;

        float move = Time.deltaTime * 90 * 2;
        if(diff < 0) {
            if(-move < diff) pos = t;
            else {
                pos += move;
                if(pos >= 360) pos -= 360;
            }
        }
        else {
            if(move > diff) pos = t;
            else {
                pos -= move;
                if (pos < 0) pos += 360;
            }
        }

        transform.localRotation = Quaternion.Euler(new Vector3(0, -90, pos));
    }
    
    public void Move(int target) {
        targ = target;
    }
}