using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using System.Collections.Generic;

public sealed class QuestTracking : MonoBehaviour
{
    public enum SteeringMode { Automatic, HeadLean, Controllers }
    public SteeringMode mode;
    public Transform head;
    public float Steering { get; private set; }
    public bool Tracked { get; private set; }
    float neutralX, neutralRoll;
    bool calibrated;
    Vector3 rawPose, neutralPose;
    Quaternion rawRotation = Quaternion.identity, neutralYaw = Quaternion.identity;
    static readonly Vector3 RidingEye = new Vector3(0, 2.05024f, -.63f);
    float neutralControllerRoll;
    bool controllersCalibrated;
    InputAction position, rotation, tracked;
    public QuestPointer left, right;
    readonly List<UnityEngine.XR.XRInputSubsystem> inputSubsystems = new List<UnityEngine.XR.XRInputSubsystem>();
    void Start()
    {
        SubsystemManager.GetSubsystems(inputSubsystems);
        foreach (var input in inputSubsystems) input.trackingOriginUpdated += TrackingOriginUpdated;
    }
    void TrackingOriginUpdated(UnityEngine.XR.XRInputSubsystem input)
    {
        calibrated = false; controllersCalibrated = false;
        Debug.Log("BFSQUEST_SYSTEM_RECENTER");
    }
    void Awake()
    {
        position = Action("<XRHMD>/centerEyePosition");
        rotation = Action("<XRHMD>/centerEyeRotation");
        tracked = Action("<XRHMD>/isTracked");
        left = new QuestPointer("LeftHand"); right = new QuestPointer("RightHand");
        mode = (SteeringMode)Mathf.Clamp(PlayerPrefs.GetInt("Quest.SteeringMode", 0), 0, 2);
        Application.onBeforeRender += Pose;
    }
    public static InputAction Action(string path) { var a = new InputAction(binding: path); a.Enable(); return a; }
    void Pose()
    {
        Tracked = tracked.ReadValue<float>() > .5f;
        var raw = position.ReadValue<Vector3>(); var rot = rotation.ReadValue<Quaternion>();
        if (!Tracked)
        {
            var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.CenterEye);
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out raw) &&
                device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rot)) Tracked = true;
        }
        if (!Tracked) return;
        rawPose = raw;
        rawRotation = rot;
        if (!calibrated) { neutralPose = raw; neutralYaw = Quaternion.Euler(0,rot.eulerAngles.y,0); }
        head.localPosition = MapPosition(raw);
        head.localRotation = MapRotation(rot);
    }
    public Vector3 MapPosition(Vector3 raw) => Tracked ? RidingEye + Quaternion.Inverse(neutralYaw) * (raw-neutralPose) : raw;
    public Quaternion MapRotation(Quaternion raw) => Tracked ? Quaternion.Inverse(neutralYaw) * raw : raw;
    public void Recenter()
    {
        if (Tracked) { neutralPose = rawPose; neutralYaw = Quaternion.Euler(0,rawRotation.eulerAngles.y,0); head.localPosition = RidingEye; head.localRotation = MapRotation(rawRotation); }
        neutralX = head.localPosition.x;
        neutralRoll = Mathf.DeltaAngle(0, head.localEulerAngles.z);
        calibrated = Tracked; Steering = 0;
        Debug.Log("BFSQUEST_RECENTER eye=" + head.localPosition + " tracked=" + Tracked);
        neutralControllerRoll = ControllerRoll(); controllersCalibrated = false;
    }
    public void CycleMode() { mode = (SteeringMode)(((int)mode + 1) % 3); PlayerPrefs.SetInt("Quest.SteeringMode", (int)mode); Recenter(); }
    float ControllerRoll()
    {
        if (left.controllerValid && right.controllerValid)
        {
            var delta = right.localPosition - left.localPosition;
            return -Mathf.Atan2(delta.y, Mathf.Max(.15f, Mathf.Abs(delta.x))) * Mathf.Rad2Deg;
        }
        var pointer = right.controllerValid ? right : left;
        return -Mathf.DeltaAngle(0, pointer.localRotation.eulerAngles.z);
    }
    void Update()
    {
        Pose();
        left.Update(transform, MetaAimHand.left, this); right.Update(transform, MetaAimHand.right, this);
        float headValue = 0;
        if (!Tracked)
        {
            calibrated = false;
            headValue = Keyboard.current == null ? 0 : (Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0);
        }
        else
        {
            if (!calibrated) Recenter();
            float lateral = (head.localPosition.x - neutralX) / .22f;
            float roll = -Mathf.DeltaAngle(neutralRoll, head.localEulerAngles.z) / 20f;
            headValue = Mathf.Abs(lateral) >= Mathf.Abs(roll) ? lateral : roll;
        }
        bool controllers = left.controllerValid || right.controllerValid;
        float controllerValue = 0;
        if (controllers)
        {
            if (!controllersCalibrated) { neutralControllerRoll = ControllerRoll(); controllersCalibrated = true; }
            float axis = Mathf.Abs(right.stick.x) >= Mathf.Abs(left.stick.x) ? right.stick.x : left.stick.x;
            controllerValue = Mathf.Abs(axis) > .12f ? axis : (ControllerRoll() - neutralControllerRoll) / 25f;
        }
        else controllersCalibrated = false;
        float v = mode == SteeringMode.HeadLean ? headValue : mode == SteeringMode.Controllers ? controllerValue :
            Mathf.Abs(controllerValue) > Mathf.Abs(headValue) ? controllerValue : headValue;
        float shaped = Mathf.Sign(v) * Mathf.Clamp01((Mathf.Abs(v) - .08f) / .92f);
        Steering = Mathf.Lerp(Steering, shaped, 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime));
    }
    void OnDestroy()
    {
        foreach (var input in inputSubsystems) input.trackingOriginUpdated -= TrackingOriginUpdated;
        Application.onBeforeRender -= Pose;
        position.Dispose(); rotation.Dispose(); tracked.Dispose(); left.Dispose(); right.Dispose();
    }
}
public sealed class QuestPointer
{
    InputAction position, rotation, tracked, trigger, secondary, axis;
    UnityEngine.XR.XRNode node;
    bool held, secondaryHeld;
    public bool valid, controllerValid, pressed, back;
    public Vector3 localPosition;
    public Quaternion localRotation;
    public Vector2 stick;
    public Ray ray;
    public QuestPointer(string hand)
    {
        node = hand == "RightHand" ? UnityEngine.XR.XRNode.RightHand : UnityEngine.XR.XRNode.LeftHand;
        string p = "<XRController>{" + hand + "}/";
        position = QuestTracking.Action(p + "devicePosition"); rotation = QuestTracking.Action(p + "deviceRotation");
        tracked = QuestTracking.Action(p + "isTracked"); trigger = QuestTracking.Action(p + "triggerPressed");
        secondary = QuestTracking.Action(p + "secondaryButton");
        axis = QuestTracking.Action(p + "primary2DAxis");
    }
    public void Update(Transform origin, MetaAimHand hand, QuestTracking tracking)
    {
        Vector3 pos = position.ReadValue<Vector3>(); Quaternion rot = rotation.ReadValue<Quaternion>();
        valid = controllerValid = tracked.ReadValue<float>() > .5f;
        localPosition = pos; localRotation = rot; stick = axis.ReadValue<Vector2>();
        bool down = trigger.ReadValue<float>() > .5f;
        var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool nativeTracked) && nativeTracked)
        {
            valid = controllerValid = true;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out var p)) pos = localPosition = p;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out var q)) rot = localRotation = q;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out var a)) stick = a;
            if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool t)) down |= t;
        }
        if (!valid && hand != null && hand.isTracked.isPressed)
        {
            valid = true; pos = hand.devicePosition.ReadValue(); rot = hand.deviceRotation.ReadValue(); down = hand.indexPressed.isPressed;
        }
        localPosition = tracking.MapPosition(pos); localRotation = tracking.MapRotation(rot);
        ray = new Ray(origin.TransformPoint(localPosition), origin.TransformDirection(localRotation * Vector3.forward));
        pressed = valid && down && !held; held = valid && down;
        bool b = secondary.ReadValue<float>() > .5f;
        back = b && !secondaryHeld; secondaryHeld = b;
    }
    public void Dispose() { position.Dispose(); rotation.Dispose(); tracked.Dispose(); trigger.Dispose(); secondary.Dispose(); axis.Dispose(); }
}
