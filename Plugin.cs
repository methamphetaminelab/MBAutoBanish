using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AutoBanish;

[BepInPlugin("megabonk.autobanish", "AutoBanish", "1.0.0")]
public class Plugin : BasePlugin
{
    internal const string Items = "Items", Weapons = "Weapons", Tomes = "Tomes";

    internal static ManualLogSource Logger;
    internal static ConfigEntry<KeyCode> MenuKey;
    internal static MonoBehaviour Host;
    internal static readonly Dictionary<string, HashSet<string>> Marked = new();
    static readonly Dictionary<string, ConfigEntry<string>> lists = new();

    public override void Load()
    {
        Logger = Log;
        Bind(Items, "Items banished when they drop from a chest (EItem names, comma separated)");
        Bind(Weapons, "Weapons banished when offered on level-up (EWeapon names, comma separated)");
        Bind(Tomes, "Tomes banished when offered on level-up (ETome names, comma separated)");
        MenuKey = Config.Bind("General", "MenuKey", KeyCode.F6, "Opens and closes the menu");
        Host = AddComponent<Menu>();
        Harmony.CreateAndPatchAll(typeof(BanishPatches));
    }

    void Bind(string category, string description)
    {
        var entry = Config.Bind("General", category, "", description);
        lists[category] = entry;
        Marked[category] = entry.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
    }

    internal static void Toggle(string category, string id)
    {
        var set = Marked[category];
        if (!set.Remove(id)) set.Add(id);
        lists[category].Value = string.Join(",", set);
    }
}

static class BanishPatches
{
    static bool chestPending;

    [HarmonyPrefix, HarmonyPatch(typeof(ChestWindowUi), nameof(ChestWindowUi.Open))]
    static void ChestOpen() => chestPending = true;

    [HarmonyPostfix, HarmonyPatch(typeof(ChestWindowUi), nameof(ChestWindowUi.OpeningFinished))]
    static void ChestOpened(ChestWindowUi __instance)
    {
        if (!chestPending) return;
        chestPending = false;
        var item = __instance.itemData;
        if (item == null || !Plugin.Marked[Plugin.Items].Contains(item.eItem.ToString())) return;
        MonoBehaviourExtensions.StartCoroutine(Plugin.Host, ChestBanish(__instance, item));
    }

    static IEnumerator ChestBanish(ChestWindowUi window, ItemData item)
    {
        var name = item.GetName();
        if (window.levelupScreen != null && window.levelupScreen.GetBanishes() <= 0)
        {
            Plugin.Logger.LogInfo($"{name}: no banishes left");
            yield break;
        }

        var button = window.b_banish;
        var deadline = Time.unscaledTime + 5f;
        while (!Clickable(button))
        {
            if (Time.unscaledTime > deadline || window.itemData != item)
            {
                Plugin.Logger.LogWarning($"{name}: banish button not clickable (active={button.gameObject.activeInHierarchy}, interactable={button.button.interactable}, cantAfford={button.cantAfford})");
                yield break;
            }
            yield return null;
        }

        button.button.onClick.Invoke();
        Plugin.Logger.LogInfo($"Banished {name}");
    }

    [HarmonyPostfix, HarmonyPatch(typeof(UpgradePicker), nameof(UpgradePicker.ShuffleUpgrades))]
    static void LevelupShuffled(UpgradePicker __instance) =>
        MonoBehaviourExtensions.StartCoroutine(Plugin.Host, LevelupBanish(__instance));

    static IEnumerator LevelupBanish(UpgradePicker picker)
    {
        var screen = picker.levelupScreen;
        var deadline = Time.unscaledTime + 5f;
        while (true)
        {
            yield return null;
            var target = Object.FindObjectsOfType(Il2CppType.Of<UpgradeButton>())
                .Select(o => o.Cast<UpgradeButton>())
                .FirstOrDefault(IsMarked);
            if (target == null) yield break;

            var name = target.upgradable.GetName();
            if (screen.GetBanishes() <= 0)
            {
                Plugin.Logger.LogInfo($"{name}: no banishes left");
                yield break;
            }
            if (Time.unscaledTime > deadline)
            {
                var banish = screen.b_banish;
                Plugin.Logger.LogWarning($"{name}: level-up banish failed (banishMode={picker.banishMode}, active={banish.gameObject.activeInHierarchy}, interactable={banish.button.interactable}, cantAfford={banish.cantAfford})");
                yield break;
            }

            if (picker.banishMode)
            {
                target.button.onClick.Invoke();
                if (!target.gameObject.activeSelf) Plugin.Logger.LogInfo($"Banished {name} on level-up");
            }
            else if (Clickable(screen.b_banish))
                screen.b_banish.button.onClick.Invoke();
        }
    }

    static bool IsMarked(UpgradeButton button)
    {
        if (button.isItem || button.upgradable == null) return false;
        var weapon = button.upgradable.TryCast<WeaponData>();
        if (weapon != null) return Plugin.Marked[Plugin.Weapons].Contains(weapon.eWeapon.ToString());
        var tome = button.upgradable.TryCast<TomeData>();
        return tome != null && Plugin.Marked[Plugin.Tomes].Contains(tome.eTome.ToString());
    }

    static bool Clickable(MyButtonOffersUtility button) =>
        button.gameObject.activeInHierarchy && button.button.interactable && !button.cantAfford;
}
