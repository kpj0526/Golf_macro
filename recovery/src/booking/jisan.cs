using System;
using System.Collections.ObjectModel;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace booking;

internal class jisan : club
{
	private DateTime opentime;

	private IWebElement target;

	private int teeTableCnt;

	private const string loginUrl = "https://www.jisanresort.co.kr/w/reservation/golfResv/member_reserv01.asp";

	public override bool monitorOne(string date_p)
	{
		return false;
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
			frm.logtxtBox(tee.Text + " index " + teeIndex);
			int num = int.Parse(tee.Text.Split(" ")[0].Replace(":", ""));
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
				result = tee;
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
			((WebDriver)drv).Navigate().GoToUrl("https://www.jisanresort.co.kr/w/reservation/golfResv/member_reserv01.asp");
			opentime = frm.coreData.getOpenDate(bookReqs[0].date);
			lock (WebDriverExtensions.lockObject)
			{
				IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='id']"));
				val.SendKeys(id);
				val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='pw']"));
				val.SendKeys(pwd);
				val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='btn_login btn_brown txt2']"));
				WebDriverExtensions.clickLock(val);
				try
				{
					val = ((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/div[1]/div[1]/div[2]/div/p[1]"));
					if (val != null)
					{
						frm.logtxtBox("T # " + threadIndex + "login Success " + (object)val);
					}
				}
				catch (Exception)
				{
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
		Thread.Sleep(30);
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		string text = "//*[@id='" + bookInfo2.date + "']";
		_ = DateTime.Now;
		int num2 = 0;
		_ = drv;
		if (DateTime.Today.Hour < 9)
		{
			opentime = opentime.AddDays(-(opentime.Day - DateTime.Today.Day));
		}
		while (num >= 0 && !frm.stopClicked)
		{
			((WebDriver)drv).Navigate().GoToUrl("https://www.jisanresort.co.kr/w/reservation/golfResv/member_reserv01.asp");
			try
			{
				Thread.Sleep(200);
				frm.logtxtBox("T # " + threadIndex + " day index " + bookInfo2?.ToString() + " cnt " + num2++);
				target = ((IWebDriver)(object)drv).FindElement(By.XPath(text), 10);
				Thread.Sleep(200);
				if (target == null)
				{
					frm.logtxtBox("No date link");
					continue;
				}
				if (!target.GetAttribute("style").StartsWith("cursor"))
				{
					frm.logtxtBox("T # " + threadIndex + " No Open");
					continue;
				}
				target.Click();
				IWebElement val = null;
				string[] array = new string[6] { "br1 221", "br1 331", "br1 111", "222", "br1 332", "br1 112" };
				for (int i = 0; i < 6; i++)
				{
					if (val != null)
					{
						break;
					}
					ReadOnlyCollection<IWebElement> readOnlyCollection = ((ISearchContext)((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='" + array[i] + "']"), 9)).FindElements(By.TagName("p"));
					if (readOnlyCollection == null)
					{
						frm.logtxtBox("teeList NULL");
						return OpResult.Fail;
					}
					frm.logtxtBox("Available " + readOnlyCollection.Count);
					if (readOnlyCollection.Count > 0)
					{
						val = find(readOnlyCollection, bookInfo2);
						if (val != null)
						{
							val = ((ISearchContext)val).FindElement(By.XPath("./span/button[1]"));
						}
					}
				}
				if (val != null)
				{
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay ");
					Thread.Sleep(200);
					opResult = tryReserve(val);
					if (opResult == OpResult.BookOneSuccess || opResult == OpResult.DuplicateFail || opResult == OpResult.DuplicateLogin)
					{
						workPool.finishDay(num);
					}
					else
					{
						workPool.returnDay(num);
					}
				}
				else
				{
					workPool.finishDay(num);
					frm.logtxtBox("T # " + threadIndex + "NoProperTime");
				}
			}
			catch (Exception ex)
			{
				workPool.returnDay(num);
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex);
			}
			break;
		}
		return OpResult.Fail;
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
		catch (Exception)
		{
		}
		return result;
	}

	private OpResult tryReserve(IWebElement btn)
	{
		OpResult result = OpResult.Fail;
		try
		{
			btn.Click();
			Thread.Sleep(100);
			IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("//html/body/div/div[2]/div[4]/button"), 3);
			if (val == null)
			{
				frm.logtxtBox("no button");
				return result;
			}
			try
			{
				WebDriverExtensions.clickLock(val);
				frm.logtxtBox("T # " + threadIndex + " reserve button clicked " + DateTime.Now.ToString("HH:mm:ss.ffffff"));
				Thread.Sleep(300);
				IWebElement val2 = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='fs30 brown pb10']"));
				if (val2 == null && !val2.Text.Contains("예약 조회"))
				{
					WebDriverExtensions.handleAlert((IWebDriver)(object)drv);
					result = OpResult.Fail;
					return result;
				}
				frm.logtxtBox("T # " + threadIndex + "예약 성공" + val2.Text);
			}
			catch (Exception ex)
			{
				string text = WebDriverExtensions.handleAlert((IWebDriver)(object)drv);
				frm.logtxtBox("found slot and trying to book " + ex?.ToString() + ((text != null) ? text : ""));
			}
		}
		catch (Exception ex2)
		{
			string text2 = "";
			frm.logtxtBox(" Reservation Buttone click, but Failed " + ex2?.ToString() + ((text2 != null) ? text2 : ""));
		}
		return result;
	}
}
