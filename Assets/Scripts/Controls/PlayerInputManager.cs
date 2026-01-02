using UnityEngine;
using System.Collections.Generic;

public class PlayerInputManager : MonoBehaviour
{
    public static PlayerInputManager Instance { get; private set; }

    private readonly Dictionary<int, PlayerInputProfile> players = new();

    private readonly Dictionary<int, bool> prevUp = new();
    private readonly Dictionary<int, bool> prevDown = new();
    private readonly Dictionary<int, bool> prevLeft = new();
    private readonly Dictionary<int, bool> prevRight = new();

    private readonly Dictionary<int, bool> curUp = new();
    private readonly Dictionary<int, bool> curDown = new();
    private readonly Dictionary<int, bool> curLeft = new();
    private readonly Dictionary<int, bool> curRight = new();

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        players[1] = new PlayerInputProfile(1);

        prevUp[1] = prevDown[1] = prevLeft[1] = prevRight[1] = false;
        curUp[1] = curDown[1] = curLeft[1] = curRight[1] = false;
    }

    void Update()
    {
        foreach (var kv in players)
        {
            int id = kv.Key;
            var p = kv.Value;

            // lê DPad do joystick "ativo" do player
            ReadDpadDigitalFromAxes(p, p.joyIndex, out bool up, out bool down, out bool left, out bool right);

            curUp[id] = up;
            curDown[id] = down;
            curLeft[id] = left;
            curRight[id] = right;
        }
    }

    void LateUpdate()
    {
        foreach (var kv in players)
        {
            int id = kv.Key;

            prevUp[id] = curUp[id];
            prevDown[id] = curDown[id];
            prevLeft[id] = curLeft[id];
            prevRight[id] = curRight[id];
        }
    }

    public PlayerInputProfile GetPlayer(int playerId) => players[playerId];

    public bool Get(PlayerAction action, int playerId = 1)
    {
        var p = players[playerId];
        var b = p.GetBinding(action);

        if (b.kind == BindKind.Key)
            return Input.GetKey(b.key);

        if (b.kind == BindKind.DPad)
        {
            // DPad usa o estado calculado no Update (do joystick ativo do player)
            return b.dpadDir switch
            {
                0 => curUp[playerId],
                1 => curDown[playerId],
                2 => curLeft[playerId],
                3 => curRight[playerId],
                _ => false
            };
        }

        if (b.kind == BindKind.JoyButton)
        {
            var kc = GetJoyButtonKeyCode(b.joyIndex, b.joyButton);
            return kc.HasValue && Input.GetKey(kc.Value);
        }

        return false;
    }

    public bool GetDown(PlayerAction action, int playerId = 1)
    {
        var p = players[playerId];
        var b = p.GetBinding(action);

        if (b.kind == BindKind.Key)
            return Input.GetKeyDown(b.key);

        if (b.kind == BindKind.DPad)
        {
            bool now = b.dpadDir switch
            {
                0 => curUp[playerId],
                1 => curDown[playerId],
                2 => curLeft[playerId],
                3 => curRight[playerId],
                _ => false
            };

            bool was = b.dpadDir switch
            {
                0 => prevUp[playerId],
                1 => prevDown[playerId],
                2 => prevLeft[playerId],
                3 => prevRight[playerId],
                _ => false
            };

            return now && !was;
        }

        if (b.kind == BindKind.JoyButton)
        {
            var kc = GetJoyButtonKeyCode(b.joyIndex, b.joyButton);
            return kc.HasValue && Input.GetKeyDown(kc.Value);
        }

        return false;
    }

    static string DpadAxisName(int joyIndex, int axis) => $"joy{joyIndex}_{axis}";

    static float ReadJoyAxisRaw(int joyIndex, int axis)
    {
        return Input.GetAxisRaw(DpadAxisName(joyIndex, axis));
    }

    public static void ReadDpadDigitalFromAxes(PlayerInputProfile p, int joyIndex, out bool up, out bool down, out bool left, out bool right)
    {
        float x = ReadJoyAxisRaw(joyIndex, 6);
        float y = ReadJoyAxisRaw(joyIndex, 7);

        if (p.invertDpadY)
            y = -y;

        float dz = Mathf.Clamp(p.axisDeadzone, 0.05f, 0.95f);

        left = x <= -dz;
        right = x >= dz;
        up = y >= dz;
        down = y <= -dz;
    }

    static KeyCode? GetJoyButtonKeyCode(int joyIndex, int button)
    {
        if (joyIndex < 1 || joyIndex > 11) return null;
        if (button < 0 || button > 39) return null;

        string name = $"Joystick{joyIndex}Button{button}";
        if (System.Enum.TryParse(name, out KeyCode kc))
            return kc;

        return null;
    }
}
