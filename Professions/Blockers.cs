using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Profession = Professions.Professions.Profession;

namespace Professions;

public static class Blockers
{
	private static bool isAllowed(Profession profession) => Professions.blockOtherProfessions[profession].Value != Professions.ProfessionToggle.BlockUsage || Professions.selectedProfessions(Player.m_localPlayer).Contains(profession);

	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
	private class BlockBlacksmithing
	{
		private static bool CheckBlacksmithingItem(ItemDrop.ItemData.SharedData item)
		{
			return item.m_itemType is
				       ItemDrop.ItemData.ItemType.Bow or
				       ItemDrop.ItemData.ItemType.Chest or
				       ItemDrop.ItemData.ItemType.Hands or
				       ItemDrop.ItemData.ItemType.Helmet or
				       ItemDrop.ItemData.ItemType.Legs or
				       ItemDrop.ItemData.ItemType.Shield or
				       ItemDrop.ItemData.ItemType.Shoulder or
				       ItemDrop.ItemData.ItemType.TwoHandedWeapon ||
			       (item.m_itemType is ItemDrop.ItemData.ItemType.OneHandedWeapon && !item.m_attack.m_consumeItem);
		}

		[HarmonyPriority(Priority.Last)]
		private static void Postfix(InventoryGui __instance)
		{
			if ((__instance.InCraftTab() || __instance.InUpradeTab()) && __instance.m_selectedRecipe.Key?.m_item is { } itemdrop && CheckBlacksmithingItem(itemdrop.m_itemData.m_shared) && !isAllowed(Profession.Blacksmithing))
			{
				__instance.m_craftButton.interactable = false;
				__instance.m_craftButton.GetComponentInChildren<Text>().text = "You cannot perform this action, because you are not a blacksmith.";
			}
		}
	}

	[HarmonyPatch]
	public static class BlockBlacksmithingSmelter
	{
		private static IEnumerable<MethodInfo> TargetMethods() => new[]
		{
			AccessTools.DeclaredMethod(typeof(Smelter), nameof(Smelter.OnAddOre)),
			AccessTools.DeclaredMethod(typeof(Smelter), nameof(Smelter.OnAddFuel))
		};

		private static bool Prefix(Smelter __instance, ref bool __result)
		{
			switch (__instance.GetComponent<Piece>().m_name)
			{
				case "$piece_smelter":
				case "$piece_blastfurnace":
					if (isAllowed(Profession.Blacksmithing))
					{
						return true;
					}

					__result = false;
					Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not a blacksmith.");
					return false;

				case "$piece_windmill":
					if (isAllowed(Profession.Farming))
					{
						return true;
					}

					__result = false;
					Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not a farmer.");
					return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(PieceTable), nameof(PieceTable.SetCategory))]
	private class BlockBuilding
	{
		private static bool Prefix(int index)
		{
			if ((Piece.PieceCategory)index != Piece.PieceCategory.Building || isAllowed(Profession.Building))
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not a builder.");
			return false;
		}
	}

	[HarmonyPatch(typeof(PieceTable), nameof(PieceTable.PrevCategory))]
	private class BlockBuildingHotkeyPrev
	{
		private static void Postfix(PieceTable __instance)
		{
			if (__instance.m_selectedCategory == Piece.PieceCategory.Building && !isAllowed(Profession.Building))
			{
				__instance.PrevCategory();
			}
		}
	}

	[HarmonyPatch(typeof(PieceTable), nameof(PieceTable.NextCategory))]
	private class BlockBuildingHotkeyNext
	{
		private static void Postfix(PieceTable __instance)
		{
			if (__instance.m_selectedCategory == Piece.PieceCategory.Building && !isAllowed(Profession.Building))
			{
				__instance.NextCategory();
			}
		}
	}

