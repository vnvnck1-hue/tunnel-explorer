#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TunnelCrew.Presentation.Visual
{
    /// <summary>Editor-only end-to-end probe. Sends real Input System device states through
    /// LabPlayer/LabEnvironment and observes physics, mining and render rebuilds.</summary>
    public static class BackgroundLabSmoke
    {
        public static string Result {get; private set;}="Not run";
        static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        static void Keys(Keyboard keyboard,params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            // Background editor frames can contain many fixed ticks before a dynamic input update.
            // Flush this synthetic device event before measuring physics movement.
            InputSystem.Update();keyboard.MakeCurrent();
        }
        public static IEnumerator Run(BackgroundUpgradeLab lab)
        {
            Result="Running";
            var previousInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            var previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            bool mined=false;
            try
            {
                lab.ResetPlayer();yield return new WaitForFixedUpdate();yield return null;
                var start=lab.Player.CellPosition;
                Keys(keyboard,Key.D);yield return new WaitForSeconds(.18f);Keys(keyboard);
                yield return new WaitForFixedUpdate();
                Check(lab.Player.CellPosition.x>start.x+.3f,"WASD did not move the real player: "+start+" -> "+lab.Player.CellPosition+"; physics="+Physics2D.simulationMode+"; current="+Keyboard.current?.name);
                Keys(keyboard,Key.W);yield return new WaitForSeconds(.8f);Keys(keyboard);
                yield return new WaitForSeconds(.05f);
                var blocked=lab.Player.CellPosition;
                Check(blocked.y>start.y+.5f&&blocked.y<9.95f,"Player failed to stop against the wall island");
                Check(lab.Environment.Field.IsSolid(13,10),"Probe wall missing");
                int oldEdits=lab.Environment.Edits;int oldRebuild=lab.RebuildCount;
                var screen=lab.labCamera.WorldToScreenPoint(new Vector3(13.5f,10.5f,0));
                for(int i=0;i<3;i++)
                {
                    screen=lab.labCamera.WorldToScreenPoint(new Vector3(13.5f,10.5f,0));
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y),buttons=1});
                    yield return null;yield return null;
                    InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(screen.x,screen.y)});
                    yield return null;yield return null;
                }
                yield return new WaitForSeconds(.1f);
                mined=!lab.Environment.Field.IsSolid(13,10);
                Check(mined,"Three mouse clicks did not mine the reachable wall");
                Check(lab.Environment.Edits==oldEdits+1&&lab.RebuildCount>oldRebuild,"Mining did not rebuild the visual contour");
                Keys(keyboard,Key.W);yield return new WaitForSeconds(.18f);Keys(keyboard);yield return new WaitForSeconds(.05f);
                Check(lab.Player.CellPosition.y>blocked.y+.25f,"Mined passage remained blocked by collision");
                lab.ResetPlayer();yield return new WaitForSeconds(.05f);
                lab.Environment.SetCell(13,10,true);mined=false;yield return new WaitForSeconds(.1f);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit1));yield return null;yield return null;Keys(keyboard);yield return null;
                Check(lab.Mode==1,"Baseline hotkey failed");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit2));yield return null;yield return null;Keys(keyboard);yield return null;
                Check(lab.Mode==2,"Overlay hotkey failed");
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit3));yield return null;yield return null;Keys(keyboard);yield return null;
                Check(lab.Mode==3,"Organic hotkey failed");
                Result="PASS: keyboard movement; wall collision; 3-hit mouse mining; contour rebuild; passage opens; wall restored; A/B/C keyboard switching.";
                Debug.Log("[Background Lab Smoke] "+Result);
            }
            finally
            {
                if(mined)lab.Environment.SetCell(13,10,true);
                lab.ResetPlayer();lab.SetMode(3);
                InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);
                InputSystem.settings.editorInputBehaviorInPlayMode=previousInput;
                InputSystem.settings.backgroundBehavior=previousBackground;
                if(Result=="Running")Result="FAIL: see Console exception";
            }
        }
    }
}
#endif
