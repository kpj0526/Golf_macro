using System;
using System.Collections.ObjectModel;
using System.Threading;
using OpenQA.Selenium;

namespace booking;

internal class daeMyung : club
{
	private IWebElement target;

	private bool needAlert;

	private string loginUrl = "https://www.sonofelicecc.com/login.dp/dmparse.dm?url=/rsvcc.cal.dp/dmparse.dm?fJiyukCd=60#startCal";

	private string[] clubs = new string[3] { "https://www.sonofelicecc.com/rsv.cal.dp/dmparse.dm?fJiyukCd=60#startCal", "https://www.sonofelicecc.com/rsvcc.cal.dp/dmparse.dm?fJiyukCd=30#startCal", "https://www.sonofelicecc.com/rsv.cal.dp/dmparse.dm?fJiyukCd=01#startCal" };

	public daeMyung(int pManagerId)
	{
		managerId = pManagerId;
	}

	public override IWebElement compare(IWebElement tee, bookInfo req)
	{
		IWebElement result = null;
		try
		{
			string[] array = tee.GetAttribute("href").Replace("'", "").Split(',');
			int num = int.Parse(array[2]);
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
				needAlert = array[5] == "0";
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + "compare " + ex);
		}
		return result;
	}

	public override void logout(Form1 _frm)
	{
		try
		{
			((WebDriver)drv).FindElement(By.XPath("//*[@id='header']/div/div[1]/ul[2]/li[1]/a")).Click();
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + " logout " + ex);
		}
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
			((WebDriver)drv).Navigate().GoToUrl(loginUrl);
			((WebDriver)drv).FindElement(By.Id("cyberId")).SendKeys(id);
			((WebDriver)drv).FindElement(By.XPath("//*[@id='cyberPass']")).SendKeys(pwd);
			((WebDriver)drv).FindElement(By.XPath("//*[@class='button3 blue']")).Click();
			Thread.Sleep(1000);
			try
			{
				string text = ((WebDriver)drv).SwitchTo().Alert().Text;
				if (text.Contains("중복") || text.Contains("일치하지") || text.Contains("필수입력"))
				{
					frm.logtxtBox("T # " + threadIndex + "login fail : alert" + text);
					return false;
				}
				frm.logtxtBox("T # " + threadIndex + "login alert " + text);
				((WebDriver)drv).SwitchTo().Alert().Accept();
			}
			catch (Exception ex)
			{
				frm.logtxtBox("T # " + threadIndex + " handleAlert " + ex.Message);
				if (ex.Message.Contains("중복"))
				{
					return false;
				}
			}
			frm.logtxtBox("thread # " + threadIndex + " Login Success");
			return true;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private (bool, string) setDatePath(string dateP)
	{
		int row = 0;
		int col = 0;
		int table = 0;
		bool posDate = GetPosDate(dateP, ref table, ref row, ref col);
		if (dateP.StartsWith("202205"))
		{
			row++;
		}
		string item = "//*[@id='container']/div[3]/div[5]/div[" + table + "]/fieldset/table/tbody/tr[" + row + "]/td[" + col + "]/a";
		return (posDate, item);
	}

	private void handleAlert()
	{
		try
		{
			((WebDriver)drv).SwitchTo().Alert().Accept();
		}
		catch (Exception)
		{
		}
	}

	public override OpResult bookOne()
	{
		int num = -1;
		num = workPool.allocDay();
		if (num < 0)
		{
			return OpResult.Success;
		}
		bookInfo bookInfo2 = bookReqs[num];
		string text;
		try
		{
			((WebDriver)drv).Navigate().GoToUrl(clubs[bookInfo2.course]);
			Thread.Sleep(300);
			bool flag;
			(flag, text) = setDatePath(bookInfo2.date);
			if (flag)
			{
				((WebDriver)drv).FindElement(By.XPath("//*[@id='container']/div[3]/div[5]/div[2]/ul/li[1]/a")).Click();
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + " : GoToUrl Error " + bookInfo2.course + " " + bookInfo2.date + " Msg" + ex);
			handleAlert();
			workPool.returnDay(num);
			return OpResult.Fail;
		}
		frm.logtxtBox(" start booking " + DateTime.Now.ToString() + ":" + bookInfo2.course + " " + bookInfo2.date);
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				frm.logtxtBox("T # " + threadIndex + " day index " + bookInfo2);
				try
				{
					target = ((WebDriver)drv).FindElement(By.XPath(text));
				}
				catch (Exception ex2)
				{
					frm.logtxtBox("T # " + threadIndex + " target path exception " + ex2);
					workPool.returnDay(num);
					if (ex2.Message.Contains("로그인을 하셔야만"))
					{
						return OpResult.FalalError;
					}
					goto end_IL_016b;
				}
				Thread.Sleep(100);
				string attribute = target.GetAttribute("title");
				if (attribute != "예약하기")
				{
					if (attribute == "예약대기" || attribute == "예약마감")
					{
						workPool.finishDay(num);
						frm.logtxtBox("thread # " + threadIndex + " " + attribute + " 다음 예약 이동 day " + target.Text);
						return OpResult.BookOneSuccess;
					}
					frm.logtxtBox("T # " + threadIndex + " refresh  " + attribute + " day " + target.Text);
					((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='today_reservBx']/span/a"), 5).Click();
					Thread.Sleep(30);
					continue;
				}
				target.Click();
				Thread.Sleep(50);
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((ISearchContext)((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='rsvTableBody']"), 5)).FindElements(By.TagName("tr"));
				if (readOnlyCollection.Count == 0)
				{
					return OpResult.Fail;
				}
				bool flag2 = false;
				IWebElement val = null;
				int alertHandle = 0;
				string text2 = null;
				foreach (IWebElement item in readOnlyCollection)
				{
					ReadOnlyCollection<IWebElement> readOnlyCollection2 = ((ISearchContext)item).FindElements(By.TagName("td"));
					bool flag3 = false;
					if (readOnlyCollection2[1].Text.Contains("분"))
					{
						flag3 = true;
					}
					text2 = readOnlyCollection2[flag3 ? 1 : 0].Text.Replace("시", "").Replace("분", "").Replace(" ", "");
					int num2 = checkTime(text2, bookInfo2.startTime, bookInfo2.endTime);
					if (num2 == 1)
					{
						continue;
					}
					if (num2 == 2 || num2 < 0)
					{
						break;
					}
					val = readOnlyCollection2[flag3 ? 5 : 4];
					frm.logtxtBox("T # " + threadIndex + " Time Ok : " + text2 + val.Text + " " + readOnlyCollection2[flag3 ? 4 : 3].Text + bookInfo2);
					if (val.Text == "예약")
					{
						flag2 = true;
						if (readOnlyCollection2[flag3 ? 4 : 3].Text != "3/4인")
						{
							alertHandle = ((bookInfo2.course == 1) ? 1 : 2);
						}
						break;
					}
				}
				OpResult opResult = OpResult.Fail;
				if (flag2)
				{
					opResult = tryReserve(((ISearchContext)val).FindElement(By.TagName("a")), alertHandle);
					if (opResult == OpResult.BookOneSuccess)
					{
						frm.logtxtBox("T # " + threadIndex + "예약완료 time " + text2);
					}
				}
				else
				{
					frm.logtxtBox("T # " + threadIndex + " Available " + readOnlyCollection.Count + " 자리 , 시간 안 맞음");
					opResult = OpResult.NoProperTime;
				}
				if (opResult != OpResult.NoProperTime && opResult != OpResult.BookOneSuccess && opResult != OpResult.DuplicateFail)
				{
					continue;
				}
				workPool.finishDay(num);
				return OpResult.OneFinish;
				end_IL_016b:;
			}
			catch (Exception ex3)
			{
				frm.logtxtBox("T # " + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex3);
				workPool.returnDay(num);
			}
			break;
		}
		return OpResult.Fail;
	}

	private void handleAlerts(bool needAlert)
	{
		((WebDriver)drv).SwitchTo().Alert().Accept();
	}

	private OpResult tryReserve(IWebElement btn, int alertHandle)
	{
		OpResult result = OpResult.Fail;
		_ = ((WebDriver)drv).CurrentWindowHandle;
		try
		{
			btn.Click();
			Thread.Sleep(10);
			handleAlert();
			switch (alertHandle)
			{
			case 1:
				handleAlert();
				break;
			case 2:
			{
				IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/div[1]/div[4]/div/dl/dd[6]/a"));
				if (val != null)
				{
					val.Click();
				}
				break;
			}
			}
			string text = null;
			IWebElement val2 = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='rsvForm']/div/fieldset/table/tbody/tr[8]/th/a/input"));
			if (val2 == null)
			{
				frm.logtxtBox("T # " + threadIndex + " no captcha");
				return result;
			}
			val2.Click();
			Thread.Sleep(50);
			val2 = ((IWebDriver)(object)drv).FindElement(By.CssSelector("#popContainer > div.contents > div.txtc > a.button.blue"));
			if (val2 == null)
			{
				frm.logtxtBox("T # " + threadIndex + " no captcha");
				return result;
			}
			val2.Click();
			Thread.Sleep(50);
			handleAlert();
			Thread.Sleep(50000);
			return OpResult.Success;
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + " Reservation Buttone click, but Failed " + ex);
			handleAlert();
			return result;
		}
	}

	public override bool monitorOne(string date_p)
	{
		try
		{
			string attribute = ((WebDriver)drv).FindElement(By.XPath("//*[@id='" + date_p + "']//a")).GetAttribute("title");
			if (!attribute.Contains("마감") && !attribute.Contains("오픈전"))
			{
				return true;
			}
		}
		catch (Exception)
		{
			return false;
		}
		return false;
	}
}
