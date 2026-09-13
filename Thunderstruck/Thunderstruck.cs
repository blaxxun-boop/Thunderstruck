using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace Thunderstruck;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("org.bepinex.plugins.valheim_plus")]
public class Thunderstruck : BaseUnityPlugin
{
	private const string ModName = "Thunderstruck";
	private const string ModVersion = "1.0.1";
	private const string ModGUID = "org.bepinex.plugins.thunderstruck";

	private static readonly ConfigSync configSync = new(ModName) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

	private static ConfigEntry<Toggle> serverConfigLocked = null!;
	private static ConfigEntry<int> damageTakenIncrease = null!;

	private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
	{
		ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

		SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
		syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

		return configEntry;
	}

	private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

	public static T ConvertStatusEffect<T>(StatusEffect statusEffect) where T : StatusEffect
	{
		T ownSE = ScriptableObject.CreateInstance<T>();

		ownSE.name = statusEffect.name;
		foreach (FieldInfo field in statusEffect.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
		{
			field.SetValue(ownSE, field.GetValue(statusEffect));
		}

		return ownSE;
	}

	private enum Toggle
	{
		On = 1,
		Off = 0,
	}

	public void Awake()
	{
		serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
		configSync.AddLockingConfigEntry(serverConfigLocked);
		damageTakenIncrease = config("1 - General", "Damage taken increase", 25, new ConfigDescription("Percentage of damage taken increase while afflicted with lightning status effect.", new AcceptableValueRange<int>(0, 100)));

		Assembly assembly = Assembly.GetExecutingAssembly();
		Harmony harmony = new(ModGUID);
		harmony.PatchAll(assembly);
	}

	[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
	private class IncreaseDamageTaken
	{
		[UsedImplicitly]
		[HarmonyPriority(Priority.VeryHigh)]
		private static void Postfix(ObjectDB __instance)
		{
			for (int i = 0; i < __instance.m_StatusEffects.Count; ++i)
			{
				if (__instance.m_StatusEffects[i].NameHash() == SEMan.s_statusEffectLightning && __instance.m_StatusEffects[i] is not ThunderEffect)
				{
					__instance.m_StatusEffects[i] = ConvertStatusEffect<ThunderEffect>(__instance.m_StatusEffects[i]);
				}
			}
		}
	}

	private class ThunderEffect : StatusEffect
	{
		public override void OnDamaged(HitData hit, Character attacker)
		{
			hit.m_damage.Modify(1 + damageTakenIncrease.Value / 100f);
			base.OnDamaged(hit, attacker);
		}
	}
}