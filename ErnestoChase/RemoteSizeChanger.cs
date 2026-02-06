using UnityEngine;

namespace ErnestoChase;

public class RemoteSizeChanger : MonoBehaviour
{
    private ErnestoState state;
    private bool shrinked = false;
    private bool changingSize = false;
    private float sizeStartTime;
    private float lastSize;
    private readonly float sizeChangeLength = 1.5f;

    private void Awake()
    {
        state = GetComponent<ErnestoState>();
        enabled = false;
    }

    private void FixedUpdate()
    {
        if (changingSize)
        {
            float timeLerp = Mathf.InverseLerp(sizeStartTime, sizeStartTime + sizeChangeLength, Time.fixedTime);
            float scale;

            if (shrinked)
            {
                scale = Mathf.SmoothStep(lastSize, 0.1f, timeLerp);
            }
            else
            {
                scale = Mathf.SmoothStep(lastSize, 1f, timeLerp);
            }

            transform.localScale = Vector3.one * scale;

            if (timeLerp == 1)
            {
                changingSize = false;
                enabled = false;

                if (!shrinked)
                {
                    state.KillVolumeEnabled = true;
                }
            }
        }
    }

    public void SetSize(bool shrink)
    {
        ErnestoChase.WriteDebugMessage("Set size");
        if (shrinked == shrink) return;

        ErnestoChase.WriteDebugMessage("Process shrink to " + shrink);
        shrinked = shrink;
        lastSize = transform.localScale.x;
        sizeStartTime = Time.fixedTime;
        changingSize = true;

        if (shrinked)
        {
            state.KillVolumeEnabled = false;
        }
        
        enabled = true;
    }
}
