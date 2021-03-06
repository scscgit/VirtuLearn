﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityStandardAssets.CrossPlatformInput;
using UnityStandardAssets.CrossPlatformInput.PlatformSpecific;

/// <summary>
/// Multi-platform and multi-purpose dynamically switchable input.
/// Improved functionality of the CrossPlatformInputManager, which was limited to compile-time choices.
/// </summary>
public class GameInput : VirtualInput
{
    public enum ActiveInputMethodType
    {
        NonVrKeyboard,
        NonVrPhone,
        Vr,
    }

    private readonly GameInputManager _gameInputManager;

    public GameInput(GameInputManager manager)
    {
        _gameInputManager = manager;
    }

    private readonly VirtualInput _hardwareInput = new StandaloneInput();

    private ActiveInputMethodType _activeInputMethod;

    protected ActiveInputMethodType ActiveInputMethod
    {
        get { return _activeInputMethod; }
        set
        {
            if (_activeInputMethod == value)
            {
                return;
            }
            // Changes the mapping
            _activeInputMethod = value;

            // Re-activates mobile control rigs by their need
            foreach (var rig in _gameInputManager.MobileControlRigs)
            {
                rig.CheckEnableControlRig();
            }

            // Additionally, when the change was external by modifying the Manager value,
            // the graphical representation within the menu will be updated too
            switch (ActiveInputMethod)
            {
                case ActiveInputMethodType.NonVrKeyboard:
                    _gameInputManager.SetActiveInputMethodNonVrKeyboard();
                    break;
                case ActiveInputMethodType.NonVrPhone:
                    _gameInputManager.SetActiveInputMethodNonVrPhone();
                    break;
                case ActiveInputMethodType.Vr:
                    _gameInputManager.SetActiveInputMethodVr();
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }

    // Convenience instance getter, uses the static instance of GameInputManager
    public static GameInput Instance
    {
        get { return GameInputManager.Instance.GameInput; }
    }

    // This workaround couples these 2 classes so that the Manager can set the value using Editor
    public void UpdateActiveInputMethod()
    {
        // Reflects the value of the Manager, executing the setter which executes required operations
        ActiveInputMethod = _gameInputManager.ActiveInputMethod;
    }

    /// <summary>
    /// Removes all bindings of virtual axes and buttons registered in this class, saving their instances
    /// before executing the action, after which all of them are returned back.
    /// This is probably no longer needed as it is not used anywhere, may be removed later.
    /// </summary>
    /// <param name="bindingUnsafeAction">Action that should be executed while there are none bindings</param>
    [Obsolete]
    public void SaveAndRestoreBindingsAround(Action bindingUnsafeAction)
    {
        // Backs up previous virtual bindings
        Dictionary<string, CrossPlatformInputManager.VirtualAxis> copiedVirtualAxes;
        Dictionary<string, CrossPlatformInputManager.VirtualButton> copiedVirtualButtons;
        UnRegisterAllBindings(out copiedVirtualAxes, out copiedVirtualButtons);

        bindingUnsafeAction();

        // Returns old virtual bindings to the new input method
        RegisterAllBindings(copiedVirtualAxes, copiedVirtualButtons);
    }

    [Obsolete]
    public void UnRegisterAllBindings(
        out Dictionary<string, CrossPlatformInputManager.VirtualAxis> copiedVirtualAxes,
        out Dictionary<string, CrossPlatformInputManager.VirtualButton> copiedVirtualButtons
    )
    {
        copiedVirtualAxes = new Dictionary<string, CrossPlatformInputManager.VirtualAxis>(m_VirtualAxes);
        copiedVirtualButtons = new Dictionary<string, CrossPlatformInputManager.VirtualButton>(m_VirtualButtons);
        foreach (var virtualAxis in m_VirtualAxes.Keys.ToList())
        {
            UnRegisterVirtualAxis(virtualAxis);
        }
        foreach (var virtualButton in m_VirtualButtons.Keys.ToList())
        {
            UnRegisterVirtualButton(virtualButton);
        }
    }

    [Obsolete]
    public void RegisterAllBindings(
        Dictionary<string, CrossPlatformInputManager.VirtualAxis> virtualAxes,
        Dictionary<string, CrossPlatformInputManager.VirtualButton> virtualButtons
    )
    {
        foreach (var virtualAxis in virtualAxes)
        {
            RegisterVirtualAxis(virtualAxis.Value);
        }
        foreach (var virtualButton in virtualButtons)
        {
            RegisterVirtualButton(virtualButton.Value);
        }
    }

    private Quaternion HeadRotation
    {
        get { return _gameInputManager.Cardboard.HeadPose.Orientation; }
    }

    private Vector3 HeadPosition
    {
        get { return _gameInputManager.Cardboard.HeadPose.Position; }
    }

    /// <summary>
    /// Returns the rotation angle of given device axis. Use Vector3.right to obtain pitch, Vector3.up for yaw and Vector3.forward for roll.
    /// This is for landscape mode. Up vector is the wide side of the phone and forward vector is where the back camera points to.
    /// The angle is in degrees with the value between 0 and 360 and represents the player's local view, while jaw being like a compass.
    /// NOTE: Was tested in GetAxisForVr() but failed to provide assumed results, will be probably removed later as it is obsolete.
    /// Source: http://answers.unity3d.com/questions/434096/lock-rotation-of-gyroscope-controlled-camera-to-fo.html
    /// </summary>
    /// <returns>A scalar value, representing the rotation amount around specified axis.</returns>
    /// <param name="deviceRotation">Rotation of the input device.</param>
    /// <param name="axis">Should be either Vector3.right, Vector3.up or Vector3.forward. Won't work for anything else.</param>
    /// <param name="normalize">Change the value to an interval between -1 and 1.</param>
    [Obsolete]
    private static float GetAngleByDeviceAxis(Quaternion deviceRotation, Vector3 axis, bool normalize = false)
    {
        Quaternion eliminationOfOthers = Quaternion.Inverse(
            Quaternion.FromToRotation(axis, deviceRotation * axis)
        );
        Vector3 filteredEuler = (eliminationOfOthers * deviceRotation).eulerAngles;

        float result = 0f;
        if (axis == Vector3.forward)
        {
            result = filteredEuler.z;
        }
        else if (axis == Vector3.up)
        {
            result = filteredEuler.y;
        }
        else if (axis == Vector3.right)
        {
            // incorporate different euler representations.
            result = (filteredEuler.y > 90 && filteredEuler.y < 270) ? 180 - filteredEuler.x : filteredEuler.x;
        }
        return !normalize ? result : NormalizeAngle(result);
    }

    public static float NormalizeAngle(float angleDegrees)
    {
        if (angleDegrees > 180)
        {
            angleDegrees -= 360;
        }
        return angleDegrees / 180;
    }

    private float GetAxisForVr(string name)
    {
        float speed;
        switch (name)
        {
            case "Horizontal":
                speed = -NormalizeAngle(HeadRotation.eulerAngles.z);
                break;
            case "Vertical":
                speed = NormalizeAngle(HeadRotation.eulerAngles.x);
                break;
            case "Mouse X":
            case "Mouse Y":
                return 0;
            default:
                throw new ArgumentOutOfRangeException("name", "Unknown axis for VR purposes");
        }

        // Only a noticeable tilt change greater than the ignored offset will be applied
        //TODO: solve a difference between VR head and mouse head rotations or center it after switching the input mode
        var offset = _gameInputManager.TiltMovementVrThreshold;
        speed = speed < 0
            ? (speed < -offset
                ? speed + offset
                : 0)
            : (speed > offset
                ? speed - offset
                : 0);
        // Making the value go closer to 1 faster
        speed *= 20;
        return speed > 1 ? 1 : speed;
    }

    public override float GetAxis(string name, bool raw)
    {
        switch (ActiveInputMethod)
        {
            case ActiveInputMethodType.NonVrKeyboard:
                return _hardwareInput.GetAxis(name, raw);
            case ActiveInputMethodType.NonVrPhone:
                return GetOrAddAxis(name).GetValue;
            //TODO: implement second joystick
            case ActiveInputMethodType.Vr:
                return GetAxisForVr(name); //TODO: change
            default:
                throw new NotImplementedException();
        }
    }

    public override bool GetButton(string name)
    {
        switch (ActiveInputMethod)
        {
            case ActiveInputMethodType.NonVrKeyboard:
                return _hardwareInput.GetButton(name);
            case ActiveInputMethodType.NonVrPhone:
                return GetOrAddButton(name).GetButton || _hardwareInput.GetButton(name);
            case ActiveInputMethodType.Vr:
                return _hardwareInput.GetButton(name);
            default:
                throw new NotImplementedException();
        }
    }

    public override bool GetButtonDown(string name)
    {
        switch (ActiveInputMethod)
        {
            case ActiveInputMethodType.NonVrKeyboard:
                return _hardwareInput.GetButtonDown(name);
            case ActiveInputMethodType.NonVrPhone:
                return GetOrAddButton(name).GetButtonDown || _hardwareInput.GetButtonDown(name);
            case ActiveInputMethodType.Vr:
                return _hardwareInput.GetButtonDown(name);
            default:
                throw new NotImplementedException();
        }
    }

    public override bool GetButtonUp(string name)
    {
        switch (ActiveInputMethod)
        {
            case ActiveInputMethodType.NonVrKeyboard:
                return _hardwareInput.GetButtonUp(name);
            case ActiveInputMethodType.NonVrPhone:
                return GetOrAddButton(name).GetButtonUp || _hardwareInput.GetButtonUp(name);
            case ActiveInputMethodType.Vr:
                return _hardwareInput.GetButtonUp(name);
            default:
                throw new NotImplementedException();
        }
    }

    private CrossPlatformInputManager.VirtualButton GetOrAddButton(string name)
    {
        if (!m_VirtualButtons.ContainsKey(name))
        {
            CrossPlatformInputManager.RegisterVirtualButton(new CrossPlatformInputManager.VirtualButton(name));
        }
        return m_VirtualButtons[name];
    }

    private CrossPlatformInputManager.VirtualAxis GetOrAddAxis(string name)
    {
        if (!m_VirtualAxes.ContainsKey(name))
        {
            CrossPlatformInputManager.RegisterVirtualAxis(new CrossPlatformInputManager.VirtualAxis(name));
        }
        return m_VirtualAxes[name];
    }

    public override void SetButtonDown(string name)
    {
        GetOrAddButton(name).Pressed();
    }

    public override void SetButtonUp(string name)
    {
        GetOrAddButton(name).Released();
    }

    public override void SetAxisPositive(string name)
    {
        SetAxis(name, 1f);
    }

    public override void SetAxisNegative(string name)
    {
        SetAxis(name, -1f);
    }

    public override void SetAxisZero(string name)
    {
        SetAxis(name, 0f);
    }

    public override void SetAxis(string name, float value)
    {
        GetOrAddAxis(name).Update(value);
    }

    public override Vector3 MousePosition()
    {
        switch (ActiveInputMethod)
        {
            case ActiveInputMethodType.NonVrKeyboard:
                //return _hardwareInput.MousePosition();
                return Vector3.zero;
            case ActiveInputMethodType.NonVrPhone:
                //return virtualMousePosition;
                return Vector3.zero;
            case ActiveInputMethodType.Vr:
                //return HeadPosition;
                return Vector3.zero;
            default:
                throw new NotImplementedException();
        }
    }
}
