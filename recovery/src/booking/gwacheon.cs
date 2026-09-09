#define TRACE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Tesseract;

namespace booking;

internal class gwacheon : club
{
	private TesseractEngine engine;

	private DateTime opentime;

	private const string loginUrl = "https://www.gctennis.co.kr/login";

	private Random rand = new Random();

	public gwacheon(int pManagerId)
	{
		managerId = pManagerId;
	}

	public override bool monitorOne(string date_p)
	{
		return false;
	}

	public override void setValues(ChromeDriver drv_p, int threadIndex_p, CoreData cd, List<bookInfo> bookReqs_p, ref dayPool _workPool)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		base.setValues(drv_p, threadIndex_p, cd, bookReqs_p, ref _workPool);
		string text = "tessdata";
		try
		{
			Trace.WriteLine("engine Init");
			if (!Directory.Exists(text))
			{
				Trace.WriteLine("No Tessdata Directory");
				return;
			}
			engine = new TesseractEngine(text, "eng", (EngineMode)3);
			engine.SetVariable("tessedit_char_whitelist", "0123456789");
			if (true)
			{
				Pix val = Pix.LoadFromFile("starcc.PNG");
				try
				{
					Page val2 = engine.Process(val, (PageSegMode?)null);
					try
					{
						val2.GetText();
					}
					finally
					{
						((IDisposable)val2)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)val)?.Dispose();
				}
			}
			Trace.WriteLine("engine Okay");
		}
		catch (Exception ex)
		{
			Trace.WriteLine("engine Error" + ex);
		}
	}

	private bool buildTeeTable()
	{
		int num = 70;
		for (int i = 0; i < bookReqs.Count; i++)
		{
			indexTeeTable[i] = num;
		}
		return true;
	}

	public override IWebElement compare(IWebElement tee, bookInfo req)
	{
		IWebElement result = null;
		try
		{
			ReadOnlyCollection<IWebElement> readOnlyCollection = ((ISearchContext)tee).FindElements(By.TagName("td"));
			frm.logtxtBox(readOnlyCollection[0].Text + "," + readOnlyCollection[1].Text + " ," + readOnlyCollection[5].Text + " index " + teeIndex);
			int num = int.Parse(readOnlyCollection[1].Text.Replace(":", ""));
			if (searchStartTime == 0)
			{
				searchStartTime = num;
			}
			if (num < req.startTime)
			{
				teeIndex++;
				increment = 1;
			}
			else if (num > req.endTime)
			{
				teeIndex--;
				increment = -1;
			}
			else
			{
				teeIndex += increment;
				if (readOnlyCollection[5].Text == "예약하기" && (req.starter == "NA" || req.starter == readOnlyCollection[0].Text))
				{
					result = readOnlyCollection[5];
				}
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + "compare " + ex);
		}
		return result;
	}

	public override bool login(Form1 _frm)
	{
		frm = _frm;
		try
		{
			string userId = managerId + "_" + id;
			mq = new mqttClient(userId, "/phone");
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1L);
			((WebDriver)drv).Navigate().GoToUrl("https://www.gctennis.co.kr/login");
			Thread.Sleep(500);
			((WebDriver)drv).Manage().Window.Maximize();
			Thread.Sleep(1000);
			bookInfo bookInfo2 = bookReqs[0];
			int year = int.Parse(bookInfo2.date.Substring(0, 4));
			int num = int.Parse(bookInfo2.date.Substring(4, 2)) - 1;
			if (int.Parse(bookInfo2.date.Substring(6, 2)) > DateTime.DaysInMonth(year, num))
			{
				num++;
			}
			opentime = frm.coreData.getOpenDate(bookInfo2.date);
			sleepShortToOpen(opentime.AddMinutes(-30.0));
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='memid']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='mempwd']")).SendKeys(pwd);
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='loginbtn1']")));
				Thread.Sleep(200);
				((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/div[7]/div/div/div[3]/p")).Click();
				Thread.Sleep(200);
				((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/div[6]/div/div/div[3]/p")).Click();
				try
				{
					string text = WebDriverExtensions.handleAlert((IWebDriver)(object)drv);
					if (text != null && text.Contains("비밀번호"))
					{
						frm.logtxtBox("thread # " + threadIndex + " Login Failed" + text);
						return false;
					}
				}
				catch (Exception ex)
				{
					frm.logtxtBox("T #" + threadIndex + "Login Error " + 0 + " Msg" + ex);
					return false;
				}
				try
				{
					Thread.Sleep(30);
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception ex3)
		{
			frm.logtxtBox("Login by Task #" + Task.CurrentId + "  " + ex3);
			return false;
		}
		return true;
	}

	private string selectDate(string date)
	{
		_ = CultureInfo.CurrentCulture.Calendar;
		int row = 0;
		int col = 0;
		int table = 0;
		GetPosDate(date, ref table, ref row, ref col);
		return "//*[@id='calendar']/div/div/table/tbody/tr[" + row + "]/td[" + col + "]/div/div[2]";
	}

	public override OpResult bookOne()
	{
		int num = -1;
		num = workPool.allocDay();
		if (num < 0)
		{
			return OpResult.Success;
		}
		frm.logtxtBox("T # " + threadIndex + " : alloc_day " + num);
		bookInfo bookInfo2 = bookReqs[num];
		teeIndex = indexTeeTable[num];
		string starter = bookReqs[0].starter;
		frm.coreData.getIndexOfStarter(starter);
		string text = "//*[@id='calendar']/div/div/div/a[8]/div/span";
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		IJavaScriptExecutor val = (IJavaScriptExecutor)(object)drv;
		sleepShortToOpen(opentime, 20);
		((WebDriver)drv).Navigate().GoToUrl("https://www.gctennis.co.kr/rent/rent");
		List<int> list = new List<int> { 3, 4, 5, 6, 7, 8, 1, 2 };
		int startH = bookInfo2.startTime / 100;
		int endH = bookInfo2.endTime / 100;
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				IWebElement val2 = ((IWebDriver)(object)drv).FindElement(By.XPath(text), 10);
				string text2 = Regex.Replace(val2.Text, "\\D", "");
				frm.logtxtBox("예약 가능 " + text2);
				if (text2.CompareTo("0") == 0)
				{
					frm.logtxtBox("No Open");
					((WebDriver)drv).Navigate().Refresh();
					Thread.Sleep(100);
					continue;
				}
				val2.Click();
				Thread.Sleep(300);
				_ = bookInfo2.startTime / 100;
				int num2 = 2;
				List<IWebElement> list2 = new List<IWebElement>();
				List<IWebElement> list3 = new List<IWebElement>();
				List<(int, int, IWebElement)> list4 = new List<(int, int, IWebElement)>();
				bool flag = false;
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((ISearchContext)((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='d1']/div[2]/div[3]/form/table/tbody"))).FindElements(By.ClassName("boxStyle"));
				frm.logtxtBox("Avails " + readOnlyCollection.Count);
				int i = 0;
				int num3 = 0;
				foreach (int item in list)
				{
					for (; i < readOnlyCollection.Count(); i++)
					{
						string[] array = readOnlyCollection[i].GetAttribute("value").Split('|');
						int num4 = int.Parse(array[0]);
						int num5 = int.Parse(array[1]);
						if (item > num5)
						{
							if (compareTime(num4, startH, endH))
							{
								list4.Add((num5, num4, readOnlyCollection[i]));
							}
							continue;
						}
						if (item < num5)
						{
							list3.Clear();
							num3 = 0;
							break;
						}
						if (compareTime(num4, startH, endH))
						{
							list2.Add(readOnlyCollection[i]);
							if (num3 + 1 < num4)
							{
								list3.Clear();
								num3 = 0;
							}
							num3 = num4;
							list3.Add(readOnlyCollection[i]);
							frm.logtxtBox($"Found avail hour {array[0]} court {array[1]}");
						}
						if (list3.Count >= num2)
						{
							frm.logtxtBox($"Found consequent {num2}");
							flag = true;
							break;
						}
					}
					if (flag)
					{
						break;
					}
				}
				if (!flag)
				{
					if (list4.Count > 0)
					{
						list3.Clear();
						num3 = 0;
						int num6 = list4[0].Item1;
						foreach (var item2 in list4)
						{
							list2.Add(item2.Item3);
							if (num3 + 1 < item2.Item2 || num6 != item2.Item1)
							{
								list3.Clear();
								num3 = 0;
							}
							list3.Add(item2.Item3);
							(num6, num3, _) = item2;
							if (list3.Count == num2)
							{
								frm.logtxtBox($"Found skip consequent {num2}");
								flag = true;
								break;
							}
						}
					}
					if (list3.Count == 0 && list2.Count > 0)
					{
						list3.Clear();
						list3.Add(list2[0]);
					}
				}
				if (list3.Count > 0)
				{
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay " + list3[0].GetAttribute("value") + " num " + list3.Count);
					val.ExecuteScript("arguments[0].scrollIntoView(true);", new object[1] { list3[0] });
					foreach (IWebElement item3 in list3)
					{
						item3.Click();
					}
					Thread.Sleep(500);
					IWebElement btn = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='d1']/div[2]/div[3]/form/div[3]/a/span"));
					opResult = tryReserve(btn);
					if (opResult != OpResult.BookOneSuccess && opResult != OpResult.DuplicateFail && opResult != OpResult.DuplicateLogin)
					{
						((WebDriver)drv).Navigate().Refresh();
						Thread.Sleep(200);
						continue;
					}
					workPool.finishDay(num);
				}
				else
				{
					workPool.finishDay(num);
					frm.logtxtBox("T # " + threadIndex + "NoProperTime");
				}
			}
			catch (Exception ex)
			{
				if (ex.Message.Contains("예약이 마감"))
				{
					frm.logtxtBox("T #" + threadIndex + " Stop " + ex.Message);
				}
				else
				{
					workPool.returnDay(num);
					frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex);
				}
				((WebDriver)drv).Navigate().Refresh();
			}
			break;
		}
		return OpResult.Fail;
	}

	private bool compareTime(int hour, int startH, int endH)
	{
		if (hour >= startH && hour <= endH)
		{
			return true;
		}
		return false;
	}

	private (IWebElement, int) findTime(ReadOnlyCollection<IWebElement> avails, int starterIndex, int startTime, int endTime)
	{
		return (null, 0);
	}

	private string myhandleAlert(IWebDriver drv)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		string result = null;
		try
		{
			((DefaultWait<IWebDriver>)new WebDriverWait(drv, TimeSpan.FromSeconds(5L))).Until<IAlert>(ExpectedConditions.AlertIsPresent());
			IAlert obj = drv.SwitchTo().Alert();
			result = obj.Text;
			obj.Accept();
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T #  handleAlert " + ex);
		}
		return result;
	}

	private string reqServer()
	{
		if (!frm.coreData.sendReqSms(mq, "01032353099", frm))
		{
			frm.logtxtBox("Sending Request Error");
			return "";
		}
		while (mq.waitAck)
		{
			Thread.Sleep(10);
		}
		_ = mq.ReceivedMessage;
		return Regex.Replace("[Web발신][과천시테니스협회] 예약 인증번호[967064]를 입력해주세요.", "\\D", "");
	}

	private OpResult tryReserve(IWebElement btn)
	{
		OpResult result = OpResult.Fail;
		string text = "";
		try
		{
			Thread.Sleep(100);
			btn.Click();
			((IWebDriver)(object)drv).FindElement(By.Id("authCodeRequest")).Click();
			frm.logtxtBox("Enter certi-numbers of your phone");
			Thread.Sleep(50000);
			return result;
		}
		catch (Exception ex)
		{
			frm.logtxtBox(" Reservation Buttone click, but Failed :" + ex);
			return result;
		}
	}
}
