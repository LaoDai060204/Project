// Adapted from MDPro3 UI/New UI/ButtonHover.cs in this repository (GPL-3.0).
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PhuocDai.Saga
{
    public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Action hoverIn;
        public Action hoverOut;

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverIn?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverOut?.Invoke();

        }
    }
}
