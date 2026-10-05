using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Frontier
{
    // Uses the HUD's ordered pointer dispatcher. Editing never injects world
    // commands; one complete press/drag/release becomes one undo transaction.
    public sealed class ScenarioEditorInput : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IDragHandler,IEndDragHandler
    {
        public Action<Vector2> Begin,Move,End;
        bool pressed;
        Vector2 Point(PointerEventData eventData)
        {
            var rect=(RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,eventData.position,eventData.pressEventCamera,out var p);
            return new Vector2(Mathf.Clamp01((p.x-rect.rect.xMin)/rect.rect.width)*128,
                Mathf.Clamp01((p.y-rect.rect.yMin)/rect.rect.height)*128);
        }
        public void OnPointerDown(PointerEventData eventData)
        {if(eventData.button!=PointerEventData.InputButton.Left)return;pressed=true;Begin?.Invoke(Point(eventData));}
        public void OnDrag(PointerEventData eventData){if(pressed)Move?.Invoke(Point(eventData));}
        public void OnPointerUp(PointerEventData eventData)
        {if(!pressed)return;pressed=false;End?.Invoke(Point(eventData));}
        public void OnEndDrag(PointerEventData eventData){OnPointerUp(eventData);}
        void OnDisable(){pressed=false;}
    }
}
