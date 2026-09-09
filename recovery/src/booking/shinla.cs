using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using OpenQA.Selenium;

namespace booking;

internal class shinla : club
{
	private IWebElement target;

	private const string loginUrl = "https://sillacc.co.kr/member/login?returnURL=/reservation/golf";

	private string[] clubs = new string[1] { "/html/body/div[2]/form[2]/div/div/div[2]/div[2]/div[1]/ul/li[1]" };

	public override IWebElement compare(IWebElement tee, bookInfo req)
	{
		IWebElement val = null;
		int num = 0;
		try
		{
			ReadOnlyCollection<IWebElement> readOnlyCollection = ((ISearchContext)tee).FindElements(By.TagName("td"));
			num = int.Parse(readOnlyCollection[0].Text.Replace(":", ""));
			if (num >= req.startTime)
			{
				if (num > req.endTime)
				{
					val = null;
					teeIndex = -1;
				}
				else
				{
					if (readOnlyCollection[req.starterIndex + 1].Text == "신청")
					{
						val = ((ISearchContext)tee).FindElement(By.XPath(".//td[" + (req.starterIndex + 2) + "]/ button"));
						if (val.TagName != "button")
						{
							val = null;
						}
						else
						{
							frm.logtxtBox("T # " + threadIndex + " OK " + num);
						}
					}
					teeIndex++;
				}
			}
		}
		catch (Exception ex)
		{
			frm.logtxtBox("T # " + threadIndex + " compare Error " + num + " " + ex);
			return null;
		}
		return val;
	}

	private bool buildTeeTable()
	{
		DateTime dateTime = DateTime.ParseExact(bookReqs[0].date, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None);
		frm.logtxtBox("T # " + threadIndex + " buildTeeTable start");
		while (true)
		{
			dateTime = dateTime.AddDays(-1.0);
			try
			{
				(bool, string) tuple = setDatePath(dateTime.ToString("yyyyMMdd"));
				IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath(tuple.Item2));
				if (!val.GetAttribute("title").Contains("잔여팀"))
				{
					continue;
				}
				WebDriverExtensions.clickLock(val);
				Thread.Sleep(300);
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((IWebDriver)(object)drv).FindElements(By.XPath("//*[@id='tabCourseALL']/div/div/table/tbody/tr"));
				Thread.Sleep(30);
				List<int> list = new List<int>();
				foreach (IWebElement item in readOnlyCollection)
				{
					ReadOnlyCollection<IWebElement> readOnlyCollection2 = ((ISearchContext)item).FindElements(By.TagName("td"));
					list.Add(int.Parse(readOnlyCollection2[0].Text.Replace(":", "")));
				}
				for (int i = 0; i < bookReqs.Count; i++)
				{
					bookInfo bookInfo2 = bookReqs[i];
					int num = list.BinarySearch(bookInfo2.startTime);
					indexTeeTable[i] = ((num < 0) ? (~num) : num);
				}
				break;
			}
			catch (Exception ex)
			{
				frm.logtxtBox("T # " + threadIndex + " buildTeeTable Error " + ex);
				return false;
			}
		}
		frm.logtxtBox("T # " + threadIndex + " buildTeeTable Done");
		return true;
	}

	public override bool login(Form1 _frm)
	{
		frm = _frm;
		try
		{
			((WebDriver)drv).Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1L);
			((WebDriver)drv).Navigate().GoToUrl("https://sillacc.co.kr/member/login?returnURL=/reservation/golf");
			lock (WebDriverExtensions.lockObject)
			{
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='usrId']")).SendKeys(id);
				((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='usrPwd']")).SendKeys(pwd);
				WebDriverExtensions.clickLock(((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='fnLogin']")));
				Thread.Sleep(500);
				try
				{
					string text = ((WebDriver)drv).SwitchTo().Alert().Text;
					if (!text.Contains("환영"))
					{
						frm.logtxtBox("T # " + threadIndex + "login fail " + text);
						return false;
					}
					((WebDriver)drv).SwitchTo().Alert().Accept();
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

	private (bool, string) setDatePath(string dateP)
	{
		int row = 0;
		int col = 0;
		int table = 0;
		bool posDate = GetPosDate(dateP, ref table, ref row, ref col);
		string text = ((table != 1) ? "B" : "A");
		text = "//*[@id='" + text + dateP + "']/a";
		return (posDate, text);
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
		frm.logtxtBox("T # " + threadIndex);
		num = workPool.allocDay();
		if (num < 0)
		{
			return OpResult.Success;
		}
		frm.logtxtBox("T # " + threadIndex + " : alloc_day " + num);
		bookInfo bookInfo2 = bookReqs[num];
		teeIndex = indexTeeTable[num];
		string item = setDatePath(bookInfo2.date).Item2;
		frm.logtxtBox("T # " + threadIndex + " start booking " + bookInfo2);
		_ = DateTime.Now;
		while (num >= 0 && !frm.stopClicked)
		{
			try
			{
				target = ((IWebDriver)(object)drv).FindElement(By.XPath(item));
				string attribute = target.GetAttribute("title");
				if (!attribute.Contains("잔여팀"))
				{
					if (attribute.Contains("마감"))
					{
						workPool.finishDay(num);
						frm.logtxtBox("T # " + threadIndex + " " + attribute + " 다음 예약 이동");
						return OpResult.BookOneSuccess;
					}
					frm.logtxtBox("T # " + threadIndex + " refresh  " + attribute);
					continue;
				}
				WebDriverExtensions.clickLock(target);
				Thread.Sleep(300);
				ReadOnlyCollection<IWebElement> readOnlyCollection = ((IWebDriver)(object)drv).FindElements(By.XPath("//*[@id='tabCourseALL']/div/div/table/tbody/tr"));
				if (readOnlyCollection.Count <= 1)
				{
					return OpResult.Fail;
				}
				string text = null;
				IWebElement val = find(readOnlyCollection, bookInfo2);
				if (val != null)
				{
					OpResult opResult = OpResult.Fail;
					frm.logtxtBox("T # " + threadIndex + "Time Okay " + text);
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
				}
			}
			catch (Exception ex)
			{
				frm.logtxtBox("T #" + threadIndex + " fail-return booking " + bookInfo2.course + " " + bookInfo2.date + " Msg " + ex);
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
			((WebDriver)drv).ExecuteScript("arguments[0].click();", new object[1] { btn });
			Thread.Sleep(100);
			frm.logtxtBox("T # " + threadIndex + " submit button clicked");
			IWebElement val = ((IWebDriver)(object)drv).FindElement(By.XPath("//*[@id='golfTimeDiv2']/div[3]/div/div[1]/button"));
			if (val == null)
			{
				frm.logtxtBox("no button");
				return result;
			}
			try
			{
				WebDriverExtensions.clickLock(val);
				frm.logtxtBox("T # " + threadIndex + " reserve button clicked " + DateTime.Now.ToString("HH:mm:ss.ffffff"));
				Thread.Sleep(200);
				string text = ((WebDriver)drv).SwitchTo().Alert().Text;
				((WebDriver)drv).SwitchTo().Alert().Accept();
				Thread.Sleep(300);
				result = (text.Contains("동일한") ? OpResult.DuplicateFail : (text.Contains("예약이 완료") ? OpResult.BookOneSuccess : ((!text.Contains("다른 곳에서")) ? OpResult.Fail : OpResult.DuplicateLogin)));
				frm.logtxtBox("T # " + threadIndex + " " + text);
			}
			catch (Exception ex)
			{
				frm.logtxtBox("thread # " + threadIndex + " found slot and trying to book " + ex);
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
