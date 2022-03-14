using UnityEngine;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
public class Skill_Element : MonoBehaviour
{
	[Header("Stuff To replace")]
	public Image m_icon = null!;
	public Text m_Title = null!;
	public Text m_desc = null!;
	public Button Select = null!;
	public Text buttontxt = null!;
	public Image[] configOptions = null!;

	public static Sprite blockUsage = null!;
	public static Sprite blockExperience = null!;
	public static GameObject tooltipPrefab = null!;

	public void Awake()
	{
		Button refButton = Menu.instance.transform.Find("MenuRoot/ExitConfirm/dialog/Button_yes").GetComponent<Button>();
		Select.spriteState = refButton.spriteState;
		Select.transition = Selectable.Transition.SpriteSwap;
		Select.GetComponent<Image>().sprite = refButton.GetComponent<Image>().sprite;

		foreach (Image configOption in configOptions)
		{
			configOption.GetComponent<UITooltip>().m_tooltipPrefab = tooltipPrefab;
		}
	}

	public void UpdateImageDisplay(Professions.Professions.ProfessionToggle toggle)
	{
		int usedImages = 0;
		switch (toggle)
		{
			case Professions.Professions.ProfessionToggle.BlockUsage:
				UITooltip tooltip = configOptions[usedImages].GetComponent<UITooltip>();
				tooltip.m_text = "If you do not pick this profession, you will not be able to perform these actions.";
				tooltip.m_topic = "Usage blocked";
				configOptions[usedImages++].sprite = blockUsage;
				break;
			case Professions.Professions.ProfessionToggle.BlockExperience:
				tooltip = configOptions[usedImages].GetComponent<UITooltip>();
				tooltip.m_text = "If you do not pick this profession, you will still be able to perform these actions, but will not gain experience.";
				tooltip.m_topic = "Experience blocked";
				configOptions[usedImages++].sprite = blockExperience;
				break;
		}
		for (int i = 0; i < configOptions.Length; ++i)
		{
			configOptions[i].gameObject.SetActive(i < usedImages);
		}
	}

	public void Toggle(bool selected, bool maxProfessionsReached)
	{
		Select.interactable = selected ? Professions.Professions.allowUnselect.Value == Professions.Professions.Toggle.On : !maxProfessionsReached;
		if (selected)
		{
			buttontxt.text = Professions.Professions.allowUnselect.Value == Professions.Professions.Toggle.On ? "Unlearn" : "Selected";
		}
		else
		{
			buttontxt.text = "Select";
		}
	}
}
