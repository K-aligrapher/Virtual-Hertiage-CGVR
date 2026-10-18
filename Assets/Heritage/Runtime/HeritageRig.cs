using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace VirtualHeritage
{
    public sealed class HeritageRig : MonoBehaviour
    {
        public Camera view;
        public XROrigin origin;
        public bool IsVR { get; private set; }
        public bool ForceDesktop { get; set; }
        public TeleportationProvider TeleportProvider { get; private set; }
        public int TeleportCount { get; private set; }
        HeritageApp app;
        Transform offset;
        GameObject hands;
        TrackedPoseDriver headPose;
        readonly List<InputAction> actions = new List<InputAction>();
        readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
        InputAction turn, rotate, menu;
        HeritageTarget hovered;
        float yaw, pitch, snapReadyAt, eyeLag, veilAlpha;
        // Screen veil used by location transitions; also shown when the head enters solid geometry.
        public float TransitionFade { get; set; }
        Renderer veil;
        Material veilMaterial;
        const float StepHeight = .36f, BodyRadius = .25f, EyeHeight = 1.7f;

        public void Initialize(HeritageApp owner)
        {
            app = owner;
            gameObject.name = "XR Origin - headset or desktop";
            origin = gameObject.AddComponent<XROrigin>(); origin.Origin = gameObject;
            offset = new GameObject("Camera floor offset").transform; offset.SetParent(transform,false);
            origin.CameraFloorOffsetObject = offset.gameObject;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 1.7f;
            var cameraObject = new GameObject("Heritage camera"); cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(offset,false);
            view = cameraObject.AddComponent<Camera>(); view.nearClipPlane = .08f; view.farClipPlane = 160;
            view.fieldOfView = 62; view.allowHDR = true;
            view.backgroundColor = new Color(.64f,.76f,.77f);
            cameraObject.AddComponent<AudioListener>(); origin.Camera = view;
            CreateVeil();
            headPose = cameraObject.AddComponent<TrackedPoseDriver>();
            headPose.positionInput = new InputActionProperty(Action("Head position", "<XRHMD>/centerEyePosition"));
            headPose.rotationInput = new InputActionProperty(Action("Head rotation", "<XRHMD>/centerEyeRotation"));
            headPose.trackingStateInput = new InputActionProperty(Action("Head tracking", "<XRHMD>/trackingState"));
            headPose.enabled = false;

            var events = new GameObject("XR and desktop event system"); events.transform.SetParent(transform,false);
            events.AddComponent<EventSystem>();
            var input = events.AddComponent<XRUIInputModule>();
            input.enableMouseInput = true; input.enableTouchInput = true; input.enableXRInput = true;
            input.enableBuiltinActionsAsFallback = true;
            events.GetComponent<EventSystem>().sendNavigationEvents = false;
            gameObject.AddComponent<XRInteractionManager>();
            var transformer = gameObject.AddComponent<XRBodyTransformer>(); transformer.xrOrigin = origin;
            var mediator = gameObject.AddComponent<LocomotionMediator>();
            TeleportProvider = gameObject.AddComponent<TeleportationProvider>(); TeleportProvider.mediator = mediator;
            hands = new GameObject("Tracked controllers"); hands.transform.SetParent(offset,false);
            Controller("LeftHand"); Controller("RightHand"); hands.SetActive(false);
            turn = Action("Snap turn", "<XRController>{RightHand}/primary2DAxis");
            rotate = Action("Inspect rotation", "<XRController>{LeftHand}/primary2DAxis");
            menu = Action("Gallery", "<XRController>{LeftHand}/menuButton", InputActionType.Button);
            menu.AddBinding("<XRController>{LeftHand}/primaryButton");
        }

        // A near-plane quad that darkens the view; drawn after everything so it covers geometry the head is inside.
        void CreateVeil()
        {
            var mesh = new Mesh { name = "View veil" };
            mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0), new Vector3(1, -1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.uv = new[] { new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(.5f, .5f) };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4);
            var go = new GameObject("Comfort veil"); go.transform.SetParent(view.transform, false);
            go.transform.localPosition = new Vector3(0, 0, view.nearClipPlane + .02f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            veil = go.AddComponent<MeshRenderer>();
            veilMaterial = new Material(HeritageGeometry.FindShader("Sprites/Default")) { name = "Comfort veil", renderQueue = 4000, color = new Color(.02f, .03f, .03f, 0) };
            veil.sharedMaterial = veilMaterial; veil.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; veil.receiveShadows = false;
            veil.enabled = false;
        }

        void UpdateVeil()
        {
            bool inside = Physics.CheckSphere(view.transform.position, .12f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            veilAlpha = Mathf.MoveTowards(veilAlpha, inside ? 1 : 0, Time.unscaledDeltaTime * 5);
            float alpha = Mathf.Max(veilAlpha, IsVR ? TransitionFade : 0);
            veil.enabled = alpha > .001f;
            if (veil.enabled) veilMaterial.color = new Color(.02f, .03f, .03f, alpha);
        }

        // Moves the feet with wall collision, stepping onto ledges up to StepHeight and sliding along walls.
        bool Walk(ref Vector3 feet, Vector3 delta)
        {
            if (delta.sqrMagnitude < 1e-8f) return false;
            return TryStep(ref feet, delta) || TryStep(ref feet, new Vector3(delta.x, 0, 0)) || TryStep(ref feet, new Vector3(0, 0, delta.z));
        }

        bool TryStep(ref Vector3 feet, Vector3 delta)
        {
            if (delta.sqrMagnitude < 1e-8f) return false;
            var target = feet + delta;
            var bottom = target + Vector3.up * (StepHeight + BodyRadius); var top = target + Vector3.up * (EyeHeight - BodyRadius + .05f);
            if (Physics.CheckCapsule(bottom, top, BodyRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            if (!Physics.Raycast(target + Vector3.up * (StepHeight + .05f), Vector3.down, out var hit, StepHeight + 1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            feet = new Vector3(target.x, hit.point.y, target.z);
            return true;
        }

        static Vector3 ClampToGrounds(Vector3 p) => new Vector3(Mathf.Clamp(p.x, -16.6f, 16.6f), p.y, Mathf.Clamp(p.z, -34f, 22f));

        InputAction Action(string name, string binding, InputActionType type = InputActionType.Value)
        {
            var action = new InputAction(name, type, binding); action.Enable(); actions.Add(action); return action;
        }

        XRInputButtonReader ButtonReader(InputAction action)
        {
            return new XRInputButtonReader { inputSourceMode = XRInputButtonReader.InputSourceMode.InputAction,
                inputActionPerformed = action, inputActionValue = action };
        }

        void Controller(string hand)
        {
            var controller = new GameObject(hand); controller.SetActive(false); controller.transform.SetParent(hands.transform,false);
            string path = "<XRController>{" + hand + "}/";
            var pose = controller.AddComponent<TrackedPoseDriver>();
            pose.positionInput = new InputActionProperty(Action(hand + " aim position",path + "pointerPosition"));
            pose.rotationInput = new InputActionProperty(Action(hand + " aim rotation",path + "pointerRotation"));
            pose.trackingStateInput = new InputActionProperty(Action(hand + " tracking",path + "trackingState"));
            var press = Action(hand + " select",path + "triggerPressed",InputActionType.Button);
            var ray = controller.AddComponent<XRRayInteractor>();
            ray.selectInput = ButtonReader(press); ray.uiPressInput = ButtonReader(press);
            ray.enableUIInteraction = true; ray.maxRaycastDistance = 30; ray.hitClosestOnly = true;
            ray.lineType = XRRayInteractor.LineType.StraightLine;
            var line = controller.AddComponent<LineRenderer>(); line.positionCount = 2;
            line.sharedMaterial = app.RayMaterial;
            var visual = controller.AddComponent<XRInteractorLineVisual>(); visual.lineWidth = .008f;
            visual.lineLength = 12; visual.smoothMovement = true;
            var gradient = new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(new Color(.25f,.85f,.7f),0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.4f,1)});
            visual.validColorGradient = gradient;
            controller.SetActive(true);
        }

        void Update()
        {
            subsystems.Clear(); SubsystemManager.GetSubsystems(subsystems);
            bool active = !ForceDesktop && subsystems.Exists(s => s.running);
            if (active != IsVR)
            {
                IsVR = active; hands.SetActive(active); headPose.enabled = active;
                if (!active) { offset.localPosition = Vector3.zero; view.transform.localPosition = Vector3.up * 1.7f; }
                else view.transform.localPosition = Vector3.zero;
                app.ui?.SetVR(active);
                app.Overview();
            }
            UpdateVeil();
            if (app.Transitioning) return;
            if (IsVR)
            {
                var axis = turn.ReadValue<Vector2>();
                if (Mathf.Abs(axis.x) < .25f) snapReadyAt = 0;
                if (Mathf.Abs(axis.x) > .7f && Time.unscaledTime > snapReadyAt)
                {
                    origin.RotateAroundCameraUsingOriginUp(Mathf.Sign(axis.x) * 30); snapReadyAt = Time.unscaledTime + .45f;
                }
                var stick = rotate.ReadValue<Vector2>();
                if (app.Inspecting) app.RotateInspection(stick.x * -85 * Time.deltaTime);
                else
                {
                    // Smooth head-relative locomotion on the left stick, plus WASD for seated testing.
                    var keyboard = Keyboard.current;
                    if (keyboard != null) stick += new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0), (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0));
                    if (stick.magnitude > .2f)
                    {
                        var heading = Quaternion.Euler(0, view.transform.eulerAngles.y, 0);
                        var delta = heading * new Vector3(stick.x, 0, stick.y) * (Vector2.ClampMagnitude(stick, 1).magnitude * 1.8f * Time.deltaTime / Mathf.Max(stick.magnitude, 1e-3f));
                        var feet = new Vector3(view.transform.position.x, transform.position.y, view.transform.position.z);
                        var start = feet;
                        if (Walk(ref feet, delta)) transform.position = ClampToGrounds(transform.position + (feet - start));
                    }
                }
                if (menu.WasPressedThisFrame()) app.OpenHub();
                return;
            }
            DesktopInput();
        }

        void DesktopInput()
        {
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { if (app.Inspecting) app.CloseInspection(); else app.OpenHub(); }
                if (keyboard.tKey.wasPressedThisFrame) app.SetThen(!app.Then);
                if (keyboard.iKey.wasPressedThisFrame) app.ToggleInspection();
                if (keyboard.fKey.wasPressedThisFrame) app.ToggleWireframe();
                if (keyboard.hKey.wasPressedThisFrame) app.OpenHub();
                if (keyboard.qKey.isPressed) { if (app.Inspecting) app.RotateInspection(80 * Time.deltaTime); else yaw -= 65 * Time.deltaTime; }
                if (keyboard.eKey.isPressed) { if (app.Inspecting) app.RotateInspection(-80 * Time.deltaTime); else yaw += 65 * Time.deltaTime; }
                if (keyboard.leftArrowKey.isPressed && app.Inspecting) app.RotateInspection(-80 * Time.deltaTime);
                if (keyboard.rightArrowKey.isPressed && app.Inspecting) app.RotateInspection(80 * Time.deltaTime);
                if (!app.Inspecting)
                {
                    if (keyboard.leftArrowKey.isPressed) yaw -= 70 * Time.deltaTime;
                    if (keyboard.rightArrowKey.isPressed) yaw += 70 * Time.deltaTime;
                }
                if (!app.Inspecting)
                {
                    Vector3 step = new Vector3((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),0,
                        ((keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) ? 1 : 0) - ((keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) ? 1 : 0));
                    bool run = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
                    step = Quaternion.Euler(0,yaw,0) * Vector3.ClampMagnitude(step,1) * (run ? 5.5f : 2.6f) * Time.deltaTime;
                    if (step.sqrMagnitude > 0)
                    {
                        if (view.transform.localPosition.y > EyeHeight + .01f)
                        {
                            // Leaving the elevated overview: drop to eye level on the ground below.
                            var drop = transform.position;
                            if (Physics.Raycast(drop + Vector3.up * 2, Vector3.down, out var ground, 4, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) drop.y = ground.point.y;
                            transform.position = drop; eyeLag = 0; view.transform.localPosition = Vector3.up * EyeHeight;
                        }
                        var feet = transform.position; float before = feet.y;
                        if (Walk(ref feet,step)) { eyeLag -= feet.y - before; transform.position = ClampToGrounds(feet); }
                    }
                }
            }
            // Ease the eye over steps instead of popping between ledges.
            if (view.transform.localPosition.y <= EyeHeight + .01f || eyeLag != 0)
            {
                eyeLag = Mathf.MoveTowards(eyeLag,0,Time.deltaTime * 2.2f);
                if (view.transform.localPosition.y <= EyeHeight + .5f) view.transform.localPosition = Vector3.up * (EyeHeight + eyeLag);
            }
            bool overUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            if (mouse != null)
            {
                if (mouse.rightButton.isPressed && !overUI)
                {
                    var delta = mouse.delta.ReadValue(); yaw += delta.x * .12f; pitch = Mathf.Clamp(pitch - delta.y * .12f,-65,70);
                }
                if (app.Inspecting && !overUI) app.ZoomInspection(mouse.scroll.ReadValue().y * .001f);
                HeritageTarget next = null;
                if (!overUI && !app.Inspecting && Physics.Raycast(view.ScreenPointToRay(mouse.position.ReadValue()),out var hit,100)) next = hit.collider.GetComponentInParent<HeritageTarget>();
                if (hovered != next) { if (hovered) hovered.Highlight(false); hovered = next; if (hovered) hovered.Highlight(true); }
                if (hovered && mouse.leftButton.wasPressedThisFrame) hovered.Activate();
            }
            if (!app.Inspecting) view.transform.rotation = Quaternion.Euler(pitch,yaw,0);
        }

        public void Place(Vector3 feet, Vector3 lookAt, float desktopHeight = 1.7f)
        {
            if (IsVR)
            {
                Vector3 headOffset = view.transform.position - transform.position;
                transform.position = feet - new Vector3(headOffset.x,0,headOffset.z);
                origin.MatchOriginUpCameraForward(Vector3.up, Vector3.ProjectOnPlane(lookAt - view.transform.position,Vector3.up));
            }
            else
            {
                transform.position = feet; transform.rotation = Quaternion.identity; eyeLag = 0;
                offset.localPosition = Vector3.zero; view.transform.localPosition = Vector3.up * desktopHeight;
                view.transform.LookAt(lookAt); yaw = view.transform.eulerAngles.y; pitch = view.transform.eulerAngles.x;
                if (pitch > 180) pitch -= 360;
            }
        }

        public bool Teleport(Vector3 destination)
        {
            if (app.Inspecting || app.Transitioning) return false;
            if (!IsVR) view.transform.localPosition = Vector3.up * 1.7f;
            bool queued = TeleportProvider.QueueTeleportRequest(new TeleportRequest {
                destinationPosition = destination + Vector3.up * .04f,
                destinationRotation = Quaternion.identity, matchOrientation = MatchOrientation.None,
                requestTime = Time.time });
            if (queued) { TeleportCount++; app.PlayClick(); StartCoroutine(RecenterAfterTeleport()); }
            return queued;
        }

        System.Collections.IEnumerator RecenterAfterTeleport()
        {
            yield return new WaitForSeconds(.1f);
            app.ui.Recenter();
        }

        void OnDestroy()
        {
            foreach (var action in actions) action.Dispose();
            if (veilMaterial) Destroy(veilMaterial);
        }
    }
}
