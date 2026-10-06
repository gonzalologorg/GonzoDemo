using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


public class ModalController : MonoBehaviour
{
	[Serializable]
	public class Option
	{
		public string Text;
		public UnityAction onClick;

		public Option(string text, UnityAction onClick)
		{
			Text = text;
			this.onClick = onClick;
		}
	}

	private static ModalController _singleton;
	private static GameObject CurrentModal;

	public static ModalController singleton {
		get
		{
			if (_singleton == null)
			{
				_singleton = FindAnyObjectByType<ModalController>();
				if (_singleton == null)
				{
					GameObject modalControllerObject = new GameObject("ModalController");
					_singleton = modalControllerObject.AddComponent<ModalController>();
				}
			}
			return _singleton;
		}
	}

	public GameObject Title;
    public GameObject Description;
    public GameObject Footer;
	ModalController modalPrefab;
	public GameObject ButtonBase;
    public Option[] Options;

	private void Awake()
	{
		modalPrefab = Resources.Load<ModalController>("UI/Modal");
	}

	public ModalController Create(GameObject parent, string Title, string Description, Option[] Options = null)
	{
		if (CurrentModal != null)
		{
			Destroy(CurrentModal);
		}
		ModalController modal = Instantiate(modalPrefab, parent.transform);
		modal.SetupModal(Title, Description, Options);
		CurrentModal = modal.gameObject;

		if (Options == null || Options.Length == 0)
		{
			modal.Footer.SetActive(false);
		}
		return modal;
	}

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
		if (Options == null) return;

		foreach (Option option in Options)
        {
			GameObject button = Instantiate(ButtonBase);
			button.SetActive(true);
			button.transform.SetParent(Footer.transform, false);
			Button uiButton = button.GetComponent<Button>();
			uiButton.onClick.AddListener(() =>
			{
				option.onClick.Invoke();
				Destroy(gameObject);
			});
			TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();
			buttonText.text = option.Text;
		}
	}

	public void SetupModal(string title, string description, Option[] options = null)
	{
		Title.GetComponent<TMP_Text>().text = title;
		Description.GetComponent<TMP_Text>().text = description;
		if (options != null && options.Length > 0)
			Options = options;
	}

    // Update is called once per frame
    void Update()
    {
        
    }
}
