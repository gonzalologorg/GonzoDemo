using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreatorBehaviour : MonoBehaviour
{
    public Sprite[] Portraits;
    public Image portraitComponent;
    public Slider portraitSlider;
    public TMP_InputField characterInput;
	public Toggle characterSide;

	[System.Serializable]
	public class CharacterCreationResponse
	{
		public string status;
		// POST /createcharacter returns { status, character }.
		public CreatedCharacter character;
	}

	[System.Serializable]
	public class CreatedCharacter
	{
		public int id;
	}


	APIRest apiClient;

	public void UpdatePortrait()
    {
		portraitComponent.sprite = Portraits[(int)portraitSlider.value];
	}

    public void CreateCharacter()
    {
        string value = characterInput.text;
		//trim value from spaces and invalid characters like ' and "
		value = value.Trim();
        value = value.Replace("'", "");
		value = value.Replace("\"", "");

		if (string.IsNullOrEmpty(value) || value.Length > 20 || value.Length < 3)
        {
            ModalController.singleton.Create(this.gameObject, "Invalid Name", "Character name must be between 3 and 20 characters.");
			return;
		}

		StartCoroutine(apiClient.CreateCharacter(value, (int)portraitSlider.value, characterSide.isOn ? 1 : 0, (response) =>
		{
			if (response.status == "success")
			{
				GlobalManager.Instance.CharacterId = response.character.id;
				ModalController.singleton.Create(this.gameObject, "Character Created", "Your character has been created successfully.", new ModalController.Option[]
				{
					new ModalController.Option("Ok", () => { UnityEngine.SceneManagement.SceneManager.LoadScene("MainScene"); })
				});
			}
			else
			{
				ModalController.singleton.Create(this.gameObject, "Error", response.status);
			}
		}));


	}
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        apiClient = APIRest.Instance;
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
