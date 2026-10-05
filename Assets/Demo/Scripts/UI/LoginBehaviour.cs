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
	public TMP_InputField rEmailTextEntry;

	CanvasGroup registerCanvasGroup;
	RectTransform registerFormRectTransform;

	bool isRegisterFormActive = false;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
	{
		registerCanvasGroup = registerForm.GetComponent<CanvasGroup>();
		registerFormRectTransform = registerForm.GetComponent<RectTransform>();
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

	public void DoLogin()
	{
		string userInput = userTextEntry.text;
		string passInput = passTextEntry.text;
		GameObject modal = null;
		ModalController.Option[] options =
		{
			new ModalController.Option("Ok", () => {
				Destroy(modal);
			}),
			new ModalController.Option("Cancelar", () => {
				Destroy(modal);
			})
		};

		modal = ModalController.singleton.Create(gameObject, "Estas seguro?", "Este es el campo del medio", options).gameObject;
	}

	public void DoRegister()
	{
		string rUserInput = rUserTextEntry.text;
		string rPassInput = rPassTextEntry.text;
		string rEmailInput = rEmailTextEntry.text;
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
