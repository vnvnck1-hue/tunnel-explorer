using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TunnelCrew.Presentation.CRT
{
    public sealed class CrtInputField : InputField
    {
        PointerEventData _drag;
        string _submitted;
        bool _submitPending;
        protected override void Awake()
        {
            base.Awake();
            onSubmit.AddListener(QueueSubmit);
        }
        void QueueSubmit(string value) { _submitted=value; _submitPending=true; }
        public bool TryConsumeSubmit(out string value)
        {
            value=_submitted;
            bool pending=_submitPending;
            _submitPending=false;_submitted=null;
            return pending;
        }
        protected override void OnDisable()
        {
            _submitPending=false;_submitted=null;
            base.OnDisable();
        }
        public override void OnDrag(PointerEventData data)
        {
            // InputField's outside-selection coroutine retains its event object. Give it a stable,
            // mapped copy so restoring the shared EventSystem event cannot unwarp it on later frames.
            if(_drag==null)_drag=new PointerEventData(EventSystem.current);
            _drag.position=CrtGui.GlassToContentScreen(data.position);
            _drag.button=data.button;
            _drag.pointerPressRaycast=data.pointerPressRaycast;
            _drag.pointerCurrentRaycast=data.pointerCurrentRaycast;
            base.OnDrag(_drag);
            data.Use();
        }
    }
}
