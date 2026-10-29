using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Gamekit3D
{
    public class SpitterSMBIdle : SceneLinkedSMB<SpitterBehaviour>
    {
        public override void OnSLStateNoTransitionUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            base.OnSLStateNoTransitionUpdate(animator, stateInfo, layerIndex);

            m_MonoBehaviour.FindTarget();
            m_MonoBehaviour.CheckNeedFleeing();

            if (m_MonoBehaviour.target != null)
            {
                m_MonoBehaviour.TriggerAttack();
            }
        }
    }
}
