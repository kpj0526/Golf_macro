using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

namespace booking;

public class mqttClient
{
	private MqttClient client;

	private string clientId;

	private string brokerAddr;

	private string topic;

	public bool clientSecurity;

	private string serverDir;

	public bool waitAck;

	public string ReceivedMessage;

	public string broker;

	public bool connected;

	public bool noServer;

	public mqttClient(string userId, string pTopic = null)
	{
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Expected O, but got Unknown
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Expected O, but got Unknown
		topic = "/image";
		broker = "3.37.228.179";
		try
		{
			GetLocalIPAddress();
			brokerAddr = broker;
			if (userId.StartsWith("4_") || userId.StartsWith("3_") || userId.StartsWith("1_") || userId.StartsWith("7_") || userId.StartsWith("1700_"))
			{
				topic = "/image";
			}
			if (pTopic != null)
			{
				topic = pTopic;
			}
			serverDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
			client = new MqttClient(brokerAddr);
			client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
			clientId = userId + Guid.NewGuid();
			clientId = clientId.Substring(0, 10);
			client.Connect(clientId);
			string[] array = new string[1] { "/golf/" + clientId };
			client.Subscribe(array, new byte[1] { 2 });
		}
		catch (Exception ex)
		{
			MessageBox.Show("서버 연결 오류: 서버 상태와 서버 ip를 설정에서 확인하고 프로그램을 다시 시작해 주세요" + ex.Message);
			connected = false;
		}
	}

	public void finish()
	{
		try
		{
			client.Disconnect();
		}
		catch (Exception)
		{
		}
	}

	public bool publish(byte[] file)
	{
		if (waitAck)
		{
			MessageBox.Show("서버 연결을 확인해 주세요");
			return false;
		}
		byte[] array = Encoding.UTF8.GetBytes(clientId).Concat(file).ToArray();
		client.Publish(topic, array, (byte)2, false);
		waitAck = true;
		return true;
	}

	private void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
	{
		ReceivedMessage = Encoding.UTF8.GetString(e.Message);
		if (waitAck)
		{
			waitAck = false;
		}
		else
		{
			MessageBox.Show("서버 연결 오류: 서버 상태와 서버 ip를 설정에서 확인하고 프로그램을 다시 시작해 주세요");
		}
	}

	public string GetLocalIPAddress()
	{
		IPAddress[] addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
		foreach (IPAddress iPAddress in addressList)
		{
			if (iPAddress.AddressFamily == AddressFamily.InterNetwork)
			{
				return iPAddress.ToString();
			}
		}
		throw new Exception("No network adapters with an IPv4 address in the system!");
	}

	private void serverAddfileTest()
	{
		string[] array = new string[4] { "아이유", "윤하", "태연", "윤아" };
		serverDir += "/kakaoMSvr";
		DirectoryInfo directoryInfo = new DirectoryInfo(serverDir);
		if (!directoryInfo.Exists)
		{
			directoryInfo.Create();
		}
		string text = DateTime.Now.ToString("yyyy-MM");
		directoryInfo = new DirectoryInfo(serverDir + "/" + text);
		if (!directoryInfo.Exists)
		{
			directoryInfo.Create();
		}
		Random random = new Random();
		for (int i = 1; i < 30; i++)
		{
			string text2 = DateTime.Now.ToString("yyyyMM") + i.ToString("D2");
			string path = directoryInfo.FullName + "/" + text2 + ".txt";
			for (int j = 0; j < 3000; j++)
			{
				using StreamWriter streamWriter = File.AppendText(path);
				streamWriter.WriteLine(array[random.Next(array.Length)] + "," + DateTime.Now.ToString("yyyyMMddHHmmss"));
			}
		}
	}
}
