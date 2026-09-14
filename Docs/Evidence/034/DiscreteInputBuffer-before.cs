using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Frontier
{
    // Capture transitions before Input System applies the event to device state.
    // A frame may contain several complete clicks and modifier changes. World
    // dispatch consumes this single FIFO, never reconstructing them from frame flags.
    internal sealed class DiscreteInputBuffer : IDisposable
    {
        internal enum EdgeKind { Left, Right, Key }
        internal struct Edge
        {
            public EdgeKind kind;
            public Vector2 position;
            public bool pressed,shift,saveGroup;
            public Key key;
            public double time;
        }
        private static readonly Key[] Keys={Key.Escape,Key.F9,Key.A,Key.S,Key.J,Key.H,Key.B,Key.G,Key.V,Key.Period,Key.T,Key.M,Key.F,
            Key.Digit0,Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8,Key.Digit9};
        private readonly Queue<Edge> edges=new Queue<Edge>();
        private readonly bool[] keyDown=new bool[Keys.Length];
        private Keyboard keyboard;
        private Mouse mouse;
        private Vector2 position;
        private bool leftDown,rightDown,shift,saveGroup;
        private readonly bool previousMerging,trace;
        public DiscreteInputBuffer(bool trace)
        {
            this.trace=trace;previousMerging=InputSystem.settings.disableRedundantEventsMerging;
            InputSystem.settings.disableRedundantEventsMerging=true;InputSystem.onEvent+=Capture;
        }
        public void Clear()=>edges.Clear();
        public void DiscardPointerEdges()
        {
            // Preserve keyboard FIFO across a UI transition without allocating
            // a new queue. Physical button state stays at the latest captured
            // event; discarding dispatch must not synthesize a new button press.
            int pending=edges.Count;
            for(int i=0;i<pending;i++){var edge=edges.Dequeue();if(edge.kind==EdgeKind.Key)edges.Enqueue(edge);}
        }
        public bool TryPeek(out Edge edge)
        {if(edges.Count==0){edge=default;return false;}edge=edges.Peek();return true;}
        public bool TryTake(out Edge edge)
        {if(edges.Count==0){edge=default;return false;}edge=edges.Dequeue();return true;}
        private static bool Pressed(KeyControl key,InputEventPtr ev)=>key.ReadValueFromEvent(ev,out float value)?value>=.5f:key.isPressed;
        private void Capture(InputEventPtr ev,InputDevice device)
        {
            if(!ev.IsA<StateEvent>()&&!ev.IsA<DeltaStateEvent>())return;
            if(device is Keyboard incomingKeyboard)
            {
                if(keyboard!=incomingKeyboard)
                {keyboard=incomingKeyboard;for(int i=0;i<Keys.Length;i++)keyDown[i]=keyboard[Keys[i]].isPressed;}
                shift=Pressed(keyboard.leftShiftKey,ev)||Pressed(keyboard.rightShiftKey,ev);
                saveGroup=Pressed(keyboard.leftCtrlKey,ev)||Pressed(keyboard.rightCtrlKey,ev)||Pressed(keyboard.leftMetaKey,ev)||Pressed(keyboard.rightMetaKey,ev);
                for(int i=0;i<Keys.Length;i++)
                {
                    if(!keyboard[Keys[i]].ReadValueFromEvent(ev,out float value))continue;
                    bool down=value>=.5f;
                    if(down&&!keyDown[i])Enqueue(new Edge{kind=EdgeKind.Key,key=Keys[i],pressed=true,time=ev.time,shift=shift,saveGroup=saveGroup,position=position});
                    keyDown[i]=down;
                }
                return;
            }
            if(!(device is Mouse incomingMouse))return;
            if(mouse!=incomingMouse)
            {mouse=incomingMouse;leftDown=mouse.leftButton.isPressed;rightDown=mouse.rightButton.isPressed;position=mouse.position.ReadValue();}
            if(mouse.position.ReadValueFromEvent(ev,out Vector2 point))position=point;
            // If both buttons change in one indivisible hardware state event,
            // left is handled before right. Distinct events retain arrival order.
            CaptureButton(mouse.leftButton,EdgeKind.Left,ref leftDown,ev);
            CaptureButton(mouse.rightButton,EdgeKind.Right,ref rightDown,ev);
        }
        private void CaptureButton(ButtonControl button,EdgeKind kind,ref bool previous,InputEventPtr ev)
        {
            if(!button.ReadValueFromEvent(ev,out float value))return;
            bool down=value>=.5f;if(down==previous)return;previous=down;
            Enqueue(new Edge{kind=kind,pressed=down,position=position,time=ev.time,shift=shift,saveGroup=saveGroup});
        }
        private void Enqueue(Edge edge)
        {
            edges.Enqueue(edge);
            if(trace)Debug.LogFormat(LogType.Log,LogOption.NoStacktrace,null,"{0}",$"FRONTIER_INPUT {edge.kind} {edge.key} {(edge.pressed?"down":"up")} p={edge.position} shift={edge.shift} save={edge.saveGroup} t={edge.time:F4}");
        }
        public void Dispose(){InputSystem.onEvent-=Capture;InputSystem.settings.disableRedundantEventsMerging=previousMerging;edges.Clear();}
    }
}
