using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEngine;

public static class ActionMgr
{
    const bool isLogRequired = false;
    static List<DrawAction> actions = new List<DrawAction>();
    static int index = 0;
    // i=0
    //|<<<<<<<<

    //     i=4
    //>>>>|<<<<

    //         i=8
    //>>>>>>>>|

    public static bool IsRunning = false;

    public static bool IsOnBottom => index == 0;
    public static bool IsOnTop => index == actions.Count;
    public static DrawAction NextAction => IsOnTop ? default : actions[index];
    public static DrawAction PrevAction => IsOnBottom ? default : actions[index - 1];

    public static async UniTask Do(DrawAction action, DrawingCanvas e)
    {
        if (IsRunning)
            return;
        IsRunning = true;
        var changed = await action.Do(e);
        if (changed)
        {
            if (index < actions.Count)
                actions.RemoveRange(index, actions.Count - index);
            actions.Add(action);
            index++;
            Log();
        }
        await UniTask.Yield();
        IsRunning = false;

        action.OnFinish(e);
    }

    public static void Undo(DrawingCanvas e)
    {
        if(index > 0)
        {
            var action = actions[--index];
            action.Undo(e);
            Log();
        }
    }

    public static void Redo(DrawingCanvas e)
    {
        if (index < actions.Count)
        {
            var action = actions[index++];
            action.Redo(e);
            Log();
        }
    }

    static StringBuilder sb = new StringBuilder();
    public static void Log()
    {
        if (isLogRequired)
        {
            sb.Clear();
            for (int i = 0; i < actions.Count + 1; i++)
            {
                if (index == i)
                {
                    sb.Append(" >___< ");
                }
                if (i < index)
                {
                    sb.Append($"|{actions[i].Name}>");
                }
                else if (i >= index && i < actions.Count)
                {
                    sb.Append($"<{actions[i].Name}|");
                }
            }
            Debug.Log(sb.ToString());
        }
    }

    public static void Clear()
    {
        actions.Clear();
        index = 0;
    }
}
