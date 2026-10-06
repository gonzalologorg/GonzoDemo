using Blocks.Gameplay.Core;
using System;
using System.Collections;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.Networking;
using static CreatorBehaviour;

public class APIRest : MonoBehaviour
{
	[Serializable]
	public class LoginRequest
	{
		public string username;
		public string password;
	}

	[Serializable]
	public class RegisterRequest
	{
		public string username;
		public string password;
		public string email;
	}

	[Serializable]
	public class CharacterResponse
	{
		public string status;
		public string token;
		public PlayerData playerData;
	}

	[Serializable]
	public class CharacterCreationResponse
	{
		public string status;
		public PlayerData playerData;
		public int characterId;
	}

	[Serializable]
	public class CharacterCreationRequest
	{
		public string name;
		public int portrait;
		public int side;
		public string token;
	}

	public static APIRest Instance { get; private set; }

	private void Awake()
	{
		if (Instance == null)
			Instance = this;
	}

	public IEnumerator Login(string user, string password, Action<CharacterResponse> action)
	{
		GameObject modal = ModalController.singleton.Create(gameObject, "", "Login in...").gameObject;
		string json = JsonUtility.ToJson(new LoginRequest
		{
			username = user,
			password = password
		});

		using UnityWebRequest request =
			UnityWebRequest.Post("http://localhost:3000/login", json, "application/json");	

		yield return request.SendWebRequest();

		if (request.responseCode != 200)
		{
			ModalController.singleton.Create(gameObject, "Error", request.downloadHandler.text, new ModalController.Option[]
			{
				new ModalController.Option("Ok", () => { })
			});
			yield break;
		}

		CharacterResponse response = JsonUtility.FromJson<CharacterResponse>(request.downloadHandler.text);
		GlobalManager.Instance.Token = response.token;
		GlobalManager.Instance.PlayerData = response.playerData;
		Destroy(modal);
		action?.Invoke(response);
	}

	public IEnumerator Register(string user, string password, string email, Action action)
	{
		GameObject modal = ModalController.singleton.Create(gameObject, "", "Registering...").gameObject;
		string json = JsonUtility.ToJson(new RegisterRequest
		{
			username = user,
			password = password,
			email = email
		});
		using UnityWebRequest request =
			UnityWebRequest.Post("http://localhost:3000/signup", json, "application/json");
		yield return request.SendWebRequest();
		if (request.responseCode != 200)
		{
			ModalController.singleton.Create(gameObject, "Error", request.downloadHandler.text, new ModalController.Option[]
			{
				new ModalController.Option("Ok", () => { })
			});
			yield break;
		}

		CharacterResponse response = JsonUtility.FromJson<CharacterResponse>(request.downloadHandler.text);
		GlobalManager.Instance.Token = response.token;
		GlobalManager.Instance.PlayerData = response.playerData;
		Destroy(modal);
		action?.Invoke();
	}

	public IEnumerator CreateCharacter(string name, int portraitIndex, int side, Action<CharacterCreationResponse> action)
	{
		GameObject modal = ModalController.singleton.Create(gameObject, "", "Creating character...").gameObject;
		string json = JsonUtility.ToJson(new CharacterCreationRequest
		{
			name = name,
			portrait = portraitIndex,
			side = side,
			token = GlobalManager.Instance.Token
		});
		using UnityWebRequest request =
			UnityWebRequest.Post("http://localhost:3000/createcharacter", json, "application/json");
		yield return request.SendWebRequest();
		if (request.responseCode != 200)
		{
			ModalController.singleton.Create(gameObject, "Error", request.downloadHandler.text, new ModalController.Option[]
			{
				new ModalController.Option("Ok", () => { })
			});
			yield break;
		}
		CharacterCreationResponse response = JsonUtility.FromJson<CharacterCreationResponse>(request.downloadHandler.text);
		GlobalManager.Instance.PlayerData = response.playerData;
		GlobalManager.Instance.CharacterId = response.characterId;
		Destroy(modal);
		action?.Invoke(response);
	}
}
