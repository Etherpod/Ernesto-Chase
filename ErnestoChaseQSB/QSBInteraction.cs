using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using System.Reflection;
using UnityEngine;

namespace ErnestoChaseQSB;

public class QSBInteraction : MonoBehaviour
{
    public void Start()
    {
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
    }
}