using AccSaber.Consts;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using HMUI;
using System;
using UnityEngine;

namespace AccSaber.Utils.Misc
{
    internal abstract class BasicModalController
    {
        protected bool parsed = false;

        [UIComponent("modal")]
        protected readonly ModalView Modal = null!;

        protected abstract void FirstParse(Transform parent);
        protected virtual void Parse(Transform parent)
        {
            if (!parsed)
            {
                FirstParse(parent);

                parsed = true;
            }

            Modal.transform.SetParent(parent);
        }
        public virtual void ShowModal(Transform parent, bool animated = true)
        {
            Parse(parent);

            Modal.Show(animated, true);
        }

        public virtual void HideModal(bool animated = true)
        {
            Modal.Hide(animated);
        }
    }
}
