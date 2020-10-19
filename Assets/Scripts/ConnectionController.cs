using Cysharp.Threading.Tasks;
using Mirror;
using UnityEngine;

public class ConnectionController : MonoBehaviour
{
	private void Start()
	{
		NetworkManager networkManager = FindObjectOfType<NetworkManager>();
		
		if (Application.isEditor)
		{
			networkManager.server.StartHost(networkManager.client).Forget();
		}
		else
		{
			networkManager.client.ConnectAsync("localhost").Forget(); 
		}
	}
}