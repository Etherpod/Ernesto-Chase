using UnityEngine;

public class TargetDataQueue
{
    public struct TargetData
    {
        public TargetData(string parent, Vector3 localPosition, Vector3 worldPosition, float time)
        {
            this.parent = parent;
            this.localPosition = localPosition;
            this.worldPosition = worldPosition;
            this.time = time;
        }

        public string parent;
        public Vector3 localPosition;
        public Vector3 worldPosition;
        public float time;
    }

    private TargetData[] targetDataHistory = new TargetData[150000];
    private int nextTarget = 0;
    private int nextOpenSlot = 0;

    public int Count => nextOpenSlot - nextTarget;

    public void AddTarget(TargetData targetData)
    {
        targetDataHistory[nextOpenSlot] = targetData;
        nextOpenSlot++;
    }

    public TargetData PeekNextTarget()
    {
        return targetDataHistory[nextTarget];
    }

    public bool PopNextTarget(out TargetData targetData)
    {
        targetData = new();
        if (nextTarget == nextOpenSlot) return false;

        targetData = targetDataHistory[nextTarget];
        nextTarget++;
        return true;
    }

    public void Reset()
    {
        nextTarget = 0;
    }
}
