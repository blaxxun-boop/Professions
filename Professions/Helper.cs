using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Professions;

public static class Helper
{
	private static byte[] ReadEmbeddedFileBytes(string name)
	{
		using MemoryStream stream = new();
		Assembly.GetExecutingAssembly().GetManifestResourceStream("Professions." + name)?.CopyTo(stream);
		return stream.ToArray();
	}

	private static Texture2D loadTexture(string name)
	{
		Texture2D texture = new(0, 0);
		texture.LoadImage(ReadEmbeddedFileBytes("icons." + name));
		return texture;
	}

	public static Sprite loadSprite(string name, int width, int height) => Sprite.Create(loadTexture(name), new Rect(0, 0, width, height), Vector2.zero);

	public static string getHumanFriendlyTime(int seconds)
	{
		TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);

		if (timeSpan.TotalSeconds < 60)
		{
			return "less than 1 minute";
		}

		string timeString = "";
		if (timeSpan.TotalDays >= 1)
		{
			timeString += $"{(int)timeSpan.TotalDays} day" + (timeSpan.TotalDays >= 2 ? "s" : "");
		}
		if (timeSpan.Hours >= 1)
		{
			if (timeSpan.TotalDays >= 1)
			{
				timeString += " and ";
			}
			timeString += $"{timeSpan.Hours} hour" + (timeSpan.Hours >= 2 ? "s" : "");
		}
		if (timeSpan.Minutes >= 1)
		{
			if (timeSpan.TotalDays >= 1 || timeSpan.Hours >= 1)
			{
				timeString += " and ";
			}
			timeString += $"{timeSpan.Minutes} minute" + (timeSpan.Minutes >= 2 ? "s" : "");
		}
		return timeString;
	}

	public static HashSet<Professions.Profession> getActiveProfessions()
	{
		HashSet<Professions.Profession> professions = new();
		if (Player.m_localPlayer.m_customData.TryGetValue("Professions Active", out string stored))
		{
			foreach (string professionStr in stored.Split(','))
			{
				if (Enum.TryParse(professionStr, out Professions.Profession profession))
				{
					professions.Add(profession);
				}
			}
		}
		return professions;
	}

	public static Dictionary<Professions.Profession, float> getInactiveProfessions()
	{
		Dictionary<Professions.Profession, float> professions = ((Professions.Profession[])Enum.GetValues(typeof(Professions.Profession))).ToDictionary(p => p, _ => 0f);
		foreach (Professions.Profession profession in getActiveProfessions())
		{
			professions.Remove(profession);
		}
		if (Player.m_localPlayer.m_customData.TryGetValue("Professions Inactive", out string stored))
		{
			foreach (string professionStr in stored.Split(','))
			{
				string[] split = professionStr.Split(':');
				if (Enum.TryParse(split[0], out Professions.Profession profession))
				{
					float level = 0;
					if (split.Length > 1)
					{
						float.TryParse(split[1], NumberStyles.Float, CultureInfo.InvariantCulture, out level);
					}
					professions[profession] = level;
				}
			}
		}
		return professions;
	}

	public static void storeActiveProfessions(HashSet<Professions.Profession> professions)
	{
		if (professions.Count == 0)
		{
			Player.m_localPlayer.m_customData.Remove("Professions Active");
		}
		else
		{
			Player.m_localPlayer.m_customData["Professions Active"] = string.Join(",", professions.Select(p => p.ToString()));
		}
	}

	public static void storeInactiveProfessions(Dictionary<Professions.Profession, float> professions)
	{
		if (professions.Count(kv => kv.Value > 0) == 0)
		{
			Player.m_localPlayer.m_customData.Remove("Professions Inactive");
		}
		else
		{
			Player.m_localPlayer.m_customData["Professions Inactive"] = string.Join(",", professions.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key}:{kv.Value.ToString(CultureInfo.InvariantCulture)}"));
		}
	}
}
