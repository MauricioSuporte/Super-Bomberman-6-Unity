using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using UnityEngine;

public sealed class DiscordIpcClient : IDisposable
{
    const int HandshakeOpcode = 0;
    const int FrameOpcode = 1;
    const int CloseOpcode = 2;

    readonly string clientId;
    NamedPipeClientStream pipe;

    public bool IsConnected => pipe != null && pipe.IsConnected;

    public DiscordIpcClient(string clientId)
    {
        this.clientId = clientId;
    }

    public bool TryConnect()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (IsConnected)
            return true;

        DisposePipe();

        for (int i = 0; i < 10; i++)
        {
            string pipeName = "discord-ipc-" + i;

            // Se o named pipe não existe (Discord fechado), pula sem bloquear.
            // Sem este guard, pipe.Connect() abaixo trava a thread por todo o timeout.
            if (!PipeExists(pipeName))
                continue;

            try
            {
                pipe = new NamedPipeClientStream(
                    ".",
                    pipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);

                pipe.Connect(100);
                WriteFrame(HandshakeOpcode, "{\"v\":1,\"client_id\":\"" + Escape(clientId) + "\"}");
                return true;
            }
            catch (Exception ex)
            {
                if (pipe != null)
                    pipe.Dispose();

                pipe = null;

                if (i == 9)
                    Debug.LogWarning("Discord Rich Presence could not connect. Is Discord open? " + ex.Message);
            }
        }
#endif

        return false;
    }

    static bool PipeExists(string pipeName)
    {
        try
        {
            // No Windows os named pipes ficam sob \\.\pipe\. File.Exists é uma checagem
            // rápida e NÃO bloqueante — ao contrário de NamedPipeClientStream.Connect().
            return File.Exists(@"\\.\pipe\" + pipeName);
        }
        catch
        {
            return false;
        }
    }

    public void SetActivity(DiscordRichPresenceActivity activity)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!IsConnected && !TryConnect())
            return;

        try
        {
            string payload = BuildSetActivityPayload(activity);
            WriteFrame(FrameOpcode, payload);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("Discord Rich Presence update failed. " + ex.Message);
            DisposePipe();
        }
#endif
    }

    public void ClearActivity()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (!IsConnected)
            return;

        try
        {
            WriteFrame(FrameOpcode, BuildSetActivityPayload(null));
        }
        catch
        {
            DisposePipe();
        }
#endif
    }

    public void Dispose()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (IsConnected)
        {
            try
            {
                WriteFrame(CloseOpcode, "{}");
            }
            catch
            {
                // Discord may already be closed.
            }
        }

        DisposePipe();
#endif
    }

    void DisposePipe()
    {
        if (pipe == null)
            return;

        pipe.Dispose();
        pipe = null;
    }

    void WriteFrame(int opcode, string json)
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        byte[] opcodeBytes = BitConverter.GetBytes(opcode);
        byte[] lengthBytes = BitConverter.GetBytes(jsonBytes.Length);

        pipe.Write(opcodeBytes, 0, opcodeBytes.Length);
        pipe.Write(lengthBytes, 0, lengthBytes.Length);
        pipe.Write(jsonBytes, 0, jsonBytes.Length);
        pipe.Flush();
    }

    string BuildSetActivityPayload(DiscordRichPresenceActivity activity)
    {
        string nonce = Guid.NewGuid().ToString("N");
        int pid = 0;

        try
        {
            pid = System.Diagnostics.Process.GetCurrentProcess().Id;
        }
        catch
        {
            pid = 0;
        }

        var json = new StringBuilder(512);
        json.Append("{\"cmd\":\"SET_ACTIVITY\",\"args\":{\"pid\":");
        json.Append(pid);
        json.Append(",\"activity\":");

        if (activity == null)
            json.Append("null");
        else
            AppendActivity(json, activity);

        json.Append("},\"nonce\":\"");
        json.Append(Escape(nonce));
        json.Append("\"}");
        return json.ToString();
    }

    static void AppendActivity(StringBuilder json, DiscordRichPresenceActivity activity)
    {
        json.Append("{");
        bool firstProperty = true;
        AppendStringProperty(json, "details", activity.Details, ref firstProperty);
        AppendStringProperty(json, "state", activity.State, ref firstProperty);

        if (activity.StartTimestamp > 0)
        {
            AppendCommaIfNeeded(json, ref firstProperty);
            json.Append("\"timestamps\":{\"start\":");
            json.Append(activity.StartTimestamp);
            json.Append("}");
        }

        bool hasAssets =
            !string.IsNullOrWhiteSpace(activity.LargeImageKey) ||
            !string.IsNullOrWhiteSpace(activity.LargeImageText) ||
            !string.IsNullOrWhiteSpace(activity.SmallImageKey) ||
            !string.IsNullOrWhiteSpace(activity.SmallImageText);

        if (hasAssets)
        {
            AppendCommaIfNeeded(json, ref firstProperty);
            json.Append("\"assets\":{");
            bool firstAsset = true;
            AppendStringProperty(json, "large_image", activity.LargeImageKey, ref firstAsset);
            AppendStringProperty(json, "large_text", activity.LargeImageText, ref firstAsset);
            AppendStringProperty(json, "small_image", activity.SmallImageKey, ref firstAsset);
            AppendStringProperty(json, "small_text", activity.SmallImageText, ref firstAsset);
            json.Append("}");
        }

        if (activity.PartySize > 0 && activity.PartyMax > 0)
        {
            AppendCommaIfNeeded(json, ref firstProperty);
            json.Append("\"party\":{\"id\":\"");
            json.Append(Escape(activity.PartyId));
            json.Append("\",\"size\":[");
            json.Append(Mathf.Clamp(activity.PartySize, 1, activity.PartyMax));
            json.Append(",");
            json.Append(activity.PartyMax);
            json.Append("]}");
        }

        json.Append("}");
    }

    static void AppendCommaIfNeeded(StringBuilder json, ref bool first)
    {
        if (!first)
            json.Append(",");

        first = false;
    }

    static void AppendStringProperty(StringBuilder json, string name, string value, ref bool first)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        AppendCommaIfNeeded(json, ref first);
        json.Append("\"");
        json.Append(name);
        json.Append("\":\"");
        json.Append(Escape(value));
        json.Append("\"");
    }

    static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var result = new StringBuilder(value.Length + 8);

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];

            switch (c)
            {
                case '\\':
                    result.Append("\\\\");
                    break;
                case '"':
                    result.Append("\\\"");
                    break;
                case '\b':
                    result.Append("\\b");
                    break;
                case '\f':
                    result.Append("\\f");
                    break;
                case '\n':
                    result.Append("\\n");
                    break;
                case '\r':
                    result.Append("\\r");
                    break;
                case '\t':
                    result.Append("\\t");
                    break;
                default:
                    if (c < 32)
                    {
                        result.Append("\\u");
                        result.Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        result.Append(c);
                    }
                    break;
            }
        }

        return result.ToString();
    }
}

public sealed class DiscordRichPresenceActivity
{
    public string Details;
    public string State;
    public long StartTimestamp;
    public string LargeImageKey;
    public string LargeImageText;
    public string SmallImageKey;
    public string SmallImageText;
    public string PartyId;
    public int PartySize;
    public int PartyMax;
}
