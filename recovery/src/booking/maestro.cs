using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

namespace booking;

internal class maestro : club
{
	private DateTime opentime;

	private const string loginUrl = "https://www.maestrocc.co.kr/login/login.asp";

	private string bookBtnHome = "//*[@id='main_calendar']/div[2]/a/img";

	private string[] logins = new string[3] { "//*[@id='login_id']", "//*[@id='login_pw']", "//*[@class='bt_login']" };

	public maestro(int pManagerId)
	{
		managerId = pManagerId;
	}

	public override bool monitorOne(string date_p)
	{
		return false;
	}

	public override IWebElement compare(IWebElement tee, bookInfo req)
	{
		return null;
	}

	public override void setValues(ChromeDriver drv_p, int threadIndex_p, CoreData cd, List<bookInfo> bookReqs_p, ref dayPool _workPool)
	{
		base.setValues(drv_p, threadIndex_p, cd, bookReqs_p, ref _workPool);
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

	public override bool login(Form1 _frm)
	{
		frm = _frm;
		try
		{
			string userId = managerId + "_" + id;
			mq = new mqttClient(userId);
			frm.coreData.testServer(mq, 1, frm);
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1L);
			((WebDriver)drv).Navigate().GoToUrl("https://www.maestrocc.co.kr/login/login.asp");
			Thread.Sleep(500);
			bookInfo bookInfo2 = bookReqs[0];
			int year = int.Parse(bookInfo2.date.Substring(0, 4));
			int num = int.Parse(bookInfo2.date.Substring(4, 2)) - 1;
			if (int.Parse(bookInfo2.date.Substring(6, 2)) > DateTime.DaysInMonth(year, num))
			{
				num++;
			}
			opentime = frm.coreData.getOpenDate(bookInfo2.date);
			sleepShortToOpen(opentime.AddMinutes(-10.0));
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath(logins[0])).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath(logins[1])).SendKeys(pwd);
				((IWebDriver)(object)drv).FindElement(By.XPath(logins[2])).Click();
				Thread.Sleep(300);
				try
				{
					string text = WebDriverExtensions.handleAlert((IWebDriver)(object)drv);
					if (text != null && text.Contains("비밀번호"))
					{
						frm.logtxtBox("thread # " + threadIndex + " Login Failed" + text);
						return false;
					}
					Thread.Sleep(2000);
					((WebDriver)drv).SwitchTo().Window(((WebDriver)drv).WindowHandles[0]);
					Thread.Sleep(200);
					((IWebDriver)(object)drv).FindElement(By.XPath(bookBtnHome)).Click();
				}
				catch (Exception ex)
				{
					frm.logtxtBox("T #" + threadIndex + "Login Error " + 0 + " Msg" + ex);
					return false;
				}
			}
		}
		catch (Exception)
		{
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
		return $"//*[@id='calendar_view_ajax_{table}']/table/tbody/tr[{row}]/td[{col}]/a";
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
		_ = bookReqs[0].starter;
		string text = selectDate(bookInfo2.date);
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		sleepShortToOpen(opentime, 30);
		((WebDriver)drv).Navigate().Refresh();
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath(text), 10);
				if (val == null || val.GetAttribute("class").Length == 0)
				{
					frm.logtxtBox("No Open");
					((WebDriver)drv).Navigate().Refresh();
					Thread.Sleep(100);
					continue;
				}
				val.Click();
				Thread.Sleep(200);
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((IWebDriver)(object)drv).FindElements(By.XPath(".//button[contains(@id, 'timeresbtn')]"), 10);
				new List<(string, IWebElement)>();
				if (readOnlyCollection == null || readOnlyCollection.Count == 0)
				{
					frm.logtxtBox("No Available");
					((WebDriver)drv).Navigate().Refresh();
					continue;
				}
				frm.logtxtBox("Available " + readOnlyCollection.Count);
				new List<IWebElement>();
				IWebElement val2 = null;
				char c = 'A';
				foreach (IWebElement item in readOnlyCollection)
				{
					string attribute = item.GetAttribute("id");
					if (attribute[2] < c)
					{
						continue;
					}
					int num2 = int.Parse(attribute.Substring(13));
					if (num2 >= bookInfo2.startTime)
					{
						if (num2 <= bookInfo2.endTime)
						{
							val2 = item;
							break;
						}
						if (c == 'D')
						{
							break;
						}
						c = (char)(c + 1);
					}
				}
				if (val2 != null)
				{
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay " + val2.Text);
					opResult = tryReserve(val2);
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

	private string myhandleAlert(IWebDriver drv)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected O, but got Unknown
		string result = null;
		try
		{
			((DefaultWait<IWebDriver>)new WebDriverWait(drv, TimeSpan.FromSeconds(3L))).Until<IAlert>(ExpectedConditions.AlertIsPresent());
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
		OpResult opResult = OpResult.Fail;
		string text = "";
		try
		{
			btn.Click();
			Thread.Sleep(50);
			((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='golfdataform']/div/button[1]")).Click();
			text = myhandleAlert((IWebDriver)(object)drv);
			if (text.Contains("타임을 예약"))
			{
				Thread.Sleep(100);
				if (((IWebDriver)(object)drv).FindElement(By.ClassName("tit_area")).Text.Contains("예약확인"))
				{
					frm.logtxtBox("성공했습니다 !!!!! ");
					Thread.Sleep(500000);
					opResult = OpResult.BookOneSuccess;
				}
			}
			if (opResult != OpResult.Success)
			{
				frm.logtxtBox("tryReserve Click Failed with " + text);
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox(" Reservation Buttone click, but Failed :" + ex);
		}
		return opResult;
	}
}
