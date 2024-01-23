using Components;
using UnityEngine;

namespace Samples.Events
{
    public class Action2 : MonoBehaviour
    {
        void Start()
        {
            EventCenter.Instance.AddEventListener("UnderAttack", BloodState);
        }

        private void BloodState()
        {
            print("Blooding...");
        }
    }
}
