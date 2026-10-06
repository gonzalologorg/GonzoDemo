using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Splines.Interpolators;
using TMPro;


public class LoginBehaviour : MonoBehaviour
{
	public GameObject loginForm;
	public GameObject registerForm;
	public TMP_InputField userTextEntry;
	public TMP_InputField passTextEntry;

	public TMP_InputField rUserTextEntry;
	public TMP_InputField rPassTextEntry;
	public TMP_InputField rPassRepeatTextEntry;
	public TMP_InputField rEmailTextEntry;
	public TMP_Text rPassMatch;

	CanvasGroup registerCanvasGroup;
	RectTransform registerFormRectTransform;

	bool isRegisterFormActive = false;
	private APIRest apiClient;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		registerCanvasGroup = registerForm.GetComponent<CanvasGroup>();
		registerFormRectTransform = registerForm.GetComponent<RectTransform>();
		apiClient = GetComponent<APIRest>();
		passTextEntry.contentType = TMP_InputField.ContentType.Password;
		rPassTextEntry.contentType = TMP_InputField.ContentType.Password;
		rPassRepeatTextEntry.contentType = TMP_InputField.ContentType.Password;

		rPassMatch.gameObject.SetActive(false);
		rPassTextEntry.onValueChanged.AddListener((value) => CheckPasswordMatch());
		rPassRepeatTextEntry.onValueChanged.AddListener((value) => CheckPasswordMatch());

		userTextEntry.text = "gonzo";
		passTextEntry.text = "1234";
	}

	public void ToggleRegisterForm()
	{
		isRegisterFormActive = !isRegisterFormActive;
		StartCoroutine(FadeTo(isRegisterFormActive ? 1f : 0f, 0.25f));
		if (isRegisterFormActive)
			registerForm.SetActive(true);
	}

	// Update is called once per frame
	void Update()
	{

	}

	public void CheckPasswordMatch()
	{
		rPassMatch.gameObject.SetActive(rPassTextEntry.text != rPassRepeatTextEntry.text);
	}

	public void DoLogin()
	{
		string userInput = userTextEntry.text;
		string passInput = passTextEntry.text;

		if (string.IsNullOrEmpty(userInput) || string.IsNullOrEmpty(passInput))
		{
			ModalController.singleton.Create(gameObject, "Error", "Please insert an user and a password!", new ModalController.Option[]
			{
				new ModalController.Option("Ok", () => { })
			});
			return;
		}

		apiClient.StartCoroutine(apiClient.Login(userInput, passInput, (response) =>
		{
			transform.Find("LoginForm").gameObject.SetActive(false);
			CharSelector selector = transform.parent.Find("CharacterList").GetComponent<CharSelector>();
			selector.gameObject.SetActive(true);
			selector.SetupCharacters(response.playerData.characters);

		}));
	}

	public void DoRegister()
	{
		string rUserInput = rUserTextEntry.text;
		string rPassInput = rPassTextEntry.text;
		string rPassRepeatInput = rPassRepeatTextEntry.text;
		string rEmailInput = rEmailTextEntry.text;

		if (string.IsNullOrEmpty(rUserInput) || string.IsNullOrEmpty(rPassInput) || string.IsNullOrEmpty(rEmailInput))
		{
			ModalController.singleton.Create(gameObject, "Error", "Please insert an user/password/email!", new ModalController.Option[]
			{
				new ModalController.Option("Ok", () => { })
			});
			return;
		}

		if (rPassInput != rPassRepeatInput)
		{
			ModalController.singleton.Create(gameObject, "Error", "Passwords do not match!", new ModalController.Option[]
			{
				new ModalController.Option("Ok", () => { })
			});
			return;
		}

		apiClient.StartCoroutine(apiClient.Register(rUserInput, rPassInput, rEmailInput, () =>
		{

		}));
	}

	private IEnumerator FadeTo(float targetAlpha, float duration)
	{
		float startAlpha = registerCanvasGroup.alpha;
		float elapsedTime = 0f;
		float targetX = isRegisterFormActive ? 367f : 250f;
		while (elapsedTime < duration)
		{
			elapsedTime += Time.deltaTime;
			targetX = Mathf.Lerp(targetX, isRegisterFormActive ? 367f : 250f, elapsedTime / duration);
			Vector2 targetPos = new Vector2(targetX, -32);

			registerCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / duration);
			registerFormRectTransform.anchoredPosition = Vector2.Lerp(registerFormRectTransform.anchoredPosition, targetPos, elapsedTime / duration);
			yield return null;
		}
		registerCanvasGroup.alpha = targetAlpha;
		registerFormRectTransform.anchoredPosition = new Vector2(targetX, -32);

		if (!isRegisterFormActive)
			registerForm.SetActive(false);
	}
}
