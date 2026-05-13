using BeatSaberMarkupLanguage;
using HMUI;
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AccsaberLeaderboard.UI.BSML_Addons.Components
{
    [RequireComponent(typeof(Graphic))]
    internal class MyEventSystemListener : EventSystemListener, IPointerClickHandler
    {
#pragma warning disable IDE1006
        public event Action<PointerEventData> pointerDidClickEvent;
        public void OnPointerClick(PointerEventData eventData)
        {
#if !NEW_VERSION
            BeatSaberUI.BasicUIAudioManager.HandleButtonClickEvent();
#endif
            pointerDidClickEvent?.Invoke(eventData);
        }
    }
}
