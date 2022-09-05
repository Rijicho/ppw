using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using Cysharp.Threading.Tasks.Triggers;
using System;
using UnityEngine;

[Flags]
public enum FuncKeys
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
}

public static class InputMgr
{


    public static bool GetUndoInput()
    {
        if (Application.isEditor)
            return GetFunction(FuncKeys.Shift) && Input.GetKeyDown(KeyCode.Z);
        else
            return GetFunction(FuncKeys.Ctrl) && Input.GetKeyDown(KeyCode.Z);
    }
    public static bool GetRedoInput()
    {
        if (Application.isEditor)
            return GetFunction(FuncKeys.Shift) && Input.GetKeyDown(KeyCode.Y);
        else
            return GetFunction(FuncKeys.Ctrl) && Input.GetKeyDown(KeyCode.Y);
    }

    public static bool GetSaveInput()
    {
        if (Application.isEditor)
            return GetFunction(FuncKeys.Shift) && Input.GetKeyDown(KeyCode.S);
        else
            return GetFunction(FuncKeys.Ctrl) && Input.GetKeyDown(KeyCode.S);
    }

    public static FuncKeys GetFunction()
    {
        FuncKeys ret = FuncKeys.None;
        if (Input.GetKey(KeyCode.LeftControl)
        || Input.GetKey(KeyCode.RightControl)
        || Input.GetKey(KeyCode.LeftCommand)
        || Input.GetKey(KeyCode.RightCommand))
            ret |= FuncKeys.Ctrl;

        if (Input.GetKey(KeyCode.LeftShift)
                || Input.GetKey(KeyCode.RightShift))
            ret |= FuncKeys.Shift;

        if (Input.GetKey(KeyCode.LeftAlt)
                || Input.GetKey(KeyCode.RightAlt))
            ret |= FuncKeys.Alt;

        return ret;
    }

    public static bool GetFunction(FuncKeys key)
    {
        if((key & FuncKeys.Ctrl) == FuncKeys.Ctrl)
        {
            if (Input.GetKey(KeyCode.LeftControl)
                    || Input.GetKey(KeyCode.RightControl)
                    || Input.GetKey(KeyCode.LeftCommand)
                    || Input.GetKey(KeyCode.RightCommand))
                return true;
        }

        if ((key & FuncKeys.Shift) == FuncKeys.Shift)
        {
            if (Input.GetKey(KeyCode.LeftShift)
                    || Input.GetKey(KeyCode.RightShift))
                return true;
        }
        if ((key & FuncKeys.Alt) == FuncKeys.Alt)
        {
            if (Input.GetKey(KeyCode.LeftAlt)
                    || Input.GetKey(KeyCode.RightAlt))
                return true;
        }

        return key == FuncKeys.None;
    }


    public static Vector3 MousePosScreen;
    public static Vector3 MousePosWorld;

    public static int LClickCount;
    static float lastLClickTime = 0;
    const float doubleClickRange = 0.2f;

    static Camera cam;

    static bool isRunning = false;

    public static async UniTask Run()
    {
        if (isRunning) return;
        isRunning = true;

        while (true)
        {
            await UniTask.Yield(PlayerLoopTiming.EarlyUpdate);
            if (!cam) 
                cam = Camera.main;
            MousePosScreen = new Vector3(Input.mousePosition.x, Input.mousePosition.y);
            MousePosWorld = cam.ScreenToWorldPoint(new Vector3(MousePosScreen.x, MousePosScreen.y, -cam.transform.position.z));

            if (Input.GetMouseButtonDown(0))
            {
                LClickCount++;

                if (LClickCount == 1)
                    lastLClickTime = Time.unscaledTime;
                if (LClickCount > 1 && Time.unscaledTime - lastLClickTime < doubleClickRange)
                {
                    LClickCount = 2;
                    lastLClickTime = 0;
                }
                else if (LClickCount > 2 || Time.unscaledTime - lastLClickTime > doubleClickRange)
                {
                    LClickCount = 0;
                }
            }
            else if (!Input.GetMouseButton(0) && Time.unscaledTime - lastLClickTime > doubleClickRange)
            {
                LClickCount = 0;
            }

        }
    }
}