using UnityEngine;

public class GlobalManager : MonoBehaviour
{
    public static GlobalManager Instance { get; private set; }
    public ItemDatabase itemDatabase;
    public string Token { get; set; }
    public int CharacterId { get; set; }
    public PlayerData PlayerData { get; set; }

	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start()
    {
        
    }

    void Awake()
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
        itemDatabase = (ItemDatabase)Instantiate(Resources.Load("Data/ItemDatabase"), this.transform);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
