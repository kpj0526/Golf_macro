using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace booking;

public class FormKakaoLogin : Form
{
	private IContainer components;

	private WebView2 webView21;

	private Button button1;

	private Button button2;

	public FormKakaoLogin()
	{
		InitializeComponent();
	}

	private void FormKakaoLogin_Load(object sender, EventArgs e)
	{
	}

	private void webView21_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
	{
		string code = getCode();
		if (code != "")
		{
			Kakao.USER_CODE = code;
			Kakao.ACCESS_TOKEN = getToken();
			base.DialogResult = DialogResult.OK;
			Close();
		}
	}

	private void button1_Click(object sender, EventArgs e)
	{
		if (webView21 != null && webView21.CoreWebView2 != null)
		{
			webView21.CoreWebView2.Navigate("https://kauth.kakao.com/oauth/authorize?response_type=code&client_id=70cdf46ec0274b5f6ec4c04770ea0283&redirect_uri=https://www.naver.com/oauth");
		}
	}

	public string getCode()
	{
		string text = webView21.Source.ToString();
		string text2 = text.Substring(text.IndexOf("=") + 1);
		if (text.CompareTo("https://www.naver.com/oauth?code=" + text2) == 0)
		{
			return text2;
		}
		return "";
	}

	public string getToken()
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		RestClient val = new RestClient("https://kauth.kakao.com");
		RestRequest val2 = new RestRequest("/oauth/token", (Method)1);
		val2.AddParameter("grant_type", (object)"authorization_code");
		val2.AddParameter("client_id", (object)"70cdf46ec0274b5f6ec4c04770ea0283");
		val2.AddParameter("redirect_uri", (object)"https://www.naver.com/oauth");
		val2.AddParameter("code", (object)Kakao.USER_CODE);
		return ((object)JObject.Parse(val.Execute((IRestRequest)(object)val2).Content)["access_token"]).ToString();
	}

	private void button2_Click(object sender, EventArgs e)
	{
		sendMessageToMyself("성우야 힘내");
		sendTemplateMessageToMyself("64997");
	}

	public void sendMessageToMyself(string message)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected O, but got Unknown
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000c: Expected O, but got Unknown
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Expected O, but got Unknown
		JObject val = new JObject();
		JObject val2 = new JObject();
		val.Add("object_type", (JToken)("text"));
		val.Add("text", (JToken)(message));
		val2.Add("web_url", (JToken)("https://developers.kakao.com"));
		val2.Add("mobile_web_url", (JToken)("https://developers.kakao.com"));
		val.Add("link", (JToken)(object)val2);
		val.Add("button_title", (JToken)("링크 이동"));
		RestClient val3 = new RestClient("https://kapi.kakao.com");
		RestRequest val4 = new RestRequest("/v2/api/talk/memo/default/send", (Method)1);
		val4.AddHeader("Authorization", "bearer " + Kakao.ACCESS_TOKEN);
		val4.AddParameter("template_object", (object)val);
		if (val3.Execute((IRestRequest)(object)val4).IsSuccessful)
		{
			MessageBox.Show("메시지 전송 성공!");
		}
		else
		{
			MessageBox.Show("메시지 전송 실패!");
		}
	}

	public void sendTemplateMessageToMyself(string templateNum)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		RestClient val = new RestClient("https://kapi.kakao.com");
		RestRequest val2 = new RestRequest("/v2/api/talk/memo/send", (Method)1);
		val2.AddHeader("Authorization", "bearer " + Kakao.ACCESS_TOKEN);
		val2.AddParameter("template_id", (object)templateNum);
		if (val.Execute((IRestRequest)(object)val2).IsSuccessful)
		{
			Console.WriteLine("메시지 보내기 성공");
		}
		else
		{
			Console.WriteLine("메시지 보내기 실패");
		}
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing && components != null)
		{
			components.Dispose();
		}
		base.Dispose(disposing);
	}

	private void InitializeComponent()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Expected O, but got Unknown
		this.webView21 = new WebView2();
		this.button1 = new System.Windows.Forms.Button();
		this.button2 = new System.Windows.Forms.Button();
		((System.ComponentModel.ISupportInitialize)this.webView21).BeginInit();
		base.SuspendLayout();
		this.webView21.CreationProperties = null;
		this.webView21.DefaultBackgroundColor = System.Drawing.Color.White;
		((System.Windows.Forms.Control)(object)this.webView21).Location = new System.Drawing.Point(22, 26);
		((System.Windows.Forms.Control)(object)this.webView21).Name = "webView21";
		((System.Windows.Forms.Control)(object)this.webView21).Size = new System.Drawing.Size(755, 447);
		this.webView21.Source = new System.Uri("http://google.com", System.UriKind.Absolute);
		((System.Windows.Forms.Control)(object)this.webView21).TabIndex = 0;
		this.webView21.ZoomFactor = 1.0;
		this.webView21.NavigationCompleted += new System.EventHandler<CoreWebView2NavigationCompletedEventArgs>(webView21_NavigationCompleted);
		this.button1.Location = new System.Drawing.Point(805, 108);
		this.button1.Name = "button1";
		this.button1.Size = new System.Drawing.Size(115, 45);
		this.button1.TabIndex = 1;
		this.button1.Text = "Login";
		this.button1.UseVisualStyleBackColor = true;
		this.button1.Click += new System.EventHandler(button1_Click);
		this.button2.Location = new System.Drawing.Point(805, 196);
		this.button2.Name = "button2";
		this.button2.Size = new System.Drawing.Size(115, 45);
		this.button2.TabIndex = 2;
		this.button2.Text = "Send Msg";
		this.button2.UseVisualStyleBackColor = true;
		this.button2.Click += new System.EventHandler(button2_Click);
		base.AutoScaleDimensions = new System.Drawing.SizeF(9f, 20f);
		base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
		base.ClientSize = new System.Drawing.Size(928, 502);
		base.Controls.Add(this.button2);
		base.Controls.Add(this.button1);
		base.Controls.Add((System.Windows.Forms.Control?)(object)this.webView21);
		base.Name = "FormKakaoLogin";
		this.Text = "FormKakaoLogin";
		base.Load += new System.EventHandler(FormKakaoLogin_Load);
		((System.ComponentModel.ISupportInitialize)this.webView21).EndInit();
		base.ResumeLayout(false);
	}
}
