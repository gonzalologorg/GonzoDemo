using UnityEngine;

public class GlobalManager : MonoBehaviour
{
    public static GlobalManager Instance { get; private set; }
    public string Token { get; set; }
    public int CharacterId { get; set; }
    public PlayerData PlayerData { get; set; }

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
