using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Frontier
{
    // UGUI still owns raycasting, selection, Button behavior and navigation.
    // Only discrete pointer presses/releases use the ordered native event positions.
    // InputSystemUIInputModule's frame-final point remains responsible for hover.
    internal sealed class UiPointerDispatcher
    {
        private readonly EventSystem eventSystem;
        private readonly bool trace;
        private readonly PointerEventData pointer;
        private readonly List<RaycastResult> hits=new List<RaycastResult>();
        public bool IsPressed {get;private set;}
        public UiPointerDispatcher(EventSystem eventSystem,bool trace=false)
        {this.trace=trace;this.eventSystem=eventSystem;pointer=new PointerEventData(eventSystem){pointerId=-1,button=PointerEventData.InputButton.Left};}
        private GameObject Raycast(Vector2 position)
        {
            pointer.position=position;hits.Clear();eventSystem.RaycastAll(pointer,hits);
            pointer.pointerCurrentRaycast=hits.Count>0?hits[0]:default;
            if(trace)Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"FRONTIER_UI raycast p={0} hit={1} frame={2}",position,pointer.pointerCurrentRaycast.gameObject?.name.Replace('\n',' '),Time.frameCount);
            return pointer.pointerCurrentRaycast.gameObject;
        }
        public bool Process(Vector2 position,bool down)
        {
            if(down)
            {
                Cancel();var over=Raycast(position);if(over==null)return false;
                IsPressed=true;pointer.pressPosition=position;pointer.eligibleForClick=true;
                pointer.pointerPressRaycast=pointer.pointerCurrentRaycast;
                pointer.clickCount=1;pointer.clickTime=Time.unscaledTime;
                var selected=ExecuteEvents.GetEventHandler<ISelectHandler>(over);
                if(selected!=eventSystem.currentSelectedGameObject)eventSystem.SetSelectedGameObject(null,pointer);
                pointer.pointerPress=ExecuteEvents.ExecuteHierarchy(over,pointer,ExecuteEvents.pointerDownHandler)
                    ??ExecuteEvents.GetEventHandler<IPointerClickHandler>(over);
                pointer.pointerDrag=ExecuteEvents.GetEventHandler<IDragHandler>(over);pointer.dragging=false;
                if(pointer.pointerDrag!=null)ExecuteEvents.Execute(pointer.pointerDrag,pointer,ExecuteEvents.initializePotentialDrag);
                pointer.rawPointerPress=over;return true;
            }
            if(!IsPressed)return false;
            Move(position);var releasedOver=Raycast(position);var pressed=pointer.pointerPress;
            bool click=pointer.eligibleForClick&&pressed!=null&&pressed==ExecuteEvents.GetEventHandler<IPointerClickHandler>(releasedOver);
            // Clear capture before invoking callbacks: a callback may close its
            // modal, restart the match or cancel pending world input.
            IsPressed=false;
            if(pressed!=null)ExecuteEvents.Execute(pressed,pointer,ExecuteEvents.pointerUpHandler);
            if(pointer.dragging&&pointer.pointerDrag!=null)ExecuteEvents.Execute(pointer.pointerDrag,pointer,ExecuteEvents.endDragHandler);
            if(click&&pressed!=null)ExecuteEvents.Execute(pressed,pointer,ExecuteEvents.pointerClickHandler);
            pointer.pointerPress=null;pointer.rawPointerPress=null;pointer.pointerDrag=null;pointer.dragging=false;pointer.eligibleForClick=false;return true;
        }
        public void Move(Vector2 position)
        {
            if(!IsPressed||pointer.pointerDrag==null)return;
            pointer.delta=position-pointer.position;pointer.position=position;
            if(!pointer.dragging&&(position-pointer.pressPosition).sqrMagnitude>=eventSystem.pixelDragThreshold*eventSystem.pixelDragThreshold)
            {
                ExecuteEvents.Execute(pointer.pointerDrag,pointer,ExecuteEvents.beginDragHandler);pointer.dragging=true;
                if(pointer.pointerDrag!=pointer.pointerPress){if(pointer.pointerPress!=null)ExecuteEvents.Execute(pointer.pointerPress,pointer,ExecuteEvents.pointerUpHandler);pointer.pointerPress=null;pointer.eligibleForClick=false;}
            }
            if(pointer.dragging)ExecuteEvents.Execute(pointer.pointerDrag,pointer,ExecuteEvents.dragHandler);
        }
        public void CancelWithin(Transform first,Transform second)
        {
            var pressed=pointer.rawPointerPress;
            if(!IsPressed||pressed==null)return;
            var target=pressed.transform;
            if(first!=null&&target.IsChildOf(first)||second!=null&&target.IsChildOf(second))Cancel();
        }
        public void Cancel()
        {
            if(IsPressed&&pointer.pointerPress!=null)ExecuteEvents.Execute(pointer.pointerPress,pointer,ExecuteEvents.pointerUpHandler);
            if(pointer.dragging&&pointer.pointerDrag!=null)ExecuteEvents.Execute(pointer.pointerDrag,pointer,ExecuteEvents.endDragHandler);
            IsPressed=false;pointer.pointerPress=null;pointer.rawPointerPress=null;pointer.pointerDrag=null;pointer.dragging=false;pointer.eligibleForClick=false;
        }
    }
}