	[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.UpdateRecipe))]
	private class BlockCookingCrafting
	{
		[HarmonyPriority(Priority.Last)]
		private static void Postfix(InventoryGui __instance)
		{
			if (__instance.m_selectedRecipe.Key?.m_item is { } itemdrop && itemdrop.m_itemData.m_shared.m_food > 0 && itemdrop.m_itemData.m_shared.m_foodStamina > 0 && !isAllowed(Profession.Cooking))
			{
				__instance.m_craftButton.interactable = false;
				__instance.m_craftButton.GetComponentInChildren<Text>().text = "You cannot perform this action, because you are not a cook.";
			}
		}
	}

	[HarmonyPatch(typeof(CookingStation), nameof(CookingStation.Interact))]
	private class BlockCookingStation
	{
		private static bool Prefix()
		{
			if (isAllowed(Profession.Cooking))
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not a cook.");
			return false;
		}
	}

	[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.IsEquipable))]
	private class BlockFarmingCultivator
	{
		private static void Postfix(ItemDrop.ItemData __instance, ref bool __result)
		{
			if (__instance.m_shared.m_name == "$item_cultivator" && !isAllowed(Profession.Farming))
			{
				__result = false;
			}
		}
	}

	[HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
	private class BlockFarmingPickingPlants
	{
		private static bool Prefix(Pickable __instance)
		{
			if (!__instance.m_nview.GetZDO().GetBool("Farming Custom Grown") || isAllowed(Profession.Farming))
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not a farmer.");
			return false;
		}
	}

	[HarmonyPatch(typeof(Plant), nameof(Plant.Grow))]
	private class SaveCustomGrownPlantState
	{
		private static GameObject TransferInfo(GameObject pickable)
		{
			pickable.GetComponent<ZNetView>().GetZDO().Set("Farming Custom Grown", true);
			return pickable;
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			MethodInfo transferInfo = AccessTools.DeclaredMethod(typeof(SaveCustomGrownPlantState), nameof(TransferInfo));
			MethodInfo instantiator = typeof(Object).GetMethods().First(m => m.Name == nameof(Object.Instantiate) && m.IsGenericMethodDefinition && m.GetParameters().Skip(1).Select(p => p.ParameterType).SequenceEqual(new[] { typeof(Vector3), typeof(Quaternion) })).MakeGenericMethod(typeof(GameObject));

			foreach (CodeInstruction instruction in instructions)
			{
				yield return instruction;
				if (instruction.opcode == OpCodes.Call && instruction.OperandIs(instantiator))
				{
					yield return new CodeInstruction(OpCodes.Call, transferInfo);
				}
			}
		}
	}

	[HarmonyPatch]
	public static class BlockLumberjacking
	{
		private static IEnumerable<MethodInfo> TargetMethods() => new[]
		{
			AccessTools.DeclaredMethod(typeof(TreeLog), nameof(TreeLog.Damage)),
			AccessTools.DeclaredMethod(typeof(TreeBase), nameof(TreeBase.Damage)),
			AccessTools.DeclaredMethod(typeof(Destructible), nameof(Destructible.Damage))
		};

		private static void Prefix(object __instance, HitData hit)
		{
			if (!isAllowed(Profession.Lumberjacking) && hit.GetAttacker() == Player.m_localPlayer)
			{
				if (__instance is not Destructible destructible || destructible.m_destructibleType == DestructibleType.Tree)
				{
					if (__instance is not TreeLog and not TreeBase)
					{
						hit.m_damage.m_slash = 0;
					}
					Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not a lumberjack.");
					hit.m_damage.m_chop = 0;
				}
			}
		}
	}

	[HarmonyPatch]
	public static class BlockMining
	{
		private static IEnumerable<MethodInfo> TargetMethods() => new[]
		{
			AccessTools.DeclaredMethod(typeof(MineRock), nameof(MineRock.Damage)),
			AccessTools.DeclaredMethod(typeof(MineRock5), nameof(MineRock5.Damage)),
			AccessTools.DeclaredMethod(typeof(Destructible), nameof(Destructible.Damage))
		};

		private static void Prefix(object __instance, HitData hit)
		{
			if (!isAllowed(Profession.Mining) && hit.GetAttacker() == Player.m_localPlayer)
			{
				if (__instance is not Destructible destructible || (destructible.m_damages.m_pickaxe > 0 && destructible.m_damages.m_chop == 0 && destructible.m_destructibleType != DestructibleType.Tree))
				{
					Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not a miner.");
					hit.m_damage.m_pickaxe = 0;
				}
			}
		}
	}

	[HarmonyPatch(typeof(Character), nameof(Character.Damage))]
	private class BlockRanching
	{
		private static bool Prefix(Character __instance, HitData hit)
		{
			if (!__instance.IsTamed() || isAllowed(Profession.Ranching) || hit.GetAttacker() != Player.m_localPlayer)
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not a rancher.");
			return false;

		}
	}

	[HarmonyPatch(typeof(ShipControlls), nameof(ShipControlls.Interact))]
	private class BlockSailing
	{
		private static bool Prefix()
		{
			if (isAllowed(Profession.Sailing))
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not a sailor.");
			return false;
		}
	}

	[HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Interact))]
	private class BlockAlchemyStation
	{
		private static bool Prefix(CraftingStation __instance)
		{
			if (!__instance.name.StartsWith("opalchemy", StringComparison.Ordinal) || isAllowed(Profession.Alchemy))
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not an alchemist.");
			return false;
		}
	}

	[HarmonyPatch(typeof(Incinerator), nameof(Incinerator.OnIncinerate))]
	private class BlockAlchemyIncinerator
	{
		private static bool Prefix(Incinerator __instance)
		{
			if (!__instance.name.StartsWith("opcauldron", StringComparison.Ordinal) || isAllowed(Profession.Alchemy))
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not an alchemist.");
			return false;
		}
	}
	
	[HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.Interact))]
	private class BlockGemcuttersTable
	{
		private static bool Prefix(CraftingStation __instance)
		{
			if (!__instance.name.StartsWith("op_transmution_table", StringComparison.Ordinal) || isAllowed(Profession.Jewelcrafting))
			{
				return true;
			}

			Player.m_localPlayer.Message(MessageHud.MessageType.Center, "You cannot perform this action, because you are not a jeweler.");
			return false;
		}
	}
}
