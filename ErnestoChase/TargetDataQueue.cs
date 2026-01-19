using System;
using System.Collections.Generic;
using UnityEngine;

namespace ErnestoChase;

public class TargetDataQueue
{
    [Serializable]
    public struct TargetData(string parent, Vector3 localPosition, Vector3 worldPosition, Vector3 worldUp,
        float time, bool isTeleportEnter = false, bool isTeleportExit = false, bool isFinalTarget = false,
        bool isInRingWorld = false)
    {
        public string parent = parent;
        public Vector3 localPosition = localPosition;
        public Vector3 worldPosition = worldPosition;
        public Vector3 worldUp = worldUp;
        public float time = time;
        public bool isTeleportEnter = isTeleportEnter;
        public bool isTeleportExit = isTeleportExit;
        public bool isFinalTarget = isFinalTarget;
        public bool isInRingWorld = isInRingWorld;
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

    public bool PeekCurrentTarget(out TargetData targetData)
    {
        if (nextTarget == nextOpenSlot)
        {
            targetData = targetDataHistory[nextTarget - 1];
            ErnestoChase.WriteDebugMessage("Tried to peek but couldn't");
            return false;
        }

        targetData = targetDataHistory[nextTarget];
        return true;
    }

    public bool PopCurrentTarget(out TargetData targetData)
    {
        if (nextTarget == nextOpenSlot)
        {
            targetData = targetDataHistory[nextTarget - 1];
            ErnestoChase.WriteDebugMessage("Tried to pop but couldn't");
            return false;
        }

        targetData = targetDataHistory[nextTarget];
        nextTarget++;
        return true;
    }

    public void Reset()
    {
        nextTarget = 0;
    }

    public bool DebugTeleportCheck()
    {
        int num = 0;
        for (int i = 0; i <= nextOpenSlot; i++)
        {
            if (targetDataHistory[i].isTeleportEnter || targetDataHistory[i].isTeleportExit ||
                targetDataHistory[i].isFinalTarget)
            {
                return true;
            }
        }

        return false;
    }
}
