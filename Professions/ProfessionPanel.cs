using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ReSharper disable once CheckNamespace
public class ProfessionPanel : MonoBehaviour
{
	[Header("Stuff To replace")]
	public Image SelectionBKG = null!;
	public Image ContentBkg = null!;
	public Text header = null!;
	public Image linebreak = null!;
	public Text description = null!;

	[Header("Skill Element")]
	public GameObject SkillElementGO = null!;
	public Skill_Element skill_element = null!;
	public RectTransform panelToInstantiateIn = null!;

	public GameObject InstantiateSkill(Sprite icon, string title, string desc, string buttontext)
	{
		GameObject go = Instantiate(SkillElementGO, panelToInstantiateIn);
		Skill_Element? sk = go.GetComponent<Skill_Element>();
		sk.m_icon.sprite = icon;
		sk.m_desc.text = desc;
		sk.m_Title.text = title;
		sk.buttontxt.text = buttontext;
		return go;
	}

	public void Awake()
	{
		GameObject ingameGui = transform.parent.gameObject;

		Image ornament = ingameGui.transform.Find("Menu/MenuRoot/Menu/ornament").gameObject.GetComponent<Image>();
		Image background = ingameGui.transform.Find("Inventory_screen/root/Player/Bkg").gameObject.GetComponent<Image>();
		TMP_FontAsset fontStyle = ingameGui.transform.Find("Inventory_screen/root/Player/Armor/ac_text").gameObject.GetComponent<TextMeshProUGUI>().font;

		description.font = fontStyle.sourceFontFile;
		header.font = fontStyle.sourceFontFile;
		header.text = "Select Profession";
		linebreak.sprite = ornament.sprite;
		ContentBkg.sprite = background.sprite;
		ContentBkg.material = background.material;
		SelectionBKG.material = background.material;
		SelectionBKG.sprite = background.sprite;
	}
}
