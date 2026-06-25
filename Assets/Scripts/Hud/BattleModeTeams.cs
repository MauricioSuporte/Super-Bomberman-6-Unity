using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BattleModeRules))]
public sealed class BattleModeTeams : MonoBehaviour
{
    public static BattleModeTeams Instance { get; private set; }

    BattleModeRules rules;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple BattleModeTeams instances found in scene. Keeping the most recent one.", this);
        }

        Instance = this;
        CacheRules();
        RefreshEnabledState();
    }

    void OnEnable()
    {
        Instance = this;
        CacheRules();
        RefreshEnabledState();
    }

    void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnValidate()
    {
        CacheRules();
        RefreshEnabledState();
    }

    void Reset()
    {
        CacheRules();
        RefreshEnabledState();
    }

    public BattleModeRules.TeamId GetTeamForPlayer(int playerId)
    {
        if (!GameSession.IsValidPlayerId(playerId))
            return BattleModeRules.TeamId.Blue;

        CacheRules();

        return rules != null
            ? rules.GetTeamForPlayer(playerId)
            : BattleModeRules.GetDefaultTeamForPlayer(playerId);
    }

    public void RefreshFromRules()
    {
        CacheRules();
        RefreshEnabledState();
    }

    void CacheRules()
    {
        rules = GetComponent<BattleModeRules>();
    }

    void RefreshEnabledState()
    {
        bool shouldBeEnabled = rules != null && rules.UsesTeams;

        if (enabled != shouldBeEnabled)
            enabled = shouldBeEnabled;
    }
}
