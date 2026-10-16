using System;
using UnityEditor;
using UnityEngine;

/// <summary>Configures only the selected local dobo_ui demo HUD sprites for Unity UI.</summary>
public sealed class FantasyRpgHudSpriteImporter : AssetPostprocessor
{
    static readonly string[] SpriteRoots =
    {
        "Assets/UI/FantasyRPG/Resources/HlwFantasyUI",
        "Assets/UI/Abilities/Fireball"
    };
    const string SessionKey = "HLW_FantasyRpgHudSpritesConfigured_v4";
    static bool configurationQueued;

    static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
        string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (configurationQueued) return;
        foreach (string path in importedAssets)
        {
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (string root in SpriteRoots)
            {
                if (!path.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)) continue;
                configurationQueued = true;
                EditorApplication.delayCall += () =>
                {
                    configurationQueued = false;
                    ConfigureSprites();
                };
                return;
            }
        }
    }

    [InitializeOnLoadMethod]
    static void ConfigureCopiedAssetsOnEditorLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            if (!HasSpritesToConfigure()) return;
            ConfigureSprites();
            SessionState.SetBool(SessionKey, true);
        };
    }

    [MenuItem("Tools/Hero Line Wars/Configure Fantasy RPG HUD Sprites")]
    public static void ConfigureSprites()
    {
        int configured = 0;
        foreach (string root in SpriteRoots)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                Vector4 border = BorderFor(path);
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (importer.textureType == TextureImporterType.Sprite &&
                    importer.spriteImportMode == SpriteImportMode.Single &&
                    importer.alphaIsTransparency && !importer.mipmapEnabled &&
                    importer.filterMode == FilterMode.Bilinear &&
                    importer.wrapMode == TextureWrapMode.Clamp &&
                    importer.textureCompression == TextureImporterCompression.CompressedHQ &&
                    importer.maxTextureSize == 1024 &&
                    importer.npotScale == TextureImporterNPOTScale.None &&
                    settings.spriteAlignment == (int)SpriteAlignment.Center &&
                    settings.spritePivot == new Vector2(.5f, .5f) &&
                    importer.spriteBorder == border)
                    continue;

                // Preserve TextureImporterSettings data such as alignment/pivot, then set
                // the importer-level type and mode afterward. Unity 6 can otherwise restore
                // textureType=Default and spriteMode=None from settings read from a PNG texture.
                settings.textureType = TextureImporterType.Sprite;
                settings.spriteMode = (int)SpriteImportMode.Single;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                settings.spritePivot = new Vector2(.5f, .5f);
                importer.SetTextureSettings(settings);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.sRGBTexture = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = 1024;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.isReadable = false;
                importer.spriteBorder = border;
                importer.SaveAndReimport();
                configured++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Configured {configured} Fantasy RPG HUD / ability PNGs as compressed, alpha-aware UI sprites.");
    }

    static bool HasSpritesToConfigure()
    {
        foreach (string root in SpriteRoots)
            if (AssetDatabase.FindAssets("t:Texture2D", new[] { root }).Length > 0)
                return true;
        return false;
    }

    static Vector4 BorderFor(string path)
    {
        if (path.EndsWith("Container3_blue.png")) return new Vector4(36f, 28f, 36f, 28f);
        if (path.EndsWith("Container1_default.png")) return new Vector4(52f, 52f, 52f, 52f);
        if (path.EndsWith("ProgressBarBackground4.png")) return new Vector4(22f, 16f, 22f, 16f);
        return Vector4.zero;
    }
}
