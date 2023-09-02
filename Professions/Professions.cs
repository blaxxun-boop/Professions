using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace Professions;

[BepInPlugin(ModGUID, ModName, ModVersion)]
[BepInIncompatibility("org.bepinex.plugins.valheim_plus")]
public class Professions : BaseUnityPlugin
{
	private const string ModName = "Professions";
	private const string ModVersion = "1.4.0";
	private const string ModGUID = "org.bepinex.plugins.professions";

	private static readonly ConfigSync configSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

	private static ConfigEntry<Toggle> serverConfigLocked = null!;
	private static ConfigEntry<KeyboardShortcut> professionPanelHotkey = null!;
	private static ConfigEntry<int> maximumAllowedProfessions = null!;
	public static ConfigEntry<Toggle> allowUnselect = null!;
	private static ConfigEntry<float> professionChangeCooldown = null!;
	public static readonly Dictionary<Profession, ConfigEntry<ProfessionToggle>> blockOtherProfessions = new();

	private static DateTime serverTime = DateTime.Now;

	private int configOrder = 0;

	private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
	{
		description = new ConfigDescription(description.Description, description.AcceptableValues, description.Tags.AddItem(new ConfigurationManagerAttributes { Order = configOrder-- }).ToArray());

		ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

		SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
		syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

		return configEntry;
	}

