#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RUtil
{
    /// <summary>
    /// 雑多な便利ツール群
    /// </summary>
    public class Utility
    {
        public static void ApplicationQuit()
        {
#if UNITY_EDITOR
            EditorApplication.ExecuteMenuItem("Edit/Play");
#else
            Application.Quit();
#endif
        }

        public static string ToReadableJson(string json)
        {
            int i = 0;
            int indent = 0;
            int quoteCount = 0;
            int position = -1;
            var sb = new System.Text.StringBuilder();
            int lastindex = 0;

            while (true)
            {
                if (i > 0 && json[i] == '"' && json[i - 1] != '\\') quoteCount++;

                if (quoteCount % 2 == 0)
                {
                    if (json[i] == '{' || json[i] == '[')
                    {
                        indent++;
                        position = 1;
                    }
                    else if (json[i] == '}' || json[i] == ']')
                    {
                        indent--;
                        position = 0;
                    }
                    else if (json.Length > i && json[i] == ',' && json[i + 1] == '"')
                    {
                        position = 1;
                    }

                    if (position >= 0)
                    {
                        sb.AppendLine(json.Substring(lastindex, i + position - lastindex));
                        sb.Append(new string(' ', indent * 4));
                        lastindex = i + position;
                        position = -1;
                    }
                }

                i++;

                if (json.Length <= i)
                {
                    sb.Append(json.Substring(lastindex));
                    break;
                }
            }

            return sb.ToString();
        }
    }
}