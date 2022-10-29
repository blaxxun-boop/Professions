using System;
using System.IO;
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
}
