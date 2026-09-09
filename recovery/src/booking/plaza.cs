#define TRACE
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Tesseract;

namespace booking;

internal class plaza : club
{
	private TesseractEngine engine;

	private const string loginUrl = "https://www.plazacc.co.kr/plzcc/irsweb/golf2/member/login.do";

	public plaza(int pManagerId)
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
			((WebDriver)drv).Navigate().GoToUrl("https://www.plazacc.co.kr/plzcc/irsweb/golf2/member/login.do");
			Thread.Sleep(500);
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='username']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='password']")).SendKeys(pwd);
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='btnLogin']")));
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
				buildTeeTable();
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
		int row = 0;
		int col = 0;
		int table = 0;
		GetPosDate(date, ref table, ref row, ref col);
		return "//*[@id='Calendar" + table + "']/tbody/tr[" + row + "]/td[" + col + "]";
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
		try
		{
			WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='reservationBtn']")));
			Thread.Sleep(500);
			((WebDriver)drv).SwitchTo().Frame("iframeTopContents");
			((WebDriver)drv).SwitchTo().Frame("bookingIframe");
			Thread.Sleep(500);
			((WebDriver)drv).SwitchTo().Frame(0);
			WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='branchBtn1']")));
			Thread.Sleep(500);
			WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='btnMemberNo']")));
			Thread.Sleep(200);
			((WebDriver)drv).SwitchTo().ParentFrame();
			((WebDriver)drv).SwitchTo().Frame(1);
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex);
			workPool.returnDay(num);
			((WebDriver)drv).SwitchTo().ParentFrame();
			return OpResult.Fail;
		}
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath(text), 2);
				if (val.GetAttribute("class").Contains("preMth"))
				{
					frm.logtxtBox("No Open");
					((WebDriver)drv).FindElement(By.XPath("//*[@id='Form1']/div[1]/div[1]/input")).Click();
					Thread.Sleep(100);
					continue;
				}
				val.Click();
				Thread.Sleep(200);
				((WebDriver)drv).SwitchTo().ParentFrame();
				((WebDriver)drv).SwitchTo().Frame("ifrmStep3");
				Thread.Sleep(0);
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((WebDriver)drv).FindElements(By.XPath("//a[contains(@href,'" + bookInfo2.date + "')]"));
				if (readOnlyCollection.Count == 0)
				{
					frm.logtxtBox("No Available");
					((WebDriver)drv).SwitchTo().Frame(1);
					continue;
				}
				frm.logtxtBox("Available " + readOnlyCollection.Count);
				char[] trimChars = new char[2] { ' ', '\'' };
				val = null;
				foreach (IWebElement item in readOnlyCollection)
				{
					int num3 = int.Parse(item.GetAttribute("href").Split(',')[2].Trim(trimChars));
					if (num3 >= bookInfo2.startTime && num3 <= bookInfo2.endTime)
					{
						val = item;
						break;
					}
				}
				if (val != null)
				{
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay ");
					opResult = tryReserve(val);
					if (opResult == OpResult.BookOneSuccess || opResult == OpResult.DuplicateFail || opResult == OpResult.DuplicateLogin)
					{
						workPool.finishDay(num);
						break;
					}
					((WebDriver)drv).SwitchTo().Frame(1);
					continue;
				}
				workPool.finishDay(num);
				frm.logtxtBox("T # " + threadIndex + " NoProperTime");
				return OpResult.OneFinish;
			}
			catch (Exception ex2)
			{
				if (ex2.Message.Contains("예약이 마감"))
				{
					frm.logtxtBox("T #" + threadIndex + " Stop " + ex2.Message);
					break;
				}
				workPool.returnDay(num);
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex2);
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
			Thread.Sleep(200);
			frm.logtxtBox("tee Clicked");
			string text = handleAlert();
			if (text == null)
			{
				((WebDriver)drv).SwitchTo().ParentFrame();
				if (((IWebDriver)(object)drv).FindElements(By.XPath("//*[@id='catpchaImg']")) != null)
				{
					frm.logtxtBox("성공했습니다. 나머지 입력하세요 !!!!!");
					Thread.Sleep(500000);
					result = OpResult.BookOneSuccess;
				}
			}
			else
			{
				frm.logtxtBox("tryReserve Click Failed with " + text);
			}
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
