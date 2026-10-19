using UnityEngine;
using UnityEngine.SceneManagement;

public enum HlwHeroSelection { Battlemage, Geomancer }
public enum HlwGameMode { SinglePlayer, MultiPlayer, Campaign }

/// <summary>Small persistent bridge between the front end and a gameplay match.</summary>
public sealed class HlwGameSession : MonoBehaviour
{
    public const string MainMenuScene = "HLW_MainMenu";
    public const string GameplayScene = "HLW_LanePrototype";

    public static HlwGameSession Current { get; private set; }
    public HlwHeroSelection SelectedHero { get; private set; } = HlwHeroSelection.Battlemage;
    public HeroDefinition SelectedHeroDefinition => HeroCatalog.Find(SelectedHero);
    public HlwGameMode SelectedMode { get; private set; } = HlwGameMode.SinglePlayer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureSession()
    {
        if (Current) return;
        HlwGameSession existing = FindAnyObjectByType<HlwGameSession>();
        if (existing)
        {
            existing.Initialize();
            return;
        }

        GameObject sessionObject = new GameObject("HLW Game Session");
        sessionObject.AddComponent<HlwGameSession>().Initialize();
    }

    void Awake()
    {
        if (Current && Current != this)
        {
            Destroy(gameObject);
            return;
        }
        Initialize();
    }

    void Initialize()
    {
        if (Current && Current != this) return;
        Current = this;
        DontDestroyOnLoad(gameObject);
    }

    public void SelectHero(HlwHeroSelection hero) => SelectedHero = hero;

    public bool SelectMode(HlwGameMode mode)
    {
        if (mode != HlwGameMode.SinglePlayer) return false;
        SelectedMode = mode;
        return true;
    }

    public void StartSelectedMatch()
    {
        if (SelectedMode != HlwGameMode.SinglePlayer) return;
        HlwGameSpeed.ResetToNormal();
        SceneManager.LoadScene(GameplayScene, LoadSceneMode.Single);
    }

    void PrepareForMainMenu()
    {
        // Keep the player's hero selection when returning, but reset transient mode/time state.
        SelectedMode = HlwGameMode.SinglePlayer;
        HlwGameSpeed.ResetToNormal();
    }

    public static void ReturnToMainMenu()
    {
        if (Current) Current.PrepareForMainMenu();
        else HlwGameSpeed.ResetToNormal();
        SceneManager.LoadScene(MainMenuScene, LoadSceneMode.Single);
    }
}
