using System.Collections;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Presentation;
using TunnelCrew.Presentation.CRT;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TunnelCrew.Tests
{
    public sealed class CrtRuntimeTests
    {
        Keyboard _keyboard;Mouse _mouse;Gamepad _pad;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            Assert.That(Application.productName,Does.Contain("CRT Validation"),"Run only in isolated QA project to preserve player saves");
            _keyboard=InputSystem.AddDevice<Keyboard>();_mouse=InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/Run.unity");
            yield return null;yield return null;
            var c=CRTDisplayController.Instance;
            // RuntimeInitialize runs once per play session, not for every scene loaded by NUnit.
            if(c==null)c=new GameObject("CRT Display (test fixture)").AddComponent<CRTDisplayController>();
            Assert.That(c,Is.Not.Null);c.SetEnabled(true);c.SetAccessibility(CrtAccessibility.Standard);c.SetProfile(0);c.SetUiFocus(false);
            c.SetCaptureMode(false);c.SetCurvatureProfile(0);c.SettingsOpen=false;
            c.Strength=c.Scanlines=c.Curvature=c.Noise=c.Aberration=c.Vignette=1;
        }
        [UnityTearDown]
        public IEnumerator Teardown()
        {
            Time.timeScale=1;
            if(_keyboard!=null)InputSystem.RemoveDevice(_keyboard);
            if(_mouse!=null)InputSystem.RemoveDevice(_mouse);
            if(_pad!=null)InputSystem.RemoveDevice(_pad);
            if(CRTDisplayController.Instance!=null)Object.Destroy(CRTDisplayController.Instance.gameObject);
            yield return null;
        }
        IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard,new KeyboardState(keys));yield return null;
            InputSystem.QueueStateEvent(_keyboard,new KeyboardState());yield return null;
        }
        IEnumerator Pad(params GamepadButton[] buttons)
        {
            var state=new GamepadState();foreach(var button in buttons)state=state.WithButton(button);
            InputSystem.QueueStateEvent(_pad,state);yield return null;
            InputSystem.QueueStateEvent(_pad,new GamepadState());yield return null;
        }
        [UnityTest]
        public IEnumerator GamepadCanOpenNavigateAdjustAndCloseDisplaySettings()
        {
            _pad=InputSystem.AddDevice<Gamepad>();var c=CRTDisplayController.Instance;
            yield return Pad(GamepadButton.Select,GamepadButton.Start);Assert.That(c.SettingsOpen,Is.True);
            yield return Pad(GamepadButton.DpadDown);yield return Pad(GamepadButton.South);Assert.That(c.ProfileIndex,Is.EqualTo(1));
            for(int i=0;i<10;i++)yield return Pad(GamepadButton.DpadDown);
            yield return Pad(GamepadButton.DpadLeft);Assert.That(c.Strength,Is.EqualTo(.95f).Within(.001));
            Assert.That(c.UsingGamepad,Is.True);yield return Pad(GamepadButton.East);Assert.That(c.SettingsOpen,Is.False);
            yield return Press(Key.LeftArrow);Assert.That(c.UsingGamepad,Is.False);
            InputSystem.QueueStateEvent(_pad,new GamepadState{rightTrigger=1});yield return null;
            Assert.That(c.UsingGamepad,Is.True,"Firing with a trigger also changes the displayed input device");
            InputSystem.QueueStateEvent(_pad,new GamepadState());yield return null;
        }
        [UnityTest]
        public IEnumerator WarpedPointerSelectsVisiblePresetWithoutChangingWorldProjection()
        {
            var c=CRTDisplayController.Instance;c.SetProfile(2);c.SetCurvatureProfile(2);c.SettingsOpen=true;yield return null;yield return null;
            var text=Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Single(t=>t.text.StartsWith("02   "));
            var screen=RectTransformUtility.WorldToScreenPoint(CrtCameraStack.UiCamera,text.rectTransform.TransformPoint(text.rectTransform.rect.center));
            // Invert the glass map to physically click the visible (curved) label, not its unwarped rectangle.
            var target=screen/new Vector2(Screen.width,Screen.height)*2-Vector2.one;var p=target;
            float aspect=Mathf.Min(Screen.width/(float)Screen.height/(16f/9f),1.3f);
            for(int i=0;i<16;i++)p=target/(Vector2.one+new Vector2(p.y*p.y,p.x*p.x*aspect*aspect)*c.Effective.curvature);
            var physical=(p+Vector2.one)*.5f*new Vector2(Screen.width,Screen.height);
            var projection=IsometricProjection.Preset;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=physical,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=physical});yield return null;
            Assert.That(c.ProfileIndex,Is.EqualTo(1));Assert.That(IsometricProjection.Preset,Is.EqualTo(projection));
        }
        [UnityTest]
        public IEnumerator MonitorShortcutsAndSettingsSaveWithoutChangingProjection()
        {
            var c=CRTDisplayController.Instance;var projection=IsometricProjection.Preset;
            yield return Press(Key.F6);Assert.That(c.ProfileIndex,Is.EqualTo(1));
            yield return Press(Key.LeftShift,Key.F6);Assert.That(c.ProfileIndex,Is.Zero);
            yield return Press(Key.F7);Assert.That(c.DisplayEnabled,Is.False);
            yield return Press(Key.F7);Assert.That(c.DisplayEnabled,Is.True);
            yield return Press(Key.F10);Assert.That(c.SettingsOpen,Is.True);
            Assert.That(Object.FindObjectsByType<CrtSurface>(FindObjectsSortMode.None).Count(s=>s.Interactive),Is.EqualTo(1));
            yield return Press(Key.Escape);Assert.That(c.SettingsOpen,Is.False);
            Assert.That(IsometricProjection.Preset,Is.EqualTo(projection));
            Assert.That(PlayerPrefs.GetInt("tc.crt.enabled"),Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator AllThirtyCombinationsPreserveIndependentGeometryAndSurviveReload()
        {
            var c=CRTDisplayController.Instance;
            float[] values={.052f,.018f,.072f,.045f,.058f};
            Assert.That(c.Profiles.Length,Is.EqualTo(6));Assert.That(c.CurvatureProfiles.Length,Is.EqualTo(5));
            for(int g=0;g<5;g++)for(int e=0;e<6;e++)
            {
                c.SetCurvatureProfile(g);c.SetProfile(e);
                Assert.That(c.Effective.curvature,Is.EqualTo(values[g]).Within(.00001));
                Assert.That(c.Profile.effect,Is.EqualTo((CrtEffect)e));
                c.Strength=0;Assert.That(c.Effective.curvature,Is.EqualTo(values[g]).Within(.00001));c.Strength=1;
            }
            c.SetProfile(5);c.SetCurvatureProfile(4);
            yield return Press(Key.LeftCtrl,Key.F6);
            Assert.That(c.CurvatureIndex,Is.Zero);Assert.That(c.ProfileIndex,Is.EqualTo(5));
            yield return Press(Key.LeftCtrl,Key.LeftShift,Key.F6);Assert.That(c.CurvatureIndex,Is.EqualTo(4));
            yield return Press(Key.F6);Assert.That(c.ProfileIndex,Is.Zero);Assert.That(c.CurvatureIndex,Is.EqualTo(4));
            c.SetProfile(5);c.Curvature=.7f;c.Save();
            Object.Destroy(c.gameObject);yield return null;
            c=new GameObject("CRT reload test").AddComponent<CRTDisplayController>();yield return null;
            Assert.That(c.ProfileIndex,Is.EqualTo(5));Assert.That(c.CurvatureIndex,Is.EqualTo(4));
            Assert.That(c.Effective.curvature,Is.EqualTo(.058f*.7f).Within(.00001));
        }
        [UnityTest]
        public IEnumerator LegacyMonitorMigratesToGlassWithoutReinterpretingEffectIndex()
        {
            bool hadLegacy=PlayerPrefs.HasKey("tc.crt.monitor");int legacy=PlayerPrefs.GetInt("tc.crt.monitor");
            PlayerPrefs.DeleteKey("tc.crt.effect");PlayerPrefs.DeleteKey("tc.crt.glass");PlayerPrefs.SetInt("tc.crt.monitor",3);
            Object.Destroy(CRTDisplayController.Instance.gameObject);yield return null;
            var c=new GameObject("CRT migration test").AddComponent<CRTDisplayController>();yield return null;
            if(hadLegacy)PlayerPrefs.SetInt("tc.crt.monitor",legacy);else PlayerPrefs.DeleteKey("tc.crt.monitor");
            Assert.That(c.CurvatureIndex,Is.EqualTo(3));Assert.That(c.ProfileIndex,Is.EqualTo(5));
            c.Save();Assert.That(PlayerPrefs.GetInt("tc.crt.glass"),Is.EqualTo(3));
            Assert.That(PlayerPrefs.GetInt("tc.crt.effect"),Is.EqualTo(5));
        }
        [UnityTest]
        public IEnumerator MonitorButtonClickPausesBeforeWorldInputAndRespectsChatFocus()
        {
            Object.FindFirstObjectByType<MetaScreens>().Show(MetaScreens.Screen.Run);
            yield return new WaitForSecondsRealtime(.6f);
            var c=CRTDisplayController.Instance;c.SetEnabled(false);yield return null;
            var label=Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None)
                .Single(t=>t.isActiveAndEnabled&&t.text.StartsWith("CRT OFF"));
            var point=RectTransformUtility.WorldToScreenPoint(CrtCameraStack.UiCamera,label.rectTransform.TransformPoint(label.rectTransform.rect.center));
            var run=Object.FindFirstObjectByType<RunBootstrap>();double time=run.Sim.RunTime;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=point,buttons=1});yield return null;
            Assert.That(c.SettingsOpen,Is.True);Assert.That(run.Sim.RunTime,Is.EqualTo(time));
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=point});yield return null;
            c.CloseSettings();yield return new WaitForSecondsRealtime(.2f);
            yield return Press(Key.Enter);Assert.That(c.TextFocus,Is.True);
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=point,buttons=1});yield return null;
            Assert.That(c.SettingsOpen,Is.False,"Finish chat before opening display controls");
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=point});yield return null;
        }
        [UnityTest]
        public IEnumerator AllGameCanvasesAreBeforeFinalGlassAndUseUiSortingLayer()
        {
            var canvases=Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Assert.That(canvases.Length,Is.GreaterThanOrEqualTo(5));
            foreach(var canvas in canvases)
            {
                Assert.That(canvas.renderMode,Is.EqualTo(RenderMode.ScreenSpaceCamera),canvas.name);
                Assert.That(canvas.worldCamera,Is.Not.Null,canvas.name);
                Assert.That(canvas.sortingLayerName,Is.EqualTo("UI"),canvas.name);
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator AccessibilityAndTextFocusSuppressEventsAndCaptureClockIsStable()
        {
            var c=CRTDisplayController.Instance;c.SetProfile(1);c.SignalHit(1);
            Assert.That(c.EventAmount,Is.GreaterThan(0));
            c.Strength=0;Assert.That(c.EventAmount,Is.Zero);c.Strength=1;
            c.Noise=0;Assert.That(c.EventAmount,Is.Zero);c.Noise=1;
            c.SetUiFocus(true);Assert.That(c.EventAmount,Is.Zero);Assert.That(c.Effective.jitterStrengthPixels,Is.Zero);
            c.SetUiFocus(false);c.SetAccessibility(CrtAccessibility.Photosensitive);c.SignalDropout(1);
            Assert.That(c.EventAmount,Is.Zero);Assert.That(c.Effective.persistence,Is.Zero);
            float t=c.Clock;yield return null;yield return null;Assert.That(c.Clock,Is.EqualTo(t));
            c.SetAccessibility(CrtAccessibility.Standard);c.SetCaptureMode(true);t=c.Clock;
            yield return null;yield return null;Assert.That(c.Clock,Is.EqualTo(t));
        }
        [UnityTest]
        public IEnumerator NativeChatCaretFollowsCurvedMousePosition()
        {
            Object.FindFirstObjectByType<MetaScreens>().Show(MetaScreens.Screen.Run);
            yield return new WaitForSecondsRealtime(.6f);
            yield return Press(Key.Enter);
            var c=CRTDisplayController.Instance;c.SetProfile(2);
            var field=Object.FindFirstObjectByType<CrtInputField>();Assert.That(field,Is.Not.Null);
            field.SetTextWithoutNotify("굴착선 신호 확인 ABC 123");field.ActivateInputField();
            yield return null;yield return null;
            var rect=field.textComponent.rectTransform;
            Vector2 Physical(Vector2 local)
            {
                var screen=RectTransformUtility.WorldToScreenPoint(CrtCameraStack.UiCamera,rect.TransformPoint(local));
                var target=screen/new Vector2(Screen.width,Screen.height)*2-Vector2.one;var p=target;
                float aspect=Mathf.Min(Screen.width/(float)Screen.height/(16f/9f),1.3f);
                for(int i=0;i<16;i++)p=target/(Vector2.one+new Vector2(p.y*p.y,p.x*p.x*aspect*aspect)*c.Effective.curvature);
                return (p+Vector2.one)*.5f*new Vector2(Screen.width,Screen.height);
            }
            var left=Physical(new Vector2(rect.rect.xMin+1,rect.rect.center.y));
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=left,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=left});yield return null;
            Assert.That(field.caretPosition,Is.Zero,"Click the visible left edge, not the flat rectangle");
            var right=Physical(new Vector2(rect.rect.xMax-3,rect.rect.center.y));
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=right,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(_mouse,new MouseState{position=right});yield return null;
            Assert.That(field.caretPosition,Is.EqualTo(field.text.Length));
        }
        [UnityTest]
        public IEnumerator NativeChatEditsUnicodeAndSubmitsAfterInputFieldDeactivates()
        {
            Object.FindFirstObjectByType<MetaScreens>().Show(MetaScreens.Screen.Run);
            yield return new WaitForSecondsRealtime(.6f);
            yield return Press(Key.Enter);
            yield return new WaitForSecondsRealtime(.2f);
            var field=Object.FindFirstObjectByType<CrtInputField>();
            Assert.That(field,Is.Not.Null);Assert.That(field.isFocused,Is.True);
            field.SetTextWithoutNotify("");field.MoveTextEnd(false);
            foreach(char ch in "심층 ABC")
                field.ProcessEvent(new Event{type=EventType.KeyDown,character=ch});
            Assert.That(field.text,Is.EqualTo("심층 ABC"));
            field.ProcessEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.Backspace});
            field.ProcessEvent(new Event{type=EventType.KeyDown,keyCode=KeyCode.LeftArrow});
            field.ProcessEvent(new Event{type=EventType.KeyDown,character='호'});
            Assert.That(field.text,Is.EqualTo("심층 A호B"));
            // Match InputField.OnUpdateSelected's actual submit -> deactivate ordering.
            // This validates committed Unicode editing, not the OS IME candidate window.
            field.onSubmit.Invoke(field.text);field.DeactivateInputField();
            yield return null;yield return null;
            var team=Object.FindFirstObjectByType<TeamOverlay>();
            Assert.That(team.ChatOpen,Is.False);
            Assert.That(CRTDisplayController.Instance.TextFocus,Is.False);
            var run=Object.FindFirstObjectByType<RunBootstrap>();
            Assert.That(run.Sim.Chat.Log.Any(m=>m.Text=="심층 A호B"),Is.True);
            yield return Press(Key.Enter);yield return null;
            Assert.That(team.ChatOpen,Is.True);
            // Adding a chat row changes the pooled element index; inspect the active editor,
            // not the now-hidden input element from the previous log layout.
            var reopened=Object.FindObjectsByType<CrtInputField>(FindObjectsSortMode.None).Single(f=>f.isActiveAndEnabled&&f.isFocused);
            Assert.That(reopened.text,Is.Empty);
        }
        [UnityTest]
        public IEnumerator RunCraftAndChatKeepExistingInputFlow()
        {
            var meta=Object.FindFirstObjectByType<MetaScreens>();meta.Show(MetaScreens.Screen.Run);
            yield return new WaitForSecondsRealtime(1);
            var run=Object.FindFirstObjectByType<RunBootstrap>();
            Assert.That(run.RunActive,Is.True);Assert.That(run.CurrentHud.Hp,Is.EqualTo(run.Sim.Player.Hp));
            yield return Press(Key.C);Assert.That(run.Sim.Craft.Phase,Is.EqualTo(TunnelCrew.Sim.CraftPhase.Wheel));
            yield return Press(Key.Escape);Assert.That(run.Sim.Craft.Phase,Is.EqualTo(TunnelCrew.Sim.CraftPhase.Closed));
            yield return Press(Key.Enter);
            var team=Object.FindFirstObjectByType<TeamOverlay>();Assert.That(team.ChatOpen,Is.True);
            Assert.That(CRTDisplayController.Instance.TextFocus,Is.True);
            Assert.That(Object.FindObjectsByType<UnityEngine.UI.InputField>(FindObjectsSortMode.None).Length,Is.GreaterThan(0));
            yield return Press(Key.Escape);Assert.That(team.ChatOpen,Is.False);
            Assert.That(CRTDisplayController.Instance.TextFocus,Is.False);
        }
    }
}
