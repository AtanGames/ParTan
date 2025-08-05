using ParTan.Core.Data;
using UnityEngine;
using UnityEngine.Serialization;

namespace ParTan.Core
{
    [CreateAssetMenu(fileName = "ParTanConfig", menuName = "ParTan/Configuration")]
    public class PartanConfig : ScriptableObject
    {
        [Header("General")]
        
        public SimConstants simConstants;
    }
}
