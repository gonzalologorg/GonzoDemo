using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CharSelector : MonoBehaviour
{
    public GameObject characterCardPrefab;
    public GameObject creatorFrame;
	public Transform characterPanelTransform;
    public GameObject characterNewOption;
	public Sprite[] Portraits;

    Color32[] sideColors = new Color32[]
	{
		new Color32(0, 0, 255, 255), // Blue
        new Color32(255, 0, 0, 255)  // Red
    };

	CanvasGroup frameObject;
    float alphaValue = 0.0f;
    void Awake()
    {
		frameObject = GetComponentInChildren<CanvasGroup>();
	}

    IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = frameObject.alpha;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            frameObject.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
            yield return null;
        }
        frameObject.alpha = targetAlpha;
    }

    public void SetupCharacters(PlayerData.CharacterInfo[] characters)
    {
        for (int i = 0; i < characters.Length; i++)
        {
			GameObject characterCard = Instantiate(characterCardPrefab, characterPanelTransform);
			characterCard.transform.Find("charname").GetComponent<TMP_Text>().text = characters[i].name;
			characterCard.transform.Find("chargraph").GetComponent<Image>().sprite = Portraits[characters[i].portrait];
            characterCard.GetComponent<Image>().color = sideColors[characters[i].side];
		}
    }

		// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        StartCoroutine(FadeTo(1f, 0.25f));
	}

	// Update is called once per frame
	void Update()
    {
        
    }
}
