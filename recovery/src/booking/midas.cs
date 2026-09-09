using System;
using System.Collections.ObjectModel;
using System.Threading;
using OpenQA.Selenium;

namespace booking;

internal class midas : club
{
	private IWebElement target;

	private const string loginUrl = "https://www.midasgolf.co.kr/Member/Login";

	private const string bookUrl = "https://www.midasgolf.co.kr/Reservation/ReservCalendar";

	public override bool monitorOne(string date_p)
	{
		return false;
	}

	private bool buildTeeTable()
	{
		int num = 18;
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
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1L);
			((WebDriver)drv).Navigate().GoToUrl("https://www.midasgolf.co.kr/Member/Login");
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='user_id']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='user_pwd']")).SendKeys(pwd);
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@class='btn btn-primary btn-lg btn-block fw-bold mt-4']")));
				Thread.Sleep(500);
				try
				{
					string text = ((WebDriver)drv).SwitchTo().Alert().Text;
					if (!text.Contains("환영"))
					{
						frm.logtxtBox("T # " + threadIndex + "login fail " + text);
						return false;
					}
				}
				catch (Exception)
				{
				}
				Thread.Sleep(500);
				buildTeeTable();
			}
		}
		catch (Exception)
		{
			return false;
		}
		return true;
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
		frm.logtxtBox("T # " + threadIndex + " : alloc_day " + num);
		bookInfo bookInfo2 = bookReqs[num];
		teeIndex = indexTeeTable[num];
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		string text;
		try
		{
			text = "//*[@data-day='" + bookInfo2.date + "']";
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + " : GoToUrl Error " + bookInfo2.course + " " + bookInfo2.date + " Msg" + ex);
			handleAlert();
			workPool.returnDay(num);
			return OpResult.Fail;
		}
		_ = DateTime.Now;
		int num2 = 0;
		while (num >= 0 && !frm.stopClicked)
		{
			((WebDriver)drv).Navigate().GoToUrl("https://www.midasgolf.co.kr/Reservation/ReservCalendar");
			if (frm.testMode)
			{
				target = ((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/div[4]/section[2]/div/div/div[3]/a"));
				target.Click();
			}
			try
			{
				Thread.Sleep(100);
				frm.logtxtBox("T # " + threadIndex + " day index " + bookInfo2?.ToString() + " cnt " + num2++);
				target = ((IWebDriver)(object)drv).FindElement(By.XPath(text));
				if (target == null)
				{
					frm.logtxtBox("No date link");
					continue;
				}
				string attribute = target.GetAttribute("title");
				if (attribute == "오픈전")
				{
					frm.logtxtBox("T # " + threadIndex + " No Open");
					continue;
				}
				if (attribute == "마감")
				{
					workPool.finishDay(num);
					frm.logtxtBox("T # " + threadIndex + "마감");
					break;
				}
				frm.logtxtBox("Date Title " + attribute);
				target.Click();
				Thread.Sleep(50);
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((IWebDriver)(object)drv).FindElements(By.XPath("//*[@id='AjaxTime']/section[2]/div/div/table//td[2]"));
				if (readOnlyCollection == null)
				{
					break;
				}
				if (readOnlyCollection.Count == 0)
				{
					return OpResult.NoSpace;
				}
				IWebElement val = find(readOnlyCollection, bookInfo2);
				if (val != null)
				{
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay ");
					val = ((ISearchContext)((ISearchContext)val).FindElement(By.XPath(".."))).FindElement(By.XPath(".//td[6]/button"));
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
			catch (Exception ex2)
			{
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex2);
				workPool.returnDay(num);
			}
			break;
		}
		return OpResult.Fail;
	}

	private OpResult tryReserve(IWebElement btn)
	{
		OpResult result = OpResult.Fail;
		try
		{
			_ = btn.Location;
			((WebDriver)drv).ExecuteScript("arguments[0].scrollIntoView(true);", new object[1] { btn });
			Thread.Sleep(300);
			btn.Click();
			Thread.Sleep(50);
			IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='btn-reservation']"));
			if (val == null)
			{
				frm.logtxtBox("no button");
				return result;
			}
			try
			{
				WebDriverExtensions.clickLock(val);
				frm.logtxtBox("T # " + threadIndex + " reserve button clicked " + DateTime.Now.ToString("HH:mm:ss.ffffff"));
				Thread.Sleep(50);
				string text = ((WebDriver)drv).SwitchTo().Alert().Text;
				((WebDriver)drv).SwitchTo().Alert().Accept();
				Thread.Sleep(30);
				if (text.Contains("예약하시겠습니까"))
				{
					IWebElement val2 = ((IWebDriver)(object)drv).FindElement(By.XPath("/html/body/div[3]/section[1]/div/h2"));
					if (val2.Text == "마이페이지")
					{
						frm.logtxtBox("T # " + threadIndex + "예약 성공 ");
						return OpResult.BookOneSuccess;
					}
					text += val2.Text;
				}
				frm.logtxtBox("T # " + threadIndex + "예약 실패 " + text);
			}
			catch (Exception ex)
			{
				frm.logtxtBox("T # " + threadIndex + " found slot and trying to book " + ex);
				handleAlert();
			}
		}
		catch (Exception ex2)
		{
			frm.logtxtBox("thread # " + threadIndex + " Reservation Buttone click, but Failed " + ex2);
			handleAlert();
		}
		return result;
	}
}
