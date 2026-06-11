using UnityEngine;

namespace Ignitives.MultiplayerEngine
{
    /// <summary>
    /// StateMachineBehaviour attached to each combo attack animation state.
    /// Handles combo window timing internally via OnStateUpdate.
    /// Manages layer weight: 1 while any combo state is active, 0 when combo fully ends.
    /// </summary>
    public class ComboAttackBehavior : StateMachineBehaviour
    {
        [Header("Combo")]
        [Tooltip("Index of this attack in the combo chain (0-based)")]
        public int attackIndex = 0;

        [Header("Combo Window (Normalized 0-1)")]
        [Tooltip("When combo input starts being accepted")]
        [Range(0, 1)] public float comboWindowStart = 0.4f;

        [Tooltip("When combo input stops - transition point to next attack")]
        [Range(0, 1)] public float comboWindowEnd = 0.85f;

        // Per-state tracking
        private bool comboWindowOpened;
        private bool comboWindowClosed;
        private EquipmentController equipController;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            comboWindowOpened = false;
            comboWindowClosed = false;

            if (equipController == null)
                equipController = animator.GetComponentInParent<EquipmentController>();

            // Layer weight → 1 when any combo state starts
            animator.SetLayerWeight(layerIndex, 1f);
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            float normalizedTime = stateInfo.normalizedTime % 1f;

            // Open combo window
            if (!comboWindowOpened && normalizedTime >= comboWindowStart)
            {
                comboWindowOpened = true;
                equipController?.OnComboWindowStart();
            }

            // Close combo window
            if (!comboWindowClosed && normalizedTime >= comboWindowEnd)
            {
                comboWindowClosed = true;
                equipController?.OnComboWindowEnd();
            }
        }

        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // If combo is continuing (next attack already started), do nothing —
            // the next state's OnStateEnter will keep the weight at 1.
            if (equipController != null && equipController.IsInComboAttack)
                return;

            // Combo is truly done — clean up layer weight and root motion
            animator.SetLayerWeight(layerIndex, 0f);
            animator.applyRootMotion = false;

            // Signal handler to unlock movement and reset combo state
            equipController?.OnAttackExitEnd();
        }
    }
}