	private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);

	public enum Toggle
	{
		On = 1,
		Off = 0,
	}

	public enum ProfessionToggle
	{
		Ignored = 0,
		[Description("Block Experience")]
		BlockExperience = 1,
		[Description("Block Usage")]
		BlockUsage = 2,
	}

	public enum Profession
	{
		Blacksmithing,
		Building,
		Cooking,
		Farming,
		Lumberjacking,
		Mining,
		Ranching,
		Sailing,
		Alchemy,
		Jewelcrafting,
		Foraging,
		Exploration,
	}

	private static readonly Dictionary<Profession, string> professionDescriptions = new()
	{
		{
			Profession.Blacksmithing,
			"A blacksmith uses the smelter and forge to smelt ore and craft armor and weapons."
		},
		{
			Profession.Building,
			"A builder uses the hammer to construct floors, walls and roofs for shelter."
		},
		{
			Profession.Cooking,
			"A cook creates lavish meals."
		},
		{
			Profession.Farming,
			"A farmer uses the cultivator to cultivate land to plant crops and harvest them."
		},
		{
			Profession.Lumberjacking,
			"A lumberjack uses an axe to cut trees to collect all kind of woods."
		},
		{
			Profession.Mining,
			"A miner uses a pickaxe to mine stone and ore."
		},
		{
			Profession.Ranching,
			"A rancher can tame certain animals and breed them for their meat."
		},
		{
			Profession.Sailing,
			"A sailor uses ships to explore the vast ocean and discover new islands."
		},
		{
			Profession.Alchemy,
			"An alchemist creates powerful potions, flasks and elixirs."
		},
		{
			Profession.Jewelcrafting,
			"A jeweler cuts powerful magic gems and adds sockets to equipment."
		},
		{
			Profession.Foraging,
			"A forager collects berries and mushrooms."
		},
		{
			Profession.Exploration,
			"An explorer explores the world and searches treasure chests."
		},
	};

	private static Skills.SkillType fromProfession(Profession profession) => (Skills.SkillType)Math.Abs(profession.ToString().GetStableHashCode());
	private static Profession? fromSkill(Skills.SkillType skill) => ((Profession[])Enum.GetValues(typeof(Profession))).Select(p => (Profession?)p).FirstOrDefault(p => skill == fromProfession(p!.Value));
	private static GameObject professionPanel = null!;
	private static GameObject? professionPanelInstance;
	private static readonly Dictionary<Profession, GameObject> professionPanelElements = new();
	private readonly Assembly? bepinexConfigManager = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "ConfigurationManager");

	private class ConfigurationManagerAttributes
	{
		[UsedImplicitly] public bool? Browsable;
		[UsedImplicitly] public int? Order;
	}

	public void Awake()
	{
		try
		{
			Type? configManagerType = bepinexConfigManager?.GetType("ConfigurationManager.ConfigurationManager");
			object? configManager = configManagerType == null ? null : BepInEx.Bootstrap.Chainloader.ManagerObject.GetComponent(configManagerType);

			void reloadConfigDisplay() => configManagerType?.GetMethod("BuildSettingList")!.Invoke(configManager, Array.Empty<object>());

			serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
			configSync.AddLockingConfigEntry(serverConfigLocked);
			maximumAllowedProfessions = config("1 - General", "Maximum Number of Professions", 1, new ConfigDescription("Sets the maximum number of professions a player is allowed to have at the same time.", new AcceptableValueRange<int>(1, 5)));
			allowUnselect = config("1 - General", "Allow Profession Change", Toggle.Off, "If on, players can unselect professions and select new ones.");
			ConfigurationManagerAttributes changeCooldownAttributes = new() { Browsable = allowUnselect.Value == Toggle.On };
			allowUnselect.SettingChanged += (_, _) =>
			{
				changeCooldownAttributes.Browsable = allowUnselect.Value == Toggle.On;
				reloadConfigDisplay();
			};
			professionChangeCooldown = config("1 - General", "Profession Change Cooldown", 0f, new ConfigDescription("Time between profession changes. Uses real time hours. Use 0 to disable this.", null, changeCooldownAttributes));
			professionPanelHotkey = config("1 - General", "Profession Panel Hotkey", new KeyboardShortcut(KeyCode.P), "Key or key combination to open the profession panel.", false);

			foreach (Profession profession in (Profession[])Enum.GetValues(typeof(Profession)))
			{
				blockOtherProfessions[profession] = config("2 - Professions", $"{profession} behaviour", ProfessionToggle.BlockExperience, "Ignored: The skill is not considered a profession and can be used by everyone.\nBlock Experience: If you did not pick the skills profession, you will not get any experience for this skill.\nBlock Usage: If you did not pick the skills profession, you will not be able to perform the action that would grant you experience for the skill.");
				blockOtherProfessions[profession].SettingChanged += (_, _) =>
				{
					if (professionPanelElements.TryGetValue(profession, out GameObject element))
					{
						element.SetActive(blockOtherProfessions[profession].Value != ProfessionToggle.Ignored);
					}
				};
			}

			Assembly assembly = Assembly.GetExecutingAssembly();
			Harmony harmony = new(ModGUID);
			harmony.PatchAll(assembly);

			AssetBundle assets = LoadAssetBundle("professionSelect");
			professionPanel = assets.LoadAsset<GameObject>("ProfessionPanel");

			Skill_Element.blockExperience = Helper.loadSprite("blockxp.png", 20, 20);
			Skill_Element.blockUsage = Helper.loadSprite("blockusage.png", 20, 20);
		}
		catch (Exception ex)
		{
			Debug.LogError($"Professions Awake failed. Shutting down, to prevent further issues with the professions. Exception:\n{ex}");
			Application.Quit();
		}
	}

	[HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
	private static class AddRPCs
	{
		private static void Postfix(ZNet __instance, ZNetPeer peer)
		{
			if (__instance.IsServer())
			{
				peer.m_rpc.Register("Professions GetServerTime", onServerTimeRequest);
			}
			else
			{
				peer.m_rpc.Register<long>("Professions GetServerTime", onServerTimeReceived);
			}
		}
	}

	private static void onServerTimeReceived(ZRpc? rpc, long time)
	{
		serverTime = new DateTime(time);
	}

	private static void onServerTimeRequest(ZRpc rpc)
	{
		rpc.Invoke("Professions GetServerTime", DateTime.Now.Ticks);
	}

	private static void requestServerTime()
	{
		if (ZNet.instance.IsServer())
		{
			onServerTimeReceived(null, DateTime.Now.Ticks);
		}
		else
		{
			ZNet.instance.GetServerPeer().m_rpc.Invoke("Professions GetServerTime");
		}
	}

	private void Update()
	{
		PreventMainMenu.AllowMainMenu = true;

		if (Player.m_localPlayer is null)
		{
			return;
		}

		if (professionPanelHotkey.Value.IsDown() && (professionPanelInstance!.activeSelf || Player.m_localPlayer.TakeInput()))
		{
			professionPanelInstance.SetActive(!professionPanelInstance.activeSelf);

			if (professionPanelInstance.activeSelf)
			{
				UpdateSelectPanelSelections();
				requestServerTime();
			}
		}

		if (professionPanelInstance!.activeSelf && Input.GetKey(KeyCode.Escape))
		{
			professionPanelInstance.SetActive(false);
			PreventMainMenu.AllowMainMenu = false;
		}
	}

	private static void UpdateSelectPanelSelections()
	{
		List<Profession> activeProfessions = selectedProfessions(Player.m_localPlayer);
		foreach (KeyValuePair<Profession, GameObject> skillElements in professionPanelElements)
		{
			Skill_Element element = skillElements.Value.GetComponent<Skill_Element>();
			element.Toggle(activeProfessions.Contains(skillElements.Key), activeProfessions.Count >= maximumAllowedProfessions.Value);
			element.UpdateImageDisplay(blockOtherProfessions[skillElements.Key].Value);
		}

		professionPanelInstance!.GetComponent<ProfessionPanel>().description.text = $"You have {activeProfessions.Count} / {maximumAllowedProfessions.Value} professions selected.\nYou are{(allowUnselect.Value == Toggle.Off ? " not" : "")} allowed to change your professions{(professionChangeCooldown.Value > 0 && allowUnselect.Value == Toggle.On ? $" every {Helper.getHumanFriendlyTime((int)(professionChangeCooldown.Value * 3600))}" : "")}.";
	}

	[HarmonyPatch(typeof(Menu), nameof(Menu.Update))]
	private class PreventMainMenu
	{
		public static bool AllowMainMenu = true;
		private static bool Prefix() => !professionPanelInstance!.activeSelf && AllowMainMenu;
	}

	[HarmonyPatch(typeof(TextInput), nameof(TextInput.IsVisible))]
	private class DisablePlayerInputInProfessionSelector
	{
		private static void Postfix(ref bool __result)
		{
			if (professionPanelInstance?.activeSelf == true)
			{
				__result = true;
			}
		}
	}

	private static AssetBundle LoadAssetBundle(string bundleName)
	{
		string resource = typeof(Professions).Assembly.GetManifestResourceNames().Single(s => s.EndsWith(bundleName));
		return AssetBundle.LoadFromStream(typeof(Professions).Assembly.GetManifestResourceStream(resource));
	}

	[HarmonyPatch(typeof(Hud), nameof(Hud.Awake))]
	public static class InstantiateSelectPanel
	{
		private static void Prefix(Hud __instance)
		{
			Transform transform = __instance.gameObject.GetComponentInParent<Localize>().gameObject.transform;
			professionPanelInstance = Instantiate(professionPanel, transform, false);
			professionPanelInstance.SetActive(false);
			professionPanelInstance.transform.SetSiblingIndex(MessageHud.instance.transform.GetSiblingIndex());

			Skill_Element.tooltipPrefab = InventoryGui.instance.m_playerGrid.m_elementPrefab.GetComponent<UITooltip>().m_tooltipPrefab;
		}
	}

	[HarmonyPatch(typeof(Skills), nameof(Skills.Awake))]
	public static class PopulateSelectPanel
	{
		private static void Postfix(Skills __instance)
		{
			if (!professionPanelInstance) return;

			ProfessionPanel? panel = professionPanelInstance?.GetComponent<ProfessionPanel>();
			if (panel?.panelToInstantiateIn.childCount == 0)
			{
				professionPanelElements.Clear();
				foreach (Profession profession in (Profession[])Enum.GetValues(typeof(Profession)))
				{
					Skills.SkillType skillType = fromProfession(profession);
					Skills.Skill skill = __instance.GetSkill(skillType);
					if (skill.m_info is null)
					{
						__instance.m_skillData.Remove(skillType);
					}
					else
					{
						GameObject element = panel.InstantiateSkill(skill.m_info.m_icon, Localization.instance.Localize("$skill_" + (int)fromProfession(profession)), professionDescriptions[profession], "Select");
						professionPanelElements[profession] = element;
						element.SetActive(blockOtherProfessions[profession].Value != ProfessionToggle.Ignored);

						Skill_Element skillElement = element.GetComponent<Skill_Element>();
						skillElement.Select.onClick.AddListener(() =>
						{
							if (selectedProfessions(Player.m_localPlayer).Contains(profession))
							{
								if (allowUnselect.Value == Toggle.On && professionChangeCooldown.Value > 0 && Player.m_localPlayer.m_knownStations.TryGetValue("Professions LastProfessionChange", out int lastChange))
								{
									int remainingTime = lastChange + (int)(professionChangeCooldown.Value * 3600) - (int)((DateTimeOffset)serverTime).ToUnixTimeSeconds();
									if (remainingTime > 0)
									{
										Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"You can change your profession in {Helper.getHumanFriendlyTime(remainingTime)}.");
										return;
									}
								}

								Player.m_localPlayer.m_skills.m_skillData.Remove(skillType);
								Player.m_localPlayer.m_knownStations["Professions LastProfessionChange"] = (int)((DateTimeOffset)serverTime).ToUnixTimeSeconds();
							}
							else
							{
								Player.m_localPlayer.m_skills.GetSkill(skillType).m_level = 1;
							}

							UpdateSelectPanelSelections();
						});
					}
				}
			}
		}
	}

	public static List<Profession> selectedProfessions(Player player) => selectedProfessions(player.m_skills);

	private static List<Profession> selectedProfessions(Skills skills)
	{
		return ((Profession[])Enum.GetValues(typeof(Profession))).Where(profession => blockOtherProfessions[profession].Value != ProfessionToggle.Ignored && skills.m_skillData.TryGetValue(fromProfession(profession), out Skills.Skill skill) && skill.m_level > 0).ToList();
	}

	[HarmonyPatch(typeof(Skills), nameof(Skills.RaiseSkill))]
	private class PreventExperience
	{
		private static bool Prefix(Skills __instance, Skills.SkillType skillType)
		{
			return fromSkill(skillType) is not { } profession || __instance.GetSkillFactor(skillType) > 0 || blockOtherProfessions[profession].Value == ProfessionToggle.Ignored;
		}
	}

}
