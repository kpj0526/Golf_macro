#define TRACE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Tesseract;

namespace booking;

internal class seolhae : club
{
	private TesseractEngine engine;

	private const string loginUrl = "https://www.seolhaeone.com/reservation/golf-day.do";

	public seolhae(int pManagerId)
	{
		managerId = pManagerId;
	}

	public override bool monitorOne(string date_p)
	{
		return false;
	}

	public override void setValues(ChromeDriver drv_p, int threadIndex_p, CoreData cd, List<bookInfo> bookReqs_p, ref dayPool _workPool)
	{
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Expected O, but got Unknown
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
			mq = new mqttClient(userId);
			frm.coreData.testServer(mq, 1, frm);
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1L);
			((WebDriver)drv).Navigate().GoToUrl("https://www.seolhaeone.com/reservation/golf-day.do");
			Thread.Sleep(500);
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@name='ID']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='password']")).SendKeys(pwd);
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='btn_login']")));
				Thread.Sleep(200);
				try
				{
					handleAlert();
				}
				catch (Exception ex)
				{
					frm.logtxtBox("T #" + threadIndex + " : Course click Error " + 0 + " Msg" + ex);
					return false;
				}
				try
				{
					((IWebDriver)(object)drv).FindElement(By.Id("popCheck")).Click();
					((IWebDriver)(object)drv).FindElement(By.Id("popCloseBtn")).Click();
					Thread.Sleep(30);
				}
				catch (Exception)
				{
				}
			}
		}
		catch (Exception)
		{
			return false;
		}
		return true;
	}

	private string handleAlert()
	{
		string result = null;
		try
		{
			IAlert obj = ((WebDriver)drv).SwitchTo().Alert();
			result = obj.Text;
			obj.Accept();
			return result;
		}
		catch (Exception)
		{
			return result;
		}
	}

	private string selectDate(string date)
	{
		return "//*[@id='day_" + date + "']";
	}

	public override OpResult bookOne()
	{
		int num = -1;
		int num2 = openTimeCheck();
		if (num2 > 3 && !frm.testMode)
		{
			frm.logtxtBox("T # Long Sleep " + num2);
			Thread.Sleep(10000);
			return OpResult.NotOpen;
		}
		num = workPool.allocDay();
		if (num < 0)
		{
			return OpResult.Success;
		}
		frm.logtxtBox("T # " + threadIndex + " : alloc_day " + num);
		bookInfo bookInfo2 = bookReqs[num];
		teeIndex = indexTeeTable[num];
		string text = selectDate(bookInfo2.date);
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath(text));
				if (val.GetAttribute("class") == "no_reservation")
				{
					frm.logtxtBox("No Open");
					((WebDriver)drv).Navigate().Refresh();
					Thread.Sleep(100);
					continue;
				}
				val.Click();
				((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/div[1]/div[2]/div[2]/div[1]/div/div[2]/div[2]/a")).Click();
				Thread.Sleep(200);
				while (true)
				{
					ReadOnlyCollection<IWebElement> readOnlyCollection = ((WebDriver)drv).FindElements(By.ClassName("btn_select"));
					if (readOnlyCollection.Count == 0)
					{
						frm.logtxtBox("No Available");
						continue;
					}
					frm.logtxtBox("Available " + readOnlyCollection.Count);
					char[] trimChars = new char[2] { ' ', '\'' };
					val = null;
					List<IWebElement> list = new List<IWebElement>();
					foreach (IWebElement item in readOnlyCollection)
					{
						string[] array = item.GetAttribute("onclick").Split(',');
						int num3 = int.Parse(array[2].Trim(trimChars));
						if (num3 >= bookInfo2.startTime && num3 <= bookInfo2.endTime)
						{
							if (!array[1].StartsWith("'더 레전드"))
							{
								val = item;
								break;
							}
							list.Add(item);
						}
					}
					if (val == null && list.Count() > 0)
					{
						val = list[0];
					}
					if (val == null)
					{
						break;
					}
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay ");
					opResult = tryReserve(val);
					if (opResult == OpResult.BookOneSuccess || opResult == OpResult.DuplicateFail || opResult == OpResult.DuplicateLogin)
					{
						workPool.finishDay(num);
						continue;
					}
					((WebDriver)drv).FindElement(By.ClassName("btn_all_reset")).Click();
					frm.logtxtBox("재설정 Clicked");
					Thread.Sleep(100);
				}
				workPool.finishDay(num);
				frm.logtxtBox("T # " + threadIndex + " NoProperTime");
				return OpResult.OneFinish;
			}
			catch (Exception ex)
			{
				if (ex.Message.Contains("예약이 마감"))
				{
					frm.logtxtBox("T #" + threadIndex + " Stop " + ex.Message);
					break;
				}
				workPool.returnDay(num);
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex);
				break;
			}
		}
		return OpResult.Fail;
	}

	private OpResult tryReserve(IWebElement btn)
	{
		OpResult result = OpResult.Fail;
		try
		{
			btn.Click();
			Thread.Sleep(10);
			((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='reservation_info_div']/div[2]/a")).Click();
			frm.logtxtBox("tee Clicked");
			Thread.Sleep(200);
			string text = handleAlert();
			if (text == null)
			{
				((WebDriver)drv).FindElement(By.XPath("//*[@id='reservationPopup']/div/div[1]/div[1]/label")).Click();
				((WebDriver)drv).FindElement(By.XPath("//*[@id='reservationPopup']/div/div[3]/a[2]")).Click();
				text = handleAlert();
				if (text != null)
				{
					frm.logtxtBox("Failed at last step " + text);
				}
				else
				{
					text = ((WebDriver)drv).FindElement(By.XPath("//*[@id='container']/div[2]/div[1]")).Text;
					if (text.Contains("골프 예약이 완료"))
					{
						frm.logtxtBox("성공했습니다 !!!!!");
					}
					Thread.Sleep(500000);
					result = OpResult.BookOneSuccess;
				}
			}
			((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='reservationPopup']/div/div[3]/a[1]")).Click();
			Thread.Sleep(100);
			frm.logtxtBox("tryReserve Click Failed with " + text);
		}
		catch (Exception ex)
		{
			string text2 = handleAlert();
			string text3 = "";
			if (text2 != null)
			{
				text3 = text2;
			}
			frm.logtxtBox(" Reservation Buttone click, but Failed " + text3 + ":" + ex);
		}
		return result;
	}
}